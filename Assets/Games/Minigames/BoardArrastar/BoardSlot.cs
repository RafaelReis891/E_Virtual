using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Posicao de destino (target) na parte de cima do board.
/// </summary>
public class BoardSlot : MonoBehaviour
{
    [Tooltip("Modo Ordem Definida: id do item que deve ficar NESTE slot.")]
    public string idCorreto;

    [Tooltip("Ponto onde o item encaixa (vazio = o proprio slot).")]
    public Transform pontoEncaixe;

    [Tooltip("Disparado quando o item certo e colocado aqui (so no modo Ordem Definida).")]
    public UnityEvent OnItemCorreto;

    [HideInInspector] public BoardItem itemAtual;

    public Transform Encaixe => pontoEncaixe != null ? pontoEncaixe : transform;
}