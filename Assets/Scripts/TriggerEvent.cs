using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class ColliderUnityEvent : UnityEvent<Collider> { }

public class TriggerEvent : MonoBehaviour
{
    [Tooltip("Lista de tags que podem acionar este trigger. Se estiver vazia, qualquer tag é aceita.")]
    public string[] useTags;

    private HashSet<string> tagSet; // Estrutura rápida para busca

    public ColliderUnityEvent _OnTriggerEnter;
    public ColliderUnityEvent _OnTriggerStay;
    public ColliderUnityEvent _OnTriggerExit;

    private void Awake()
    {
        // Cria o HashSet apenas uma vez
        if (useTags != null && useTags.Length > 0)
            tagSet = new HashSet<string>(useTags);
        else
            tagSet = null; // Aceita qualquer tag
    }

    private bool IsTagAllowed(Collider other)
    {
        // Se não há tags definidas, aceita qualquer coisa
        if (tagSet == null || tagSet.Count == 0) return true;

        // Verifica em O(1)
        return tagSet.Contains(other.tag);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsTagAllowed(other))
            _OnTriggerEnter.Invoke(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsTagAllowed(other))
            _OnTriggerStay.Invoke(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsTagAllowed(other))
            _OnTriggerExit.Invoke(other);
    }
}
