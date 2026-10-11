using UnityEngine;

public class TextureOffset : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;

    [Header("Optional Shared Material")]
    public Material useMaterial;

    [Header("Texture Settings")]
    public string textureProperty = "_BaseMap";

    [Header("Offset Control")]
    public Vector2 direction = Vector2.right;
    public float speed = 1f;

    private Vector2 currentOffset;
    private Vector2 originalTiling;

    void Awake()
    {
        if(!meshRenderer)
            meshRenderer = GetComponent<MeshRenderer>();
        propBlock = new MaterialPropertyBlock();

        Material source = useMaterial != null
            ? useMaterial
            : meshRenderer.sharedMaterial;

        if (source != null)
            originalTiling = source.GetTextureScale(textureProperty);
        else
            originalTiling = Vector2.one;
    }

    void Update()
    {
        currentOffset += direction.normalized * speed * Time.deltaTime;

        if (useMaterial == null)
        {
            meshRenderer.GetPropertyBlock(propBlock);

            propBlock.SetVector(
                textureProperty + "_ST",
                new Vector4(
                    originalTiling.x,
                    originalTiling.y,
                    currentOffset.x,
                    currentOffset.y));

            meshRenderer.SetPropertyBlock(propBlock);
        }
        else
        {
            useMaterial.SetTextureOffset(textureProperty, currentOffset);
        }
    }
}