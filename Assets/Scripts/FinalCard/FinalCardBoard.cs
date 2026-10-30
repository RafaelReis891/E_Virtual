using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.Video;

// Final Card: ao terminar o puzzle, chame Mostrar("F11").
// Carrega titulo, descricao e imagem (ou video) do cards.json.
// Botao de idioma: chame AlternarIdioma() (texto alterna ENG / BR).
public class FinalCardBoard : MonoBehaviour
{
    [Header("Dados")]
    public TextAsset cardsJson;
    public string idInicial;              // opcional: mostra ao iniciar

    [Header("Textos (TextMeshPro)")]
    public TMPro.TMP_Text titulo;
    public TMPro.TMP_Text descricao;
    public TMPro.TMP_Text textoBotaoIdioma;

    [Header("Imagem no board (mesh)")]
    public Renderer rendererImagem;       // ex: Board_Puzzle
    public int indiceMaterial = 1;        // material que recebe a foto
    public bool usarTambemNaEmissao = false;

    [Header("Imagem em UI (opcional)")]
    public UnityEngine.UI.RawImage rawImage;

    [Header("Video (opcional)")]
    public VideoPlayer videoPlayer;
    public bool videoPorAproximacao = true;
    public Transform jogador;             // vazio = Camera.main
    public float distanciaVideo = 4f;
    public int resolucaoVideo = 1024;

    [Header("Audio do video")]
    public bool semSom = false;           // ligado = video sempre mudo
    [Range(0f, 1f)] public float volumeVideo = 0.25f;
    public bool somSomentePerto = true;   // som liga perto do board e desliga longe

    [Header("Eventos")]
    public UnityEvent OnCardCarregado;

    [Header("Debug")]
    public bool mostrarDebug;

    FinalCardAlbum album;
    FinalCardData atual;
    string idUltimo;
    bool ingles;
    Texture imagemAtual;
    RenderTexture rtVideo;
    bool temVideo;
    MaterialPropertyBlock bloco;

    static readonly int ID_BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int ID_EmissionMap = Shader.PropertyToID("_EmissionMap");

