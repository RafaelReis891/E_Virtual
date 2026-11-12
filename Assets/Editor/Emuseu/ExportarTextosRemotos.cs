using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Emuseu > Exportar textos remotos
// Acha todo RemoteTexto nas cenas do Build (habilitadas) e nos prefabs, SO LEITURA (nada e salvo),
// e grava Localizacao/textos_remotos_seed.json e textos_remotos_seed.csv para o upload inicial no admin do site.
// Cada linha: key, type "text", PT (texto local atual), EN (TextoIdioma.en), origem (cena/prefab) e caminho na hierarquia.
// Keys repetidas com textos diferentes ficam marcadas como conflito (resolver com keyOverride).
public static class ExportarTextosRemotos
{
    static readonly string[] PastasIgnoradas =
    {
        "Assets/TextMesh Pro", "Assets/Starter Assets", "Assets/TutorialInfo", "Assets/Plugins",
        "Assets/Stadium Creator 2", "Assets/_Backup_Deletados"
    };

    sealed class Ocorrencia
    {
        public string key, pt, en, origem, caminho;
    }

    sealed class ItemSeed
    {
        public string key;
        public string type = "text";
        public string text;
        public string textEn;
        public bool conflito;
        public List<string> textosDiferentes;
        public List<string> onde = new List<string>();
    }

    sealed class Seed
    {
        public string gerado;
        public int totalKeys;
        public int totalOcorrencias;
        public int conflitos;
        public List<ItemSeed> textos = new List<ItemSeed>();
    }

