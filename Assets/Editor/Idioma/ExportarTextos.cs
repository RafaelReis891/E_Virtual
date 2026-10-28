using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exporta os textos (TextMeshPro, UI Text, TextMesh) para um CSV. So le, nao altera nada.
// - Cenas: so as marcadas no Build Settings (ou so a cena aberta, pelo outro menu).
// - Prefabs do projeto (menos pastas de terceiros).
// - Ignora objetos dentro de "ToBEdeleted".
// - Textos iguais viram uma linha so, com a lista de onde aparecem.
public static class ExportarTextos
{
    static readonly string[] PastasIgnoradas = { "Assets/TextMesh Pro", "Assets/Starter Assets", "Assets/TutorialInfo", "Assets/Plugins" };
    const string Ignorar = "ToBEdeleted";

    class Item { public string pt; public bool dinamico; public List<string> onde = new List<string>(); }

    [MenuItem("Tools/Idioma/Exportar Textos (cenas do Build)")]
    public static void ExportarBuild()
    {
        var cenas = new List<string>();
        foreach (var s in EditorBuildSettings.scenes) if (s.enabled) cenas.Add(s.path);
        if (cenas.Count == 0) { Debug.LogWarning("[Idioma] Nenhuma cena marcada no Build Settings."); return; }
        Exportar(cenas, true, "textos_export.csv");
    }

    [MenuItem("Tools/Idioma/Exportar Textos (so cena aberta)")]
    public static void ExportarCenaAberta()
    {
        Scene c = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(c.path)) { Debug.LogWarning("[Idioma] Salve a cena antes."); return; }
        Exportar(new List<string> { c.path }, false, "textos_" + c.name + ".csv");
    }

    static void Exportar(List<string> cenas, bool incluirPrefabs, string nomeArquivo)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var itens = new Dictionary<string, Item>();
        var ordem = new List<string>();
        int total = 0;

        System.Action<Component, string> add = (comp, local) =>
        {
            string txt;
            if (!PegarTexto(comp, out txt)) return;
            if (Caminho(comp.transform).Contains(Ignorar)) return;
            // Instancia de prefab com o mesmo texto do prefab: ja conta no prefab
            var src = PrefabUtility.GetCorrespondingObjectFromSource(comp);
            if (src != null) { string ts; if (PegarTexto(src, out ts) && ts == txt) return; }
            txt = txt ?? "";
            total++;
            Item it;
            if (!itens.TryGetValue(txt, out it)) { it = new Item { pt = txt, dinamico = PareceDinamico(txt) }; itens[txt] = it; ordem.Add(txt); }
            it.onde.Add(local + " > " + Caminho(comp.transform));
        };

        try
        {
            if (incluirPrefabs)
            {
                string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
                for (int i = 0; i < prefabs.Length; i++)
                {
                    string p = AssetDatabase.GUIDToAssetPath(prefabs[i]);
                    if (PastaIgnorada(p)) continue;
                    EditorUtility.DisplayProgressBar("Exportar Textos", p, (float)i / prefabs.Length * 0.4f);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go == null) continue;
                    foreach (var comp in go.GetComponentsInChildren<Component>(true)) add(comp, Path.GetFileName(p));
                }
            }

            for (int i = 0; i < cenas.Count; i++)
            {
                string p = cenas[i];
                EditorUtility.DisplayProgressBar("Exportar Textos", p, 0.4f + (float)i / cenas.Count * 0.6f);
                var cena = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                foreach (var raiz in cena.GetRootGameObjects())
                    foreach (var comp in raiz.GetComponentsInChildren<Component>(true)) add(comp, Path.GetFileName(p));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        string pasta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Localizacao");
        Directory.CreateDirectory(pasta);
        string arq = Path.Combine(pasta, nomeArquivo);

        var sb = new StringBuilder();
        sb.AppendLine("PT;EN;Possivel dinamico;Ocorrencias;Onde");
        int din = 0;
        foreach (var k in ordem)
        {
            var it = itens[k];
            if (it.dinamico) din++;
            sb.AppendLine(string.Join(";", new[] { Csv(it.pt), "", it.dinamico ? "sim" : "", it.onde.Count.ToString(), Csv(string.Join(" | ", it.onde)) }));
        }
        File.WriteAllText(arq, sb.ToString(), new UTF8Encoding(true));

        Debug.Log("[Idioma] " + ordem.Count + " textos unicos (" + total + " ocorrencias, " + din + " possiveis dinamicos) em " + cenas.Count + " cena(s). Arquivo: " + arq);
        EditorUtility.RevealInFinder(arq);
    }

    static bool PastaIgnorada(string p)
    {
        foreach (var x in PastasIgnoradas) if (p.StartsWith(x)) return true;
        return false;
    }

    static bool PegarTexto(Object c, out string txt)
    {
        txt = null;
        if (c is TMPro.TMP_Text tmp) { txt = tmp.text; return true; }
        if (c is UnityEngine.UI.Text ui) { txt = ui.text; return true; }
        if (c is TextMesh tm) { txt = tm.text; return true; }
        return false;
    }

    // Textos que provavelmente sao trocados por script (vazios, so numeros, placeholders)
    static bool PareceDinamico(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return true;
        if (t.Contains("{0}")) return true;
        string l = t.Trim().ToLower();
        if (l == "teste" || l.StartsWith("new text") || l.StartsWith("titulo titulo") || l.StartsWith("descricao descricao")) return true;
        foreach (char ch in t) if (char.IsLetter(ch)) return false;
        return true;
    }

    static string Caminho(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static string Csv(string v)
    {
        v = v ?? "";
        if (v.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0) v = "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}