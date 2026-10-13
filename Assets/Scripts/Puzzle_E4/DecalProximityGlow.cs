using UnityEngine;

/// <summary>
/// Coloque no objeto pai dos decals (com Collider trigger).
/// Arraste os GameObjects dos Quads/decals no array.
/// Quando o player entra no trigger, os decals brilham.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DecalProximityGlow : MonoBehaviour
{
    [Header("Decals (arraste os GameObjects dos Quads)")]
    public GameObject[] decalObjects;

    [Header("Cores")]
    public Color offColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    public Color glowColor = new Color(1f, 0.85f, 0.4f, 1f);
    public float glowIntensity = 2.5f;

    [Header("Detecção")]
    public string playerTag = "Player";

    Renderer[] renderers;
    MaterialPropertyBlock propBlock;
    float currentIntensity;

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        // Pega o Renderer de cada GameObject arrastado
        if (decalObjects != null && decalObjects.Length > 0)
        {
            renderers = new Renderer[decalObjects.Length];
            for (int i = 0; i < decalObjects.Length; i++)
            {
                if (decalObjects[i] != null)
                    renderers[i] = decalObjects[i].GetComponent<Renderer>();
            }
        }
    }

    void Update()
    {
        ApplyEmission(currentIntensity);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
            currentIntensity = glowIntensity;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
            currentIntensity = 0f;
    }

    void ApplyEmission(float intensity)
    {
        if (renderers == null) return;

        Color emissive = glowColor * intensity;
        float t = glowIntensity > 0 ? intensity / glowIntensity : 0f;

        foreach (var r in renderers)
        {
            if (!r) continue;
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor("_EmissionColor", emissive);
            propBlock.SetColor("_BaseColor", Color.Lerp(offColor, glowColor, t));
            r.SetPropertyBlock(propBlock);
        }
    }
}