    void Awake()
    {
        CarregarJson();
        AtualizarBotao();
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(idInicial)) Mostrar(idInicial);
    }

    // Se os dados do site chegarem depois do Mostrar, recarrega o card atual.
    void OnEnable() { CardRemoteCatalog.Changed += OnCatalogoRemotoMudou; if (atual != null) OnCatalogoRemotoMudou(); } // reaplica se o site chegou enquanto o board estava desligado
    void OnDisable() { CardRemoteCatalog.Changed -= OnCatalogoRemotoMudou; }

    void OnCatalogoRemotoMudou()
    {
        if (string.IsNullOrEmpty(idUltimo) || !CardRemoteCatalog.TryGet(idUltimo, out _)) return;
        StopAllCoroutines();
        Mostrar(idUltimo);
    }

    void CarregarJson()
    {
        if (cardsJson == null) { Debug.LogWarning("[FinalCard] cardsJson vazio", this); return; }
        album = JsonUtility.FromJson<FinalCardAlbum>(cardsJson.text);
        Log("json carregado: " + (album.cards != null ? album.cards.Count : 0) + " cards");
    }

    // ---------- API publica ----------

    public void Mostrar(string id)
    {
        if (album == null) CarregarJson();
        idUltimo = id;
        atual = album != null ? album.Buscar(id) : null;
        atual = CardRemoteCatalog.AplicarSobre(id, atual); // site (se tiver) por cima do json local, em copia
        if (atual == null) { EmuseuDebug.Warn("card", "id nao encontrado (nem local nem remoto): " + id + " board=" + name); Debug.LogWarning("[FinalCard] id nao encontrado: " + id, this); return; }
        Log("mostrando " + id);
        EmuseuDebug.CardMostrado(id, CardRemoteCatalog.TryGet(id, out _), atual.imagePath, atual.video, name);
        AtualizarTextos();
        PararVideo();
        temVideo = !string.IsNullOrEmpty(atual.video) && videoPlayer != null;
        if (temVideo) PrepararVideo(atual.video);
        else StartCoroutine(CarregarImagem(atual.imagePath));
    }

    public void AlternarIdioma()
    {
        ingles = !ingles;
        AtualizarTextos();
        AtualizarBotao();
        Log("idioma: " + (ingles ? "EN" : "BR"));
    }

    public void SetIngles(bool valor) { ingles = valor; AtualizarTextos(); AtualizarBotao(); }

    // ---------- Textos ----------

    void AtualizarTextos()
    {
        if (atual == null) return;
        string t = ingles && !string.IsNullOrEmpty(atual.titleEN) ? atual.titleEN : atual.title;
        string d = ingles && !string.IsNullOrEmpty(atual.descriptionEN) ? atual.descriptionEN : atual.description;
        if (ingles && string.IsNullOrEmpty(atual.titleEN)) Log("card " + atual.id + " sem titleEN, usando PT");
        if (titulo != null) titulo.text = t;
        if (descricao != null) descricao.text = d;
    }

    void AtualizarBotao()
    {
        // Mostra o idioma para o qual o botao vai mudar.
        if (textoBotaoIdioma != null) textoBotaoIdioma.text = ingles ? "BR" : "ENG";
    }

    // ---------- Imagem ----------

    string CaminhoStreaming(string p)
    {
        if (string.IsNullOrEmpty(p)) return null;
        if (p.StartsWith("http://") || p.StartsWith("https://")) return p; // link direto da internet
        p = p.Replace("\\", "/");
        if (p.StartsWith("StreamingAssets/")) p = p.Substring("StreamingAssets/".Length);
        string baseP = Application.streamingAssetsPath;
        // WebGL/Android ja e URL; no editor/PC vira file://
        if (baseP.Contains("://")) return baseP + "/" + p;
        return "file://" + Path.Combine(baseP, p).Replace("\\", "/");
    }

    IEnumerator CarregarImagem(string caminho)
    {
        string url = CaminhoStreaming(caminho);
        if (url == null) { EmuseuDebug.ImagemSemUrl(idUltimo, name); yield break; }
        Log("carregando imagem " + url);
        EmuseuDebug.ImagemInicio(idUltimo, url, name);
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                EmuseuDebug.ImagemResultado(idUltimo, url, false, 0, 0, req.downloadedBytes, req.error, req.responseCode);
                Debug.LogWarning("[FinalCard] falha ao carregar " + url + " : " + req.error, this);
                yield break;
            }
            if (imagemAtual != null && imagemAtual is Texture2D) Destroy(imagemAtual);
            imagemAtual = DownloadHandlerTexture.GetContent(req);
            EmuseuDebug.ImagemResultado(idUltimo, url, true, imagemAtual != null ? imagemAtual.width : 0, imagemAtual != null ? imagemAtual.height : 0, req.downloadedBytes, null, req.responseCode);
            AplicarTextura(imagemAtual);
            OnCardCarregado.Invoke();
        }
    }

    void AplicarTextura(Texture tex)
    {
        if (rendererImagem != null)
        {
            // Usa uma copia do material so deste objeto (o asset nao muda).
            // MaterialPropertyBlock nao funciona com o GPU Resident Drawer do Unity 6.
            var mats = rendererImagem.materials;
            if (indiceMaterial >= 0 && indiceMaterial < mats.Length)
            {
                var m = mats[indiceMaterial];
                if (m.HasProperty(ID_BaseMap)) m.SetTexture(ID_BaseMap, tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                if (usarTambemNaEmissao && m.HasProperty(ID_EmissionMap)) m.SetTexture(ID_EmissionMap, tex);
                Log("textura aplicada em " + rendererImagem.name + " (" + tex.width + "x" + tex.height + ")");
            }
        }
        if (rawImage != null) rawImage.texture = tex;
    }

    // ---------- Video ----------

    void PrepararVideo(string caminho)
    {
        if (rtVideo == null)
        {
            rtVideo = new RenderTexture(resolucaoVideo, resolucaoVideo * 9 / 16, 0);
            rtVideo.Create();
        }
        if (!videoPlayer.enabled) { videoPlayer.enabled = true; Log("VideoPlayer estava desativado, ativei"); }
        if (!videoPlayer.gameObject.activeInHierarchy)
        {
            EmuseuDebug.VideoFallback(idUltimo, null, "objeto do VideoPlayer inativo (" + videoPlayer.name + ")");
            Debug.LogWarning("[FinalCard] o objeto do VideoPlayer (" + videoPlayer.name + ") esta inativo; mostrando imagem", this);
            temVideo = false;
            StartCoroutine(CarregarImagem(atual.imagePath));
            return;
        }
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = CaminhoStreaming(caminho);
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = rtVideo;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside; // video vertical ganha faixas pretas, sem esticar
        videoPlayer.errorReceived -= OnErroVideo;
        videoPlayer.errorReceived += OnErroVideo;
        videoPlayer.isLooping = true;
        videoPlayer.audioOutputMode = semSom ? VideoAudioOutputMode.None : VideoAudioOutputMode.Direct;
        videoPlayer.EnableAudioTrack(0, true);
        AplicarVolume();
        if (somSomentePerto) DesligarSom();
        videoPlayer.Prepare();
        AplicarTextura(rtVideo);
        Log("video preparado " + videoPlayer.url);
        EmuseuDebug.VideoPreparar(videoPlayer, idUltimo, videoPlayer.url);
        if (!videoPorAproximacao) videoPlayer.Play();
        OnCardCarregado.Invoke();
    }

    void OnErroVideo(VideoPlayer vp, string msg)
    {
        EmuseuDebug.VideoFallback(idUltimo, vp.url, msg);
        Debug.LogWarning("[FinalCard] erro no video " + vp.url + " : " + msg + " (mostrando imagem)", this);
        temVideo = false;
        if (atual != null) StartCoroutine(CarregarImagem(atual.imagePath));
    }

    void PararVideo()
    {
        if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
    }

    void Update()
    {
        if (!temVideo || !videoPorAproximacao || videoPlayer == null || !videoPlayer.isActiveAndEnabled) return;
        Transform alvo = jogador;
        if (alvo == null && Camera.main != null) { jogador = Camera.main.transform; alvo = jogador; }
        if (alvo == null) return;
        bool perto = Vector3.Distance(alvo.position, transform.position) <= distanciaVideo;
        if (somSomentePerto) { if (perto) LigarSom(); else DesligarSom(); }
        if (perto && !videoPlayer.isPlaying) { videoPlayer.Play(); Log("video play (perto)"); }
        else if (!perto && videoPlayer.isPlaying) { videoPlayer.Pause(); Log("video pause (longe)"); }
    }

    // ---------- Audio (pode chamar por UnityEvent) ----------

    bool somLigado = true;

    public void LigarSom()
    {
        if (videoPlayer == null || somLigado || semSom) return;
        somLigado = true;
        videoPlayer.SetDirectAudioMute(0, false);
        AplicarVolume();
        Log("som ligado");
    }

    public void DesligarSom()
    {
        if (videoPlayer == null || !somLigado) return;
        somLigado = false;
        videoPlayer.SetDirectAudioMute(0, true);
        Log("som desligado");
    }

    public void SetVolume(float v)
    {
        volumeVideo = Mathf.Clamp01(v);
        AplicarVolume();
    }

    void AplicarVolume()
    {
        if (videoPlayer == null) return;
        videoPlayer.SetDirectAudioVolume(0, volumeVideo);
    }

    void OnValidate()
    {
        if (!Application.isPlaying || videoPlayer == null) return;
        if (semSom) DesligarSom(); else if (!somSomentePerto) LigarSom();
        AplicarVolume();
    }

    void OnDestroy()
    {
        if (rtVideo != null) rtVideo.Release();
        if (imagemAtual is Texture2D) Destroy(imagemAtual);
    }

    void Log(string m) { if (mostrarDebug) Debug.Log("[FinalCard] " + name + ": " + m, this); }
}
