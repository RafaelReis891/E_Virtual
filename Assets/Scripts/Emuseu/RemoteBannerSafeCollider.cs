using UnityEngine;

// Collider "so para raycast" que o admin (root) usa para clicar em banners.
// IMPORTANTE: nunca pode bloquear o jogador nem disparar OnTriggerEnter/Exit de outros scripts.
//  - isTrigger: CharacterController nao colide com triggers.
//  - excludeLayers = tudo: nenhum corpo/trigger gera contato com ele (Unity 2022.2+). Raycast/overlap NAO sao afetados
//    por layer overrides, entao o clique continua acertando (o driver usa QueryTriggerInteraction.Collide).
//  - sempre BoxCollider pelo bounds do mesh: no build os FBX nao sao legiveis (isReadable=false), e no Editor
//    Mesh.isReadable e sempre true; usar a mesma forma nos dois evita comportamento diferente entre Editor e WebGL.
public static class RemoteBannerSafeCollider
{
    public static Collider Adicionar(Renderer r)
    {
        if (r == null || r.GetComponent<Collider>() != null) return null;

        Mesh mesh = null;
        var mf = r.GetComponent<MeshFilter>();
        if (mf != null) mesh = mf.sharedMesh;
        else if (r is SkinnedMeshRenderer) mesh = ((SkinnedMeshRenderer)r).sharedMesh;
        if (mesh == null) return null;

        var bc = r.gameObject.AddComponent<BoxCollider>();
        bc.center = mesh.bounds.center;
        bc.size = mesh.bounds.size;
        bc.isTrigger = true;
        bc.excludeLayers = ~0;
        return bc;
    }
}