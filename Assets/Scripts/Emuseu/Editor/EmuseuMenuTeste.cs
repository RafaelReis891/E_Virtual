#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Menu de teste: Tools > Emuseu > Carregar Payload de Teste
// Le Assets/Scripts/Emuseu/Teste/payload_teste.json e manda para EmuseuBridge.OnPlayConfig (so em Play).
public static class EmuseuMenuTeste
{
    const string Caminho = "Scripts/Emuseu/Teste/payload_teste.json";

    [MenuItem("Tools/Emuseu/Carregar Payload de Teste")]
    static void Carregar()
    {
        string arquivo = Path.Combine(Application.dataPath, Caminho);
        if (!File.Exists(arquivo)) { Debug.LogWarning("[EmuseuTeste] arquivo nao encontrado: " + arquivo); return; }
        if (EmuseuBridge.Instance == null) { Debug.LogWarning("[EmuseuTeste] EmuseuBridge nao existe (entre em Play)."); return; }
        EmuseuBridge.Instance.OnPlayConfig(File.ReadAllText(arquivo));
    }

    // So habilita o menu em Play mode.
    [MenuItem("Tools/Emuseu/Carregar Payload de Teste", true)]
    static bool CarregarValidar() { return Application.isPlaying; }
}
#endif