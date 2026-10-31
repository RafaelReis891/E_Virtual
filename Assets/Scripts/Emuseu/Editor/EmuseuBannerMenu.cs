#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Menus de banners (chave do site = nome do GameObject, ^[a-z0-9_]+$):
// - Tools > Emuseu > Adicionar RemoteBanner nos selecionados (com Undo; salve a cena depois)
// - Tools > Emuseu > Listar chaves de banner da cena
// - Tools > Emuseu > Carregar Payload de Teste (Banners) (so em Play)
public static class EmuseuBannerMenu
{
    static readonly Regex ChaveValida = new Regex("^[a-z0-9_]+$");
    const string CaminhoTeste = "Scripts/Emuseu/Teste/payload_teste_banners.json";

    [MenuItem("Tools/Emuseu/Adicionar RemoteBanner nos selecionados")]
    static void Adicionar()
    {
        int add = 0, jaTinha = 0, semRenderer = 0, invalidos = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null || EditorUtility.IsPersistent(go)) continue; // so objetos de cena
            if (go.GetComponent<Renderer>() == null) { semRenderer++; continue; }
            if (go.GetComponent<RemoteBanner>() != null) { jaTinha++; continue; }
            Undo.AddComponent<RemoteBanner>(go);
            add++;
            if (!ChaveValida.IsMatch(go.name)) { invalidos++; Debug.LogWarning("[EmuseuBanner] nome fora do padrao ^[a-z0-9_]+$: '" + go.name + "' (" + Caminho(go) + ")", go); }
        }
        Debug.Log("[EmuseuBanner] RemoteBanner adicionado em " + add + " objeto(s); ja tinham: " + jaTinha + "; sem Renderer (ignorados): " + semRenderer + "; nomes invalidos: " + invalidos + ". Salve a cena (Ctrl+S).");
    }

    [MenuItem("Tools/Emuseu/Adicionar RemoteBanner nos selecionados", true)]
    static bool AdicionarValidar() { return Selection.gameObjects != null && Selection.gameObjects.Length > 0; }

    [MenuItem("Tools/Emuseu/Listar chaves de banner da cena")]
    static void Listar()
    {
        RemoteBanner[] todos = Object.FindObjectsByType<RemoteBanner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var porChave = new Dictionary<string, List<RemoteBanner>>();
        foreach (RemoteBanner b in todos)
        {
            List<RemoteBanner> lista;
            if (!porChave.TryGetValue(b.Chave, out lista)) { lista = new List<RemoteBanner>(); porChave[b.Chave] = lista; }
            lista.Add(b);
        }
        var chaves = new List<string>(porChave.Keys);
        chaves.Sort();

        int invalidas = 0, duplicadas = 0;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[EmuseuBanner] " + todos.Length + " RemoteBanner na cena, " + chaves.Count + " chaves:");
        foreach (string k in chaves)
        {
            List<RemoteBanner> l = porChave[k];
            sb.AppendLine("  " + k + (l.Count > 1 ? "   (x" + l.Count + ")" : "") + "   <- " + Caminho(l[0].gameObject));
        }
        Debug.Log(sb.ToString());

        foreach (string k in chaves)
        {
            if (!ChaveValida.IsMatch(k)) { invalidas++; Debug.LogWarning("[EmuseuBanner] chave invalida (use ^[a-z0-9_]+$): '" + k + "' em " + Caminho(porChave[k][0].gameObject), porChave[k][0].gameObject); }
            if (porChave[k].Count > 1)
            {
                duplicadas++;
                var onde = new List<string>();
                foreach (RemoteBanner b in porChave[k]) onde.Add(Caminho(b.gameObject));
                Debug.LogWarning("[EmuseuBanner] chave duplicada '" + k + "' em " + porChave[k].Count + " objetos:\n  " + string.Join("\n  ", onde), porChave[k][0].gameObject);
            }
        }
        Debug.Log("[EmuseuBanner] resumo: " + invalidas + " chave(s) invalida(s), " + duplicadas + " duplicada(s).");
    }

    [MenuItem("Tools/Emuseu/Carregar Payload de Teste (Banners)")]
    static void CarregarTeste()
    {
        string arquivo = Path.Combine(Application.dataPath, CaminhoTeste);
        if (!File.Exists(arquivo)) { Debug.LogWarning("[EmuseuBanner] arquivo nao encontrado: " + arquivo); return; }
        if (EmuseuBridge.Instance == null) { Debug.LogWarning("[EmuseuBanner] EmuseuBridge nao existe (entre em Play)."); return; }
        EmuseuBridge.Instance.OnPlayConfig(File.ReadAllText(arquivo));
    }

    [MenuItem("Tools/Emuseu/Carregar Payload de Teste (Banners)", true)]
    static bool CarregarTesteValidar() { return Application.isPlaying; }

    static string Caminho(GameObject go)
    {
        string p = go.name;
        Transform t = go.transform.parent;
        while (t != null) { p = t.name + "/" + p; t = t.parent; }
        return p;
    }
}
#endif