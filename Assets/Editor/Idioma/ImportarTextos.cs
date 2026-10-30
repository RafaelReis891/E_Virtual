using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Le o CSV (PT;EN;...) e coloca o EN no componente TextoIdioma de cada texto igual ao PT.
// Mesmas cenas do export (Build) + prefabs. Textos sem EN no CSV ficam como estao.
public static class ImportarTextos
{
    static readonly string[] PastasIgnoradas = { "Assets/TextMesh Pro", "Assets/Starter Assets", "Assets/TutorialInfo", "Assets/Plugins" };

    [MenuItem("Tools/Idioma/Importar Textos (CSV)")]
    static void Importar()
    {
        string arq = EditorUtility.OpenFilePanel("CSV com PT;EN", Path.Combine(Application.dataPath, "../Localizacao"), "csv");
        if (string.IsNullOrEmpty(arq)) return;
        var mapa = LerCsv(arq);
        if (mapa.Count == 0) { Debug.LogWarning("[Idioma] Nenhuma linha com EN no CSV."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        int n = 0;
        try
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < prefabs.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(prefabs[i]);
                if (Ignorada(p)) continue;
                EditorUtility.DisplayProgressBar("Importar Textos", p, (float)i / prefabs.Length * 0.5f);
                var root = PrefabUtility.LoadPrefabContents(p);
                int k = Aplicar(root.GetComponentsInChildren<Component>(true), mapa, false);
                if (k > 0) { PrefabUtility.SaveAsPrefabAsset(root, p); n += k; }
                PrefabUtility.UnloadPrefabContents(root);
            }
            var cenas = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) cenas.Add(s.path);
            for (int i = 0; i < cenas.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Importar Textos", cenas[i], 0.5f + (float)i / cenas.Count * 0.5f);
                var cena = EditorSceneManager.OpenScene(cenas[i], OpenSceneMode.Single);
                var comps = new List<Component>();
                foreach (var r in cena.GetRootGameObjects()) comps.AddRange(r.GetComponentsInChildren<Component>(true));
                int k = Aplicar(comps.ToArray(), mapa, true);
                if (k > 0) { EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveScene(cena); n += k; }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        Debug.Log("[Idioma] Importacao concluida: " + n + " textos receberam EN (" + mapa.Count + " linhas com EN no CSV).");
    }

    static int Aplicar(Component[] comps, Dictionary<string, string> mapa, bool emCena)
    {
        int n = 0;
        foreach (var c in comps)
        {
            if (c == null) continue;
            string txt = null;
            if (c is TMPro.TMP_Text tmp) txt = tmp.text;
            else if (c is UnityEngine.UI.Text ui) txt = ui.text;
            else if (c is TextMesh tm) txt = tm.text;
            else continue;
            if (txt == null || !mapa.TryGetValue(Norm(txt), out string en)) continue;
            var ti = c.GetComponent<TextoIdioma>();
            if (ti == null) ti = Undo.AddComponent<TextoIdioma>(c.gameObject);
            if (ti.en == en) continue;
            ti.en = en;
            EditorUtility.SetDirty(ti);
            if (emCena) PrefabUtility.RecordPrefabInstancePropertyModifications(ti);
            n++;
        }
        return n;
    }

    static string Norm(string s) => s.Replace("\r\n", "\n").Trim();

    static bool Ignorada(string p)
    {
        foreach (var x in PastasIgnoradas) if (p.StartsWith(x)) return true;
        return p.Contains("ToBEdeleted");
    }

    // CSV com ';', aceita campos entre aspas com quebra de linha
    static Dictionary<string, string> LerCsv(string arq)
    {
        var mapa = new Dictionary<string, string>();
        string t = File.ReadAllText(arq, Encoding.UTF8);
        var linha = new List<string>(); var campo = new StringBuilder(); bool aspas = false; bool primeira = true;
        for (int i = 0; i <= t.Length; i++)
        {
            char ch = i < t.Length ? t[i] : '\n';
            if (aspas)
            {
                if (ch == '"') { if (i + 1 < t.Length && t[i + 1] == '"') { campo.Append('"'); i++; } else aspas = false; }
                else campo.Append(ch);
                continue;
            }
            if (ch == '"') aspas = true;
            else if (ch == ';') { linha.Add(campo.ToString()); campo.Clear(); }
            else if (ch == '\r') { }
            else if (ch == '\n')
            {
                linha.Add(campo.ToString()); campo.Clear();
                if (!primeira && linha.Count >= 2 && !string.IsNullOrWhiteSpace(linha[1]))
                    mapa[Norm(linha[0])] = linha[1].Replace("\r\n", "\n").Trim();
                primeira = false; linha.Clear();
            }
            else campo.Append(ch);
        }
        return mapa;
    }
}