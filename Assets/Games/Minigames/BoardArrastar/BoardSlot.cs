using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Posicao de destino (target) na parte de cima do board.
/// </summary>
public class BoardSlot : MonoBehaviour
{
    [Tooltip("Modo Ordem Definida: id do item que deve ficar NESTE slot. VAZIO = target de erro: nao precisa ser preenchido e somente rejeita o item (volta para o inicio).")]
    public string idCorreto;

    [Tooltip("Ponto onde o item encaixa (vazio = o proprio slot).")]
    public Transform pontoEncaixe;

    [Header("Moldura (opcional)")]
    [Tooltip("Objeto da moldura deste target (ex: SpriteRenderer com IMG_Moldura).")]
    public GameObject moldura;
    [Tooltip("Esconde a moldura quando um item encaixa aqui.")]
    public bool esconderMolduraAoEncaixar = false;

    [Tooltip("Disparado quando um item certo e colocado aqui (nos dois modos).")]
    public UnityEvent OnItemCorreto;

    [HideInInspector] public BoardItem itemAtual;

    public Transform Encaixe => pontoEncaixe != null ? pontoEncaixe : transform;

    public void MostrarMoldura(bool vazio)
    {
        if (moldura == null) return;
        moldura.SetActive(vazio || !esconderMolduraAoEncaixar);
    }
}