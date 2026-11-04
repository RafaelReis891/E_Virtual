#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vegetacao.EditorTools
{
    /// <summary>
    /// Ferramenta de TESTE: cria marcadores (esferas/cubos) nas posicoes das arvores de BH
    /// lidas de bh_tree_positions.csv, para conferir se o eixo X precisa ser invertido.
    /// Os objetos criados usam HideFlags.DontSaveInEditor (nao sao gravados na cena) e
    /// nao criam nenhum asset de material.
    /// </summary>
    public static class MarcadoresArvoresBH
    {
        const string CsvPath = "Assets/Editor/Vegetacao/bh_tree_positions.csv";
        const string RootUnity = "_TesteMarcadoresBH";
        const string RootFbx = "_TesteMarcadoresBH_FBX";

        static readonly Dictionary<string, Material> s_Materials = new Dictionary<string, Material>();

        struct TreeRow
        {
            public string type;
            public int idx;
            public Vector3 unityPos;
            public Vector3 fbxPos;
        }

        [MenuItem("Tools/Vegetacao/Marcadores Teste (X Unity)")]
        static void CriarUnity() { Criar(false); }

        [MenuItem("Tools/Vegetacao/Marcadores Teste (X FBX)")]
        static void CriarFbx() { Criar(true); }

        [MenuItem("Tools/Vegetacao/Remover Marcadores")]
        static void Remover()
        {
            int n = RemoverRoot(RootUnity) + RemoverRoot(RootFbx);
            MarcarSujo();
            Debug.Log("[MarcadoresArvoresBH] Raizes removidas: " + n);
        }

        static void Criar(bool usarFbx)
        {
            var rows = LerCsv();
            if (rows == null || rows.Count == 0)
            {
                Debug.LogError("[MarcadoresArvoresBH] Nao consegui ler " + CsvPath);
                return;
            }

            string rootName = usarFbx ? RootFbx : RootUnity;
            RemoverRoot(rootName);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Criar " + rootName);
            int group = Undo.GetCurrentGroup();

            var root = new GameObject(rootName);
            root.transform.position = Vector3.zero;
            root.hideFlags = HideFlags.DontSaveInEditor;
            Undo.RegisterCreatedObjectUndo(root, "Criar " + rootName);

            foreach (var r in rows)
            {
                var go = GameObject.CreatePrimitive(usarFbx ? PrimitiveType.Cube : PrimitiveType.Sphere);
                var col = go.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);

                go.name = r.type + "_" + r.idx;
                go.hideFlags = HideFlags.DontSaveInEditor;
                go.transform.SetParent(root.transform, false);
                go.transform.position = usarFbx ? r.fbxPos : r.unityPos;
                go.transform.localScale = Vector3.one;

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = PegarMaterial(r.type, usarFbx);
                Undo.RegisterCreatedObjectUndo(go, "Criar marcador");
            }

            Undo.CollapseUndoOperations(group);
            MarcarSujo();
            Debug.Log("[MarcadoresArvoresBH] " + rows.Count + " marcadores criados em '" + rootName + "' ("
                      + (usarFbx ? "coordenadas FBX brutas, cubos" : "X invertido (unity_x), esferas") + ").");
        }

        static int RemoverRoot(string nome)
        {
            int n = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                foreach (var go in sc.GetRootGameObjects())
                {
                    if (go != null && go.name == nome)
                    {
                        Undo.DestroyObjectImmediate(go);
                        n++;
                    }
                }
            }
            return n;
        }

        static void MarcarSujo()
        {
            var sc = SceneManager.GetActiveScene();
            if (sc.IsValid()) EditorSceneManager.MarkSceneDirty(sc);
        }

        static Color CorDaClasse(string type, bool fbx)
        {
            Color c;
            switch (type)
            {
                case "PALMEIRAS": c = Color.red; break;
                case "ARVORES_GRANDES": c = new Color(0.1f, 0.3f, 1f); break;
                case "ARVORES_MEDIAS": c = Color.yellow; break;
                case "ARBUSTOS": c = Color.magenta; break;
                default: c = Color.white; break;
            }
            // Conjunto FBX: tom mais claro/pastel, alem do formato cubo.
            return fbx ? Color.Lerp(c, Color.white, 0.55f) : c;
        }

        static Material PegarMaterial(string type, bool fbx)
        {
            string key = type + (fbx ? "|fbx" : "|unity");
            Material m;
            if (s_Materials.TryGetValue(key, out m) && m != null) return m;

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Standard");

            m = new Material(sh);
            m.name = "MarcadorTeste_" + key;
            m.hideFlags = HideFlags.HideAndDontSave; // material de runtime, nao vira asset
            Color c = CorDaClasse(type, fbx);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            s_Materials[key] = m;
            return m;
        }

        static List<TreeRow> LerCsv()
        {
            string text = null;
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvPath);
            if (ta != null) text = ta.text;
            else if (File.Exists(CsvPath)) text = File.ReadAllText(CsvPath);
            if (string.IsNullOrEmpty(text)) return null;

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return null;

            var header = lines[0].Split(',');
            int iType = Array.IndexOf(header, "type");
            int iIdx = Array.IndexOf(header, "idx");
            int iUx = Array.IndexOf(header, "unity_x");
            int iUy = Array.IndexOf(header, "unity_y");
            int iUz = Array.IndexOf(header, "unity_z");
            int iFx = Array.IndexOf(header, "fbx_x");
            int iFy = Array.IndexOf(header, "fbx_y");
            int iFz = Array.IndexOf(header, "fbx_z");
            if (iType < 0 || iIdx < 0 || iUx < 0 || iUy < 0 || iUz < 0 || iFx < 0 || iFy < 0 || iFz < 0)
            {
                Debug.LogError("[MarcadoresArvoresBH] Cabecalho do CSV inesperado: " + lines[0]);
                return null;
            }

            var list = new List<TreeRow>(lines.Length - 1);
            var ci = CultureInfo.InvariantCulture;
            for (int i = 1; i < lines.Length; i++)
            {
                var f = lines[i].Split(',');
                if (f.Length < header.Length) continue;
                var row = new TreeRow
                {
                    type = f[iType].Trim(),
                    idx = int.Parse(f[iIdx], ci),
                    unityPos = new Vector3(float.Parse(f[iUx], ci), float.Parse(f[iUy], ci), float.Parse(f[iUz], ci)),
                    fbxPos = new Vector3(float.Parse(f[iFx], ci), float.Parse(f[iFy], ci), float.Parse(f[iFz], ci)),
                };
                list.Add(row);
            }
            return list;
        }
    }
}
#endif
