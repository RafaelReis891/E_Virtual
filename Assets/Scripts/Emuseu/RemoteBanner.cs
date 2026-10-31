using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Banner/telao/logo controlado pelo site. A "chave" (vazia = nome do GameObject) liga o objeto a um setting
// de tipo image do play-config. Baixa pelo CardRemoteImageCache e aplica por MaterialPropertyBlock
// (por renderer e por slot), sem mexer no material do projeto. A textura local fica como fallback.
// Admin (root): clique no banner pede upload ao site (frame-upload / requested).
[DisallowMultipleComponent]
public sealed class RemoteBanner : MonoBehaviour
{
    [Tooltip("Chave do setting no site. Vazia = usa o nome do GameObject.")]
    [SerializeField] string chave = "";

    [Tooltip("Slot do material a trocar. -1 = automatico.")]
    [SerializeField] int slot = -1;

    static readonly string[] NomesConhecidos = { "M_TelaoCircular", "M_SimpleScreen", "Telao_02", "ElasNoJogo_Opaco" };
    static readonly int IdBaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int IdMainTex = Shader.PropertyToID("_MainTex");
    static readonly int IdAlbedo = Shader.PropertyToID("_Albedo");        // SG_TelaoCircular (shader graph)
    static readonly int IdEmissionMap = Shader.PropertyToID("_EmissionMap");

    Renderer[] renderers;
    MaterialPropertyBlock mpb;
    Coroutine espera;
    string urlPedida;
    string urlAplicada;
    bool aplicado;

    public string Chave { get { return string.IsNullOrEmpty(chave) ? gameObject.name : chave; } }
    public string UrlAplicada { get { return urlAplicada; } }

    void Awake()
    {
        renderers = ResolverRenderers();
    }

    void OnEnable()
    {
        if (renderers == null) renderers = ResolverRenderers();
        EmuseuSettingsCatalog.Changed += AoMudarCatalogo;
        Atualizar();
    }

    void OnDisable()
    {
        EmuseuSettingsCatalog.Changed -= AoMudarCatalogo;
        if (espera != null) { StopCoroutine(espera); espera = null; }
        urlPedida = null; // permite pedir de novo ao reativar
    }

    void AoMudarCatalogo(string key)
    {
        if (key != null && key != Chave) return;
        Atualizar();
    }

    void Atualizar()
    {
        string url;
        if (EmuseuSettingsCatalog.TryGetImageUrl(Chave, out url))
        {
            if (url != urlPedida) Baixar(url);
        }
        else if (aplicado)
        {
            Reverter();
        }
        PrepararAdmin();
    }

    void Baixar(string url)
    {
        if (!CardRemoteImageCache.EhHttp(url))
        {
            EmuseuDebug.Warn("banner", Chave + ": url invalida (" + url + ")");
            return;
        }
        urlPedida = url;
        if (espera != null) StopCoroutine(espera);
        espera = StartCoroutine(EsperarTextura(url));
    }

    IEnumerator EsperarTextura(string url)
    {
        float inicio = Time.realtimeSinceStartup;
        float proximoPedido = 0f;
        while (Time.realtimeSinceStartup - inicio < 120f)
        {
            Texture2D tex;
            if (CardRemoteImageCache.TryGet(url, out tex))
            {
                Aplicar(tex, url);
                espera = null;
                yield break;
            }
            if (Time.realtimeSinceStartup >= proximoPedido)
            {
                CardRemoteImageCache.Pedir(url, Chave, true);
                proximoPedido = Time.realtimeSinceStartup + 3f;
            }
            yield return new WaitForSeconds(0.2f);
        }
        EmuseuDebug.Warn("banner", Chave + ": imagem nao chegou em 120s (" + url + "); mantendo textura local");
        espera = null;
    }

