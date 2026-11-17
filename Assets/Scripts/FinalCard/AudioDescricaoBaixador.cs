using System.Collections;
using UnityEngine;

// Ajudante do AudioDescricaoFinalCard (criado sozinho, fica entre cenas):
// - roda os downloads dos mp3 (o board pode ser desligado no meio);
// - a cada 2s poe o AudioDescricaoFinalCard em boards criados depois (Instantiate).
public sealed class AudioDescricaoBaixador : MonoBehaviour
{
    static AudioDescricaoBaixador inst;
    float t;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { inst = null; }

    public static void Garantir()
    {
        if (inst != null) return;
        var go = new GameObject("AudioDescricaoBaixador");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        inst = go.AddComponent<AudioDescricaoBaixador>();
    }

    public static void Iniciar(IEnumerator rotina)
    {
        Garantir();
        inst.StartCoroutine(rotina);
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (t < 2f) return;
        t = 0f;
        AudioDescricaoFinalCard.GarantirTodos();
    }
}
