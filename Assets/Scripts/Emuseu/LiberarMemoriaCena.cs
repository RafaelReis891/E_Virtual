using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Libera memoria ao trocar de cena (iPhone/Safari tem pouca memoria no WebGL).
// Cria-se sozinho (RuntimeInitializeOnLoadMethod), sem Inspector, sem editar cena/prefab.
//
// Quando uma cena e descarregada (SceneManager.sceneUnloaded), espera a cena nova carregar e rodar
// alguns frames (Awake/OnEnable/Start ja pediram o que precisam) e entao:
//  1) CardRemoteImageCache: destroi as imagens de cards/banners do site que a cena nova NAO usou;
//  2) RemoteAudioCatalog: destroi os audios do site (musica, vozes) que a cena nova NAO pediu;
//  3) Resources.UnloadUnusedAssets() + GC.Collect().
// Seguranca:
//  - "usado" = pedido/consultado no cache a partir do frame em que a cena antiga saiu (a cena nova sempre conta);
//  - alem disso, nunca destroi textura que ainda esta num Renderer/RawImage vivo, nem clip que esta em
//    algum AudioSource vivo ou no RemoteVoiceClip (inclui objetos DontDestroyOnLoad e desligados);
//  - textos e JSON do site (payload, catalogos) NAO sao mexidos; so texturas e audios pesados.
//  - downloads em andamento continuam; quem pediu ja checa se ainda existe (this == null).
// Se a cena nova pedir algo que foi liberado, o cache baixa de novo (rapido).
public sealed class LiberarMemoriaCena : MonoBehaviour
{
    public static bool Ativo = true;
    const int FramesDeEspera = 3;   // frames depois do sceneLoaded (Start dos objetos da cena nova ja rodou)

    static LiberarMemoriaCena inst;
    static int desdeFrame = -1;     // frame em que a cena antiga saiu (-1 = nada pendente)
    static int rodarNoFrame = -1;
    static bool rodando;

