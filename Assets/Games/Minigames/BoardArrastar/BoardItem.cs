using UnityEngine;

/// <summary>
/// Objeto que o jogador arrasta (imagem, palavra, simbolo...).
/// Precisa de um Collider.
/// </summary>
public class BoardItem : MonoBehaviour
{
    [Tooltip("Identificador da resposta. Ex.: \"Flamengo\", \"Palavra1\".")]
    public string id;

    [HideInInspector] public Vector3 posicaoInicial;
    [HideInInspector] public Quaternion rotacaoInicial;
    [HideInInspector] public BoardSlot slotAtual;

    void Awake()
    {
        posicaoInicial = transform.position;
        rotacaoInicial = transform.rotation;
    }

    public void VoltarParaInicio()
    {
        if (slotAtual != null) { slotAtual.itemAtual = null; slotAtual.MostrarMoldura(true); }
        slotAtual = null;
        transform.SetPositionAndRotation(posicaoInicial, rotacaoInicial);
    }
}