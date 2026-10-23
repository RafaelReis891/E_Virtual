using UnityEngine;

// Troca Base Map / Emission Map de UM material da mesh so neste objeto,
// sem alterar o asset do material (as outras cenas continuam iguais).
// Usa MaterialPropertyBlock: nao cria copias de material.
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class BoardMaterialOverride : MonoBehaviour
{
    [Tooltip("Indice do material no Mesh Renderer (0 = Board, 1 = Field, 2 = Neon)")]
    public int indiceMaterial = 1;

    [Header("Base Map")]
    public Texture baseMap;
    public bool trocarCorBase;
    public Color corBase = Color.white;

    [Header("Emission Map")]
    public Texture emissionMap;
    public bool trocarCorEmissao;
    [ColorUsage(true, true)] public Color corEmissao = Color.white;

    static readonly int ID_BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int ID_EmissionMap = Shader.PropertyToID("_EmissionMap");
    static readonly int ID_EmissionColor = Shader.PropertyToID("_EmissionColor");

    Renderer rend;
    MaterialPropertyBlock bloco;

    void OnEnable() { Aplicar(); }
    void OnValidate() { Aplicar(); }

    public void Aplicar()
    {
        if (rend == null) rend = GetComponent<Renderer>();
        if (bloco == null) bloco = new MaterialPropertyBlock();
        if (indiceMaterial < 0 || indiceMaterial >= rend.sharedMaterials.Length) return;

        rend.GetPropertyBlock(bloco, indiceMaterial);
        if (baseMap != null) bloco.SetTexture(ID_BaseMap, baseMap);
        if (trocarCorBase) bloco.SetColor(ID_BaseColor, corBase);
        if (emissionMap != null) bloco.SetTexture(ID_EmissionMap, emissionMap);
        if (trocarCorEmissao) bloco.SetColor(ID_EmissionColor, corEmissao);
        rend.SetPropertyBlock(bloco, indiceMaterial);
    }

    // Para trocar por codigo/eventos em runtime
    public void SetBaseMap(Texture t) { baseMap = t; Aplicar(); }
    public void SetEmissionMap(Texture t) { emissionMap = t; Aplicar(); }

    public void Limpar()
    {
        if (rend == null) rend = GetComponent<Renderer>();
        rend.SetPropertyBlock(null, indiceMaterial);
    }
}