    [MenuItem("Tools/Emuseu/Exportar textos remotos")]
    static void Exportar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Exportar textos remotos", "Saia do Play Mode antes.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var ocorrencias = new List<Ocorrencia>();
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            // 1) Prefabs (leitura do asset, sem abrir nem salvar). Filtra pelo GUID do script para ser rapido.
            string guidScript = GuidDoScript();
            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < prefabs.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(prefabs[i]);
                if (Ignorada(p)) continue;
                if (i % 20 == 0) EditorUtility.DisplayProgressBar("Exportar textos remotos", p, (float)i / Math.Max(1, prefabs.Length) * 0.4f);
                if (!string.IsNullOrEmpty(guidScript) && !ArquivoContem(p, guidScript)) continue;
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (root == null) continue;
                foreach (RemoteTexto rt in root.GetComponentsInChildren<RemoteTexto>(true))
                    ocorrencias.Add(Ler(rt, Path.GetFileName(p)));
            }

            // 2) Cenas do Build: abre uma por vez em modo Single (sem salvar) e restaura o setup no fim.
            var cenas = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.enabled && !Ignorada(s.path)) cenas.Add(s.path);
            for (int i = 0; i < cenas.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Exportar textos remotos", cenas[i], 0.4f + (float)i / Math.Max(1, cenas.Count) * 0.6f);
                Scene cena = EditorSceneManager.OpenScene(cenas[i], OpenSceneMode.Single);
                foreach (GameObject r in cena.GetRootGameObjects())
                    foreach (RemoteTexto rt in r.GetComponentsInChildren<RemoteTexto>(true))
                        ocorrencias.Add(Ler(rt, Path.GetFileName(cenas[i])));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        Seed seed = Montar(ocorrencias);
        string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "../Localizacao"));
        Directory.CreateDirectory(pasta);
        string json = Path.Combine(pasta, "textos_remotos_seed.json");
        string csv = Path.Combine(pasta, "textos_remotos_seed.csv");
        File.WriteAllText(json, JsonConvert.SerializeObject(seed, Formatting.Indented), new UTF8Encoding(false));
        File.WriteAllText(csv, Csv(ocorrencias, seed), new UTF8Encoding(true)); // BOM: Excel abre os acentos certo

        string resumo = seed.totalKeys + " keys (" + seed.totalOcorrencias + " RemoteTexto), " + seed.conflitos + " keys com textos diferentes.\n" + json + "\n" + csv;
        Debug.Log("[ExportarTextosRemotos] " + resumo);
        if (seed.conflitos > 0)
            foreach (ItemSeed it in seed.textos)
                if (it.conflito) Debug.LogWarning("[ExportarTextosRemotos] CONFLITO key '" + it.key + "': " + string.Join(" | ", it.onde));
        EditorUtility.DisplayDialog("Exportar textos remotos", resumo, "OK");
        EditorUtility.RevealInFinder(json);
    }

    static Ocorrencia Ler(RemoteTexto rt, string origem)
    {
        return new Ocorrencia
        {
            key = rt.Key,
            pt = Norm(rt.TextoLocalPt),
            en = Norm(rt.TextoLocalEn),
            origem = origem,
            caminho = Caminho(rt.transform)
        };
    }

    static Seed Montar(List<Ocorrencia> ocorrencias)
    {
        var seed = new Seed { gerado = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), totalOcorrencias = ocorrencias.Count };
        var porKey = new Dictionary<string, ItemSeed>(StringComparer.Ordinal);
        foreach (Ocorrencia o in ocorrencias)
        {
            if (string.IsNullOrEmpty(o.key)) continue;
            ItemSeed it;
            if (!porKey.TryGetValue(o.key, out it))
            {
                it = new ItemSeed { key = o.key, text = o.pt, textEn = o.en };
                porKey[o.key] = it;
                seed.textos.Add(it);
            }
            else
            {
                if (string.IsNullOrEmpty(it.text)) it.text = o.pt;
                if (string.IsNullOrEmpty(it.textEn)) it.textEn = o.en;
                if (!string.IsNullOrEmpty(o.pt) && !string.IsNullOrEmpty(it.text) && o.pt != it.text)
                {
                    it.conflito = true;
                    if (it.textosDiferentes == null) it.textosDiferentes = new List<string> { it.text };
                    if (!it.textosDiferentes.Contains(o.pt)) it.textosDiferentes.Add(o.pt);
                }
            }
            it.onde.Add(o.origem + " > " + o.caminho);
        }
        seed.textos.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
        seed.totalKeys = seed.textos.Count;
        foreach (ItemSeed it in seed.textos) if (it.conflito) seed.conflitos++;
        return seed;
    }

    static string Csv(List<Ocorrencia> ocorrencias, Seed seed)
    {
        var conflito = new HashSet<string>(StringComparer.Ordinal);
        var contagem = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ItemSeed it in seed.textos) if (it.conflito) conflito.Add(it.key);
        foreach (Ocorrencia o in ocorrencias)
        {
            int n; contagem.TryGetValue(o.key ?? "", out n); contagem[o.key ?? ""] = n + 1;
        }
        var lista = new List<Ocorrencia>(ocorrencias);
        lista.Sort((a, b) => string.CompareOrdinal(a.key, b.key));

        var sb = new StringBuilder();
        sb.AppendLine("key;type;PT;EN;origem;caminho;repetida;conflito");
        foreach (Ocorrencia o in lista)
        {
            int n; contagem.TryGetValue(o.key ?? "", out n);
            sb.Append(Campo(o.key)).Append(';')
              .Append("text").Append(';')
              .Append(Campo(o.pt)).Append(';')
              .Append(Campo(o.en)).Append(';')
              .Append(Campo(o.origem)).Append(';')
              .Append(Campo(o.caminho)).Append(';')
              .Append(n > 1 ? "sim" : "").Append(';')
              .Append(conflito.Contains(o.key ?? "") ? "sim" : "")
              .AppendLine();
        }
        return sb.ToString();
    }

    static string Campo(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    static string Norm(string s)
    {
        return string.IsNullOrEmpty(s) ? "" : s.Replace("\r\n", "\n").Trim();
    }

    static string Caminho(Transform t)
    {
        var partes = new List<string>();
        for (Transform x = t; x != null; x = x.parent) partes.Insert(0, x.name);
        return string.Join("/", partes);
    }

    static bool Ignorada(string p)
    {
        foreach (string x in PastasIgnoradas) if (p.StartsWith(x, StringComparison.Ordinal)) return true;
        return p.Contains("ToBEdeleted");
    }

    static string GuidDoScript()
    {
        string[] achados = AssetDatabase.FindAssets("RemoteTexto t:MonoScript");
        foreach (string g in achados)
        {
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
            if (ms != null && ms.GetClass() == typeof(RemoteTexto)) return g;
        }
        return null;
    }

    // Prefab em texto (Force Text): procura o GUID do script. Prefab aninhado com RemoteTexto so dentro de outro
    // prefab nao tem o GUID no arquivo; por isso, na duvida (binario/erro), considera que contem.
    static bool ArquivoContem(string assetPath, string guid)
    {
        try
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            string t = File.ReadAllText(full);
            if (!t.StartsWith("%YAML", StringComparison.Ordinal)) return true;
            if (t.Contains(guid)) return true;
            // Pode ter prefab aninhado: tem instancia de outro prefab? entao carrega para conferir.
            return t.Contains("PrefabInstance:");
        }
        catch (Exception) { return true; }
    }
}