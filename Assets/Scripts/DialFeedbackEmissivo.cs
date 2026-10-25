using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Feedback emissivo para discos de cadeado: depois de X segundos parado,
/// numero errado pisca vermelho e numero certo fica verde.
/// Usado pelo LockDoor e pelo Enigma5Manager (o CadeadoDials tem o seu proprio).
/// Os discos devem usar um material com Emission Map (ex.: Numeros_Emissivo).
/// </summary>
public class DialFeedbackEmissivo : MonoBehaviour
{
    [Tooltip("Liga o brilho vermelho/verde nos numeros.")]
    public bool usarFeedbackEmissivo = true;
    [Tooltip("Segundos parado no numero antes de mostrar a cor.")]
    [Min(0f)] public float tempoParado = 1.2f;
    [ColorUsage(false, true)] public Color corErrado = new Color(4f, 0.15f, 0.15f);
    [ColorUsage(false, true)] public Color corCorreto = new Color(0.2f, 4f, 0.3f);
    [Tooltip("Quantas vezes pisca no numero errado.")]
    [Min(1)] public int piscadasErrado = 3;
    [Tooltip("Duracao de cada piscada (aceso + apagado).")]
    [Min(0.05f)] public float duracaoPiscada = 0.3f;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;
    readonly Dictionary<Transform, Coroutine> rotinas = new Dictionary<Transform, Coroutine>();

    /// <summary>Chame depois de girar o disco.</summary>
    public void Notificar(Transform disco, bool correto)
    {
        if (!usarFeedbackEmissivo || disco == null) return;
        Parar(disco);
        SetEmissao(disco, Color.black);
        rotinas[disco] = StartCoroutine(Rotina(disco, correto));
    }

    /// <summary>Acende verde na hora (ex.: puzzle resolvido).</summary>
    public void AcenderCorreto(Transform disco)
    {
        if (!usarFeedbackEmissivo || disco == null) return;
        Parar(disco);
        SetEmissao(disco, corCorreto);
    }

    void Parar(Transform disco)
    {
        if (rotinas.TryGetValue(disco, out var c) && c != null) StopCoroutine(c);
        rotinas.Remove(disco);
    }

    IEnumerator Rotina(Transform disco, bool correto)
    {
        yield return new WaitForSeconds(tempoParado);
        if (correto) { SetEmissao(disco, corCorreto); yield break; }
        for (int i = 0; i < piscadasErrado; i++)
        {
            SetEmissao(disco, corErrado);
            yield return new WaitForSeconds(duracaoPiscada * 0.5f);
            SetEmissao(disco, Color.black);
            yield return new WaitForSeconds(duracaoPiscada * 0.5f);
        }
        rotinas.Remove(disco);
    }

    void SetEmissao(Transform disco, Color cor)
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (var r in disco.GetComponentsInChildren<Renderer>())
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && !m.IsKeywordEnabled("_EMISSION")) { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorId, cor);
            r.SetPropertyBlock(mpb);
        }
    }
}