    void Aplicar(Texture2D tex, string url)
    {
        if (tex == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        int feitos = 0;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            int idx = ResolverSlot(r);
            if (idx < 0) { EmuseuDebug.Warn("banner", Chave + ": nenhum slot de material em " + r.name); continue; }

            Material[] mats = r.sharedMaterials;
            Material m = mats[idx];
            r.GetPropertyBlock(mpb, idx);
            mpb.SetTexture(IdBaseMap, tex);
            mpb.SetTexture(IdMainTex, tex);
            bool albedo = m != null && m.HasProperty(IdAlbedo);
            bool emissao = m != null && m.HasProperty(IdEmissionMap) && m.IsKeywordEnabled("_EMISSION");
            if (albedo) mpb.SetTexture(IdAlbedo, tex);
            if (emissao) mpb.SetTexture(IdEmissionMap, tex);
            r.SetPropertyBlock(mpb, idx);
            feitos++;
            EmuseuDebug.Info("banner", Chave + ": aplicado em " + r.name + " slot " + idx + " mat=" + (m != null ? m.name : "null") +
                             " shader=" + (m != null && m.shader != null ? m.shader.name : "?") +
                             (albedo ? " +_Albedo" : "") + (emissao ? " +_EmissionMap" : "") +
                             " tex=" + tex.width + "x" + tex.height);
        }
        if (feitos > 0) { aplicado = true; urlAplicada = url; }
    }

    void Reverter()
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            int idx = ResolverSlot(r);
            if (idx >= 0) r.SetPropertyBlock(null, idx);
        }
        aplicado = false;
        urlAplicada = null;
        urlPedida = null;
        EmuseuDebug.Info("banner", Chave + ": setting removido, voltou para a textura local");
    }

    Renderer[] ResolverRenderers()
    {
        Renderer own = GetComponent<Renderer>();
        if (own != null) return new[] { own };
        var lista = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r is MeshRenderer || r is SkinnedMeshRenderer) lista.Add(r);
        return lista.ToArray();
    }

    // slot >= 0 manual; senao: 1) material com "Banner" no nome, 2) materiais de telao/logo conhecidos,
    // 3) primeiro slot que nao seja M_LightGreen*.
    int ResolverSlot(Renderer r)
    {
        Material[] mats = r.sharedMaterials;
        if (mats == null || mats.Length == 0) return -1;
        if (slot >= 0)
        {
            if (slot < mats.Length) return slot;
            EmuseuDebug.Warn("banner", Chave + ": slot " + slot + " fora do alcance (" + mats.Length + " materiais em " + r.name + ")");
            return -1;
        }
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null && mats[i].name.IndexOf("Banner", StringComparison.OrdinalIgnoreCase) >= 0) return i;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) continue;
            foreach (string conhecido in NomesConhecidos)
                if (mats[i].name.IndexOf(conhecido, StringComparison.OrdinalIgnoreCase) >= 0) return i;
        }
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null && !mats[i].name.StartsWith("M_LightGreen", StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    // ---------- admin ----------

    void PrepararAdmin()
    {
        if (!EmuseuSettingsCatalog.IsAdmin) return;
        RemoteBannerClickDriver.Garantir();
        foreach (Renderer r in renderers)
        {
            if (r == null || r.GetComponent<Collider>() != null) continue;
            // Trigger + excludeLayers: nao bloqueia o jogador nem o teleporte (ver RemoteBannerSafeCollider).
            if (RemoteBannerSafeCollider.Adicionar(r) == null) continue;
            EmuseuDebug.Info("banner", Chave + ": collider adicionado em runtime (admin) em " + r.name);
        }
    }

    // Chamado pelo RemoteBannerClickDriver quando o admin clica neste banner.
    public void SolicitarUpload()
    {
        if (!EmuseuSettingsCatalog.IsAdmin) return;
        string k = Chave.Replace("\\", "\\\\").Replace("\"", "\\\"");
        EmuseuDebug.Info("banner", "upload solicitado: " + Chave);
        EmuseuBridge.SendToReact("frame-upload", "{\"frameId\":\"" + k + "\",\"phase\":\"requested\"}");
    }
}