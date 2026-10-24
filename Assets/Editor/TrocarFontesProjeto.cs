// Troca as fontes de todas as cenas e prefabs do projeto pelas fontes do cliente.
// Menu: Tools > Fontes > Trocar Fontes do Projeto
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class TrocarFontesProjeto : EditorWindow
{
    TMP_FontAsset fonteTMP;
    Font fonteLegacy;
    bool manterFontesDoCliente = true;
    string pastaCliente = "Assets/Fonts/Elas_No_Jogo";
    bool incluirCenas = true;
    bool incluirPrefabs = true;
    Vector2 scroll;

    [MenuItem("Tools/Fontes/Trocar Fontes do Projeto")]
    static void Abrir() { GetWindow<TrocarFontesProjeto>("Trocar Fontes"); }

    void OnEnable()
    {
        if (fonteTMP == null) fonteTMP = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(pastaCliente + "/Barlow/Barlow-Regular SDF.asset");
        if (fonteLegacy == null) fonteLegacy = AssetDatabase.LoadAssetAtPath<Font>(pastaCliente + "/Barlow/Barlow-Regular.ttf");
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Fontes novas", EditorStyles.boldLabel);
        fonteTMP = (TMP_FontAsset)EditorGUILayout.ObjectField("TextMeshPro", fonteTMP, typeof(TMP_FontAsset), false);
        fonteLegacy = (Font)EditorGUILayout.ObjectField("Text (Legacy)", fonteLegacy, typeof(Font), false);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Opcoes", EditorStyles.boldLabel);
        manterFontesDoCliente = EditorGUILayout.ToggleLeft("Nao mexer em textos que ja usam fontes da pasta do cliente", manterFontesDoCliente);
        pastaCliente = EditorGUILayout.TextField("Pasta do cliente", pastaCliente);
        incluirCenas = EditorGUILayout.ToggleLeft("Incluir cenas", incluirCenas);
        incluirPrefabs = EditorGUILayout.ToggleLeft("Incluir prefabs", incluirPrefabs);
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Faca um commit antes de aplicar. 'So listar' mostra no Console o que seria trocado, sem alterar nada.", MessageType.Info);
        if (GUILayout.Button("So listar (nao altera nada)", GUILayout.Height(28))) Executar(false);
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("Aplicar em todo o projeto", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog("Trocar fontes", "Isso vai alterar e salvar todas as cenas e prefabs. Continuar?", "Sim", "Cancelar"))
                Executar(true);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndScrollView();
    }

    bool EhDoCliente(Object o)
    {
        if (o == null) return false;
        string p = AssetDatabase.GetAssetPath(o).Replace('\\', '/');
        return p.StartsWith(pastaCliente.TrimEnd('/') + "/");
    }

    // Retorna quantos textos foram (ou seriam) trocados
    int Processar(GameObject raiz, bool aplicar, bool ehCena, StringBuilder log, string origem)
    {
        int n = 0;
        if (fonteTMP != null)
        {
            foreach (var t in raiz.GetComponentsInChildren<TMP_Text>(true))
            {
                if (ehCena && PrefabUtility.IsPartOfPrefabInstance(t) && !PrefabUtility.IsAddedComponentOverride(t)) continue; // tratado no prefab
                if (t.font == fonteTMP) continue;
                if (manterFontesDoCliente && EhDoCliente(t.font)) continue;
                log.AppendLine("  [TMP] " + Caminho(t.transform) + "  (" + (t.font ? t.font.name : "None") + " -> " + fonteTMP.name + ")");
                if (aplicar)
                {
                    Undo.RecordObject(t, "Trocar fonte");
                    t.font = fonteTMP;
                    t.fontSharedMaterial = fonteTMP.material;
                    EditorUtility.SetDirty(t);
                }
                n++;
            }
        }
        if (fonteLegacy != null)
        {
            foreach (var t in raiz.GetComponentsInChildren<Text>(true))
            {
                if (ehCena && PrefabUtility.IsPartOfPrefabInstance(t) && !PrefabUtility.IsAddedComponentOverride(t)) continue;
                if (t.font == fonteLegacy) continue;
                if (manterFontesDoCliente && EhDoCliente(t.font)) continue;
                log.AppendLine("  [Legacy] " + Caminho(t.transform) + "  (" + (t.font ? t.font.name : "None") + " -> " + fonteLegacy.name + ")");
                if (aplicar)
                {
                    Undo.RecordObject(t, "Trocar fonte");
                    t.font = fonteLegacy;
                    EditorUtility.SetDirty(t);
                }
                n++;
            }
        }
        return n;
    }

    static string Caminho(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    void Executar(bool aplicar)
    {
        if (fonteTMP == null && fonteLegacy == null) { Debug.LogWarning("[Fontes] Escolha pelo menos uma fonte."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var setup = EditorSceneManager.GetSceneManagerSetup();
        var log = new StringBuilder();
        int total = 0, arquivos = 0;

        try
        {
            // 1) Prefabs primeiro (assim as instancias nas cenas herdam a troca)
            if (incluirPrefabs)
            {
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.StartsWith("Assets/TextMesh Pro")) continue;
                    if (!path.EndsWith(".prefab")) continue;
                    if (EditorUtility.DisplayCancelableProgressBar("Fontes - Prefabs", path, (float)i / guids.Length)) break;

                    var sb = new StringBuilder();
                    GameObject conteudo = null;
                    try
                    {
                        conteudo = PrefabUtility.LoadPrefabContents(path);
                        int n = Processar(conteudo, aplicar, false, sb, path);
                        if (n > 0)
                        {
                            if (aplicar) PrefabUtility.SaveAsPrefabAsset(conteudo, path);
                            log.AppendLine("PREFAB " + path + " : " + n);
                            log.Append(sb);
                            total += n; arquivos++;
                        }
                    }
                    catch (System.Exception e) { Debug.LogWarning("[Fontes] Erro em " + path + ": " + e.Message); }
                    finally { if (conteudo != null) PrefabUtility.UnloadPrefabContents(conteudo); }
                }
            }

            // 2) Cenas
            if (incluirCenas)
            {
                var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.StartsWith("Assets/TextMesh Pro")) continue;
                    if (EditorUtility.DisplayCancelableProgressBar("Fontes - Cenas", path, (float)i / guids.Length)) break;
                    try
                    {
                        var cena = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        var sb = new StringBuilder();
                        int n = 0;
                        foreach (var raiz in cena.GetRootGameObjects()) n += Processar(raiz, aplicar, true, sb, path);
                        if (n > 0)
                        {
                            if (aplicar) { EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveScene(cena); }
                            log.AppendLine("CENA " + path + " : " + n);
                            log.Append(sb);
                            total += n; arquivos++;
                        }
                    }
                    catch (System.Exception e) { Debug.LogWarning("[Fontes] Erro em " + path + ": " + e.Message); }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            AssetDatabase.SaveAssets();
        }

        string titulo = (aplicar ? "[Fontes] TROCADOS: " : "[Fontes] SO LISTAGEM: ") + total + " textos em " + arquivos + " arquivos.";
        Debug.Log(titulo + "\n" + log);
        File.WriteAllText("Logs/TrocarFontes.txt", titulo + "\n" + log);
        Debug.Log("[Fontes] Relatorio completo salvo em Logs/TrocarFontes.txt");
    }
}