    static readonly int[] IdsTextura =
    {
        Shader.PropertyToID("_BaseMap"), Shader.PropertyToID("_MainTex"),
        Shader.PropertyToID("_EmissionMap"), Shader.PropertyToID("_Albedo")
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar()
    {
        inst = null;
        desdeFrame = -1;
        rodarNoFrame = -1;
        rodando = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar()
    {
        SceneManager.sceneUnloaded -= AoDescarregar;
        SceneManager.sceneUnloaded += AoDescarregar;
        SceneManager.sceneLoaded -= AoCarregar;
        SceneManager.sceneLoaded += AoCarregar;
    }

    static void Garantir()
    {
        if (inst != null) return;
        var go = new GameObject("LiberarMemoriaCena");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        inst = go.AddComponent<LiberarMemoriaCena>();
    }

    static void AoDescarregar(Scene cena)
    {
        if (!Ativo || !Application.isPlaying) return;
        if (!cena.IsValid() || string.IsNullOrEmpty(cena.name)) return;
        int f = Time.frameCount;
        if (desdeFrame < 0 || f < desdeFrame) desdeFrame = f;
        Agendar();
    }

    static void AoCarregar(Scene cena, LoadSceneMode modo)
    {
        if (desdeFrame >= 0) Agendar();   // cena nova chegou: espera ela acordar antes de liberar
    }

    static void Agendar()
    {
        rodarNoFrame = Time.frameCount + FramesDeEspera;
        Garantir();
    }

    void Update()
    {
        if (desdeFrame < 0 || rodando || Time.frameCount < rodarNoFrame) return;
        int desde = desdeFrame;
        desdeFrame = -1;
        StartCoroutine(Liberar(desde));
    }

    IEnumerator Liberar(int desde)
    {
        rodando = true;
        float t0 = Time.realtimeSinceStartup;
        int nTex = 0, nAudio = 0;
        long bytesTex = 0, bytesAudio = 0;
        try
        {
            var texCache = new HashSet<Texture>();
            CardRemoteImageCache.CopiarTexturas(texCache);
            if (texCache.Count > 0)
                nTex = CardRemoteImageCache.LiberarNaoUsadas(desde, TexturasEmUso(texCache), out bytesTex);

            var clipCache = new HashSet<AudioClip>();
            RemoteAudioCatalog.CopiarClips(clipCache);
            if (clipCache.Count > 0)
                nAudio = RemoteAudioCatalog.LiberarNaoUsados(desde, ClipsEmUso(clipCache), out bytesAudio);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LiberarMemoriaCena] falha ao liberar caches: " + ex.GetType().Name + ": " + ex.Message);
        }

        yield return null;   // Destroy() vale no fim do frame
        AsyncOperation op = null;
        try { op = Resources.UnloadUnusedAssets(); }
        catch (Exception ex) { Debug.LogWarning("[LiberarMemoriaCena] UnloadUnusedAssets falhou: " + ex.Message); }
        if (op != null) yield return op;
        GC.Collect();

        if (Debug.isDebugBuild)
        {
            Debug.Log("[LiberarMemoriaCena] cena '" + SceneManager.GetActiveScene().name + "': liberadas " + nTex + " texturas (~" + Mb(bytesTex) +
                      ") e " + nAudio + " audios (~" + Mb(bytesAudio) + ") do site; no cache agora: " + CardRemoteImageCache.Count +
                      " texturas; UnloadUnusedAssets+GC em " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms");
        }
        rodando = false;
        // Outra troca de cena durante a liberacao: o Update roda de novo.
    }

    static string Mb(long b)
    {
        return b > 0 ? (b / (1024f * 1024f)).ToString("0.0") + " MB" : "? MB";
    }

    // Texturas do cache que ainda estao aplicadas em algum Renderer (material ou MaterialPropertyBlock) ou RawImage vivo.
    static HashSet<Texture> TexturasEmUso(HashSet<Texture> candidatas)
    {
        var emUso = new HashSet<Texture>();
        var mpb = new MaterialPropertyBlock();
        var mats = new List<Material>();
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (r == null) continue;
            try
            {
                r.GetSharedMaterials(mats);
                for (int i = 0; i < mats.Count; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    foreach (int id in IdsTextura)
                    {
                        if (!m.HasProperty(id)) continue;
                        Texture t = m.GetTexture(id);
                        if (t != null && candidatas.Contains(t)) emUso.Add(t);
                    }
                }
                if (r.HasPropertyBlock())
                {
                    Checar(r, -1, mpb, candidatas, emUso);
                    for (int i = 0; i < mats.Count; i++) Checar(r, i, mpb, candidatas, emUso);
                }
            }
            catch (Exception) { }
        }
        foreach (RawImage ri in FindObjectsByType<RawImage>(FindObjectsInactive.Include))
        {
            if (ri == null) continue;
            Texture t = ri.texture;
            if (t != null && candidatas.Contains(t)) emUso.Add(t);
        }
        return emUso;
    }

    static void Checar(Renderer r, int slot, MaterialPropertyBlock mpb, HashSet<Texture> candidatas, HashSet<Texture> emUso)
    {
        mpb.Clear();
        if (slot < 0) r.GetPropertyBlock(mpb); else r.GetPropertyBlock(mpb, slot);
        if (mpb.isEmpty) return;
        foreach (int id in IdsTextura)
        {
            if (!mpb.HasTexture(id)) continue;
            Texture t = mpb.GetTexture(id);
            if (t != null && candidatas.Contains(t)) emUso.Add(t);
        }
    }

    // Clips do cache que ainda estao em algum AudioSource vivo ou guardados por um RemoteVoiceClip.
    static HashSet<AudioClip> ClipsEmUso(HashSet<AudioClip> candidatos)
    {
        var emUso = new HashSet<AudioClip>();
        foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (s == null) continue;
            AudioClip c = s.clip;
            if (c != null && candidatos.Contains(c)) emUso.Add(c);
        }
        foreach (RemoteVoiceClip v in FindObjectsByType<RemoteVoiceClip>(FindObjectsInactive.Include))
        {
            if (v == null) continue;
            AudioClip c = v.ClipEmUso;
            if (c != null && candidatos.Contains(c)) emUso.Add(c);
        }
        return emUso;
    }
}
