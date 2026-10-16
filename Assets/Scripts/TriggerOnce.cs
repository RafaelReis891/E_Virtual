using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Versão do TriggerEvent que dispara só uma vez.
/// Classe separada para não conflitar com o TriggerEvent original.
/// </summary>
public class TriggerOnce : MonoBehaviour
{
    [Tooltip("Lista de tags que podem acionar este trigger. Se estiver vazia, qualquer tag é aceita.")]
    public string[] useTags;

    private HashSet<string> tagSet;
    private bool hasTriggered;

    public UnityEvent onTriggerOnce;

    private void Awake()
    {
        if (useTags != null && useTags.Length > 0)
            tagSet = new HashSet<string>(useTags);
        else
            tagSet = null;
    }

    private bool IsTagAllowed(Collider other)
    {
        if (tagSet == null || tagSet.Count == 0) return true;
        return tagSet.Contains(other.tag);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!IsTagAllowed(other)) return;

        hasTriggered = true;
        onTriggerOnce?.Invoke();
    }

    /// <summary>
    /// Reseta para poder disparar de novo.
    /// </summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}
