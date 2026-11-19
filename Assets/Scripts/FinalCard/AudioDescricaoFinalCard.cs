using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// Audiodescricao do Final Card (BFC / Prefab_FinalCard).
//
// - Icone "AudioDescricaoVideo": toca a audiodescricao da aba Video do admin do album.
// - Icone "AudioDescricaoTexto": toca a audiodescricao da aba Imagem.
//   Hoje o site tem UMA audiodescricao por figurinha e idioma (audioDescriptionUrl / audioDescriptionUrlEn),
//   igual nas duas abas. Se um dia o payload trouxer "videoAudioDescriptionUrl" / "videoAudioDescriptionUrlEn"
//   (e esses campos entrarem no EmuseuCard), o icone do video passa a usar esse audio sozinho.
// - Idioma: Idioma.Ingles (sem EN, toca o PT). Trocar o idioma para a fala.
// - Clicar de novo no icone que esta tocando = para. Sem audio para o card = icone escondido.
// - Durante a audiodescricao o som do video desce (fade) e volta quando ela termina.
// - Clique no video (Board_Puzzle): Mutar (padrao), Pausar ou Nada.
// - Mouse (cursor livre = ponto do mouse; travado = mira do centro) e toque do celular (MobileInput).
//   Nao usa collider: o teste e feito na caixa do proprio sprite/mesh, entao nao mexe na fisica nem nos triggers.
//
// Liga sozinho em todo FinalCardBoard da cena (nao precisa mexer no Inspector).
// Para mudar as opcoes so de um board (ou do prefab), adicione este componente no objeto do FinalCardBoard.
[DisallowMultipleComponent]
public sealed class AudioDescricaoFinalCard : MonoBehaviour
{
    public enum AcaoCliqueVideo { Mutar, Pausar, Nada }
    enum Tipo { Video, Imagem }

    // Desligue (false) para nao adicionar o componente sozinho nos boards.
    public static bool AutoAdicionar = true;

    [Header("Icones (vazio = procura filhos pelo nome)")]
    public Renderer iconeVideo;
    public Renderer iconeTexto;
    public string nomeIconeVideo = "AudioDescricaoVideo";
    public string nomeIconeTexto = "AudioDescricaoTexto";

    [Header("Clique no video")]
    [Tooltip("O que acontece ao clicar/tocar no video do board")]
    public AcaoCliqueVideo cliqueNoVideo = AcaoCliqueVideo.Mutar;

    [Header("Audiodescricao")]
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Segundos do fade do som do video (desce ao comecar, sobe ao terminar)")]
    public float tempoFadeVideo = 0.4f;
    [Tooltip("Esconde o icone quando o card nao tem audiodescricao")]
    public bool esconderSemAudio = true;
    [Tooltip("Esconde o icone do video quando o card nao tem video")]
    public bool esconderIconeVideoSemVideo = true;
    [Tooltip("Sem audio proprio do video no site: o icone do video toca o mesmo audio da imagem")]
    public bool videoUsaAudioDaImagemSeFaltar = true;
    [Tooltip("Para a fala quando o jogador se afasta mais que isso do board (0 = nunca)")]
    public float distanciaParar = 12f;
    [Tooltip("Para a fala quando o jogador sai do trigger do board (o mesmo que tira o som do video)")]
    public bool pararAoSairDoTrigger = true;
    [Tooltip("Segundos do fade da fala ao sair do trigger")]
    public float tempoFadeSaida = 0.3f;

    [Header("Clique")]
    public float distanciaMaximaClique = 25f;
    [Tooltip("Aumenta a area de clique dos icones (1 = tamanho do sprite)")]
    public float folgaIcone = 1.3f;
    [Tooltip("No celular a area dos icones fica maior")]
    public float folgaIconeCelular = 1.8f;

    [Header("Debug")]
    public bool mostrarLogs = true;

    FinalCardBoard board;
    AudioSource fonte;
    bool tocando, carregando;
    Tipo tipoAtual;
    string idTocando;
    int pedido;
    bool mutadoPeloClique, pausadoPeloClique;
    float mult = 1f;            // multiplicador do som do video (fade)
    float multAplicado = -1f;
    AudioSource fonteVideoAs;   // video em modo AudioSource
    float volumeBaseAs = 1f;
    bool abaixouAs;
    float proximaChecagem;
    Vector3 escalaVideo, escalaTexto;
    Color corVideo = Color.white, corTexto = Color.white;
    bool guardouVisual;
    bool sumindo;               // fala em fade-out (saiu do trigger)
    bool estavaPerto;           // jogador estava no trigger no frame anterior

    static readonly List<AudioDescricaoFinalCard> todos = new List<AudioDescricaoFinalCard>();
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static readonly Dictionary<string, List<Action<AudioClip>>> baixando = new Dictionary<string, List<Action<AudioClip>>>();

    // ---------------------------------------------------------------- auto
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { todos.Clear(); baixando.Clear(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
        AudioDescricaoBaixador.Garantir(); // tambem procura boards novos a cada 2s
        GarantirTodos();
    }

    static void AoCarregarCena(Scene s, LoadSceneMode m) { GarantirTodos(); }

    // Adiciona este componente em todo FinalCardBoard que ainda nao tem.
    public static void GarantirTodos()
    {
        if (!AutoAdicionar) return;
        foreach (var b in FindObjectsByType<FinalCardBoard>(FindObjectsInactive.Include))
            if (b != null && b.GetComponent<AudioDescricaoFinalCard>() == null)
                b.gameObject.AddComponent<AudioDescricaoFinalCard>();
    }

    // ---------------------------------------------------------------- ciclo
    void Awake()
    {
        board = GetComponent<FinalCardBoard>();
        if (board == null) board = GetComponentInParent<FinalCardBoard>(true);
        if (iconeVideo == null) iconeVideo = AcharIcone(nomeIconeVideo);
        if (iconeTexto == null) iconeTexto = AcharIcone(nomeIconeTexto);
        GuardarVisual();
        var go = new GameObject("AudioDescricao_Fonte");
        go.transform.SetParent(transform, false);
        fonte = go.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.loop = false;
        fonte.spatialBlend = 0f; // 2D: ouve igual de qualquer lugar
        fonte.volume = volume;
        Log("pronto (board=" + (board != null ? board.name : "NULL") + " iconeVideo=" + (iconeVideo != null) + " iconeTexto=" + (iconeTexto != null) + ")");
    }

    void OnEnable()
    {
        if (!todos.Contains(this)) todos.Add(this);
        Idioma.OnMudou += AoMudarIdioma;
        CardRemoteCatalog.Changed += AoMudarCatalogo;
        if (board != null) { board.JogadorSaiuDoTrigger -= AoSairDoTrigger; board.JogadorSaiuDoTrigger += AoSairDoTrigger; estavaPerto = board.JogadorNoTriggerSom; }
        proximaChecagem = 0f;
    }

    void OnDisable()
    {
        todos.Remove(this);
        Idioma.OnMudou -= AoMudarIdioma;
        CardRemoteCatalog.Changed -= AoMudarCatalogo;
        if (board != null) board.JogadorSaiuDoTrigger -= AoSairDoTrigger;
        Parar("board desligado");
        // Sem Update: devolve o som do video na hora.
        mult = mutadoPeloClique ? 0f : 1f;
        AplicarMult();
    }

    void AoMudarIdioma(bool ingles) { if (tocando || carregando) Parar("idioma mudou"); proximaChecagem = 0f; }
    void AoMudarCatalogo() { proximaChecagem = 0f; }
    void AoSairDoTrigger() { if (pararAoSairDoTrigger && (tocando || carregando)) PararComFade("jogador saiu do trigger"); }

    Renderer AcharIcone(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return null;
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t != transform && string.Equals(t.name, nome, StringComparison.OrdinalIgnoreCase))
            {
                var r = t.GetComponent<Renderer>();
                if (r != null) return r;
            }
        return null;
    }

    void GuardarVisual()
    {
        if (guardouVisual) return;
        guardouVisual = true;
        if (iconeVideo != null) { escalaVideo = iconeVideo.transform.localScale; corVideo = Cor(iconeVideo); }
        if (iconeTexto != null) { escalaTexto = iconeTexto.transform.localScale; corTexto = Cor(iconeTexto); }
    }

    void Update()
    {
        if (board == null) return;

        // Card trocou ou jogador longe: para.
        if ((tocando || carregando) && idTocando != board.IdAtual) Parar("card mudou");
        if ((tocando || carregando) && distanciaParar > 0f && Longe()) Parar("jogador se afastou");

        // Saiu do trigger (rede de seguranca do evento JogadorSaiuDoTrigger): para com fade curto.
        bool perto = board.JogadorNoTriggerSom;
        if (pararAoSairDoTrigger && estavaPerto && !perto && (tocando || carregando)) PararComFade("jogador saiu do trigger");
        estavaPerto = perto;
        AtualizarSumindo();

        // Fim da fala.
        if (tocando && fonte != null && !fonte.isPlaying && !AudioListener.pause && !EmuseuBridge.Paused)
        {
            tocando = false;
            Log("audiodescricao terminou");
        }

        if (Time.unscaledTime >= proximaChecagem)
        {
            proximaChecagem = Time.unscaledTime + 0.5f;
            AtualizarIcones();
        }

        TratarClique();
        AtualizarFade();
        AtualizarVisual();
    }

    bool Longe()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        return Vector3.Distance(cam.transform.position, transform.position) > distanciaParar;
    }

    // ---------------------------------------------------------------- icones
    void AtualizarIcones()
    {
        string id = board.IdAtual;
        bool ingles = Idioma.Ingles;
        string u;
        bool temImagem = ResolverUrl(id, Tipo.Imagem, ingles, out u);
        bool temVideoAudio = ResolverUrl(id, Tipo.Video, ingles, out u);
        bool temVideo = board.TemVideo || CardRemotoTemVideo(id);
        Mostrar(iconeTexto, !esconderSemAudio || temImagem);
        Mostrar(iconeVideo, (!esconderSemAudio || temVideoAudio) && (!esconderIconeVideoSemVideo || temVideo));
    }

    static void Mostrar(Renderer r, bool v) { if (r != null && r.enabled != v) r.enabled = v; }

    void AtualizarVisual()
    {
        // Icone que esta tocando pulsa; carregando fica meio apagado.
        float pulso = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
        if (iconeVideo != null)
        {
            bool ativo = (tocando || carregando) && tipoAtual == Tipo.Video;
            iconeVideo.transform.localScale = ativo && tocando ? escalaVideo * pulso : escalaVideo;
            SetCor(iconeVideo, ativo && carregando ? new Color(corVideo.r, corVideo.g, corVideo.b, corVideo.a * 0.55f) : corVideo);
        }
        if (iconeTexto != null)
        {
            bool ativo = (tocando || carregando) && tipoAtual == Tipo.Imagem;
            iconeTexto.transform.localScale = ativo && tocando ? escalaTexto * pulso : escalaTexto;
            SetCor(iconeTexto, ativo && carregando ? new Color(corTexto.r, corTexto.g, corTexto.b, corTexto.a * 0.55f) : corTexto);
        }
    }

    static Color Cor(Renderer r)
    {
        var sr = r as SpriteRenderer;
        return sr != null ? sr.color : Color.white;
    }

    static void SetCor(Renderer r, Color c)
    {
        var sr = r as SpriteRenderer;
        if (sr != null && sr.color != c) sr.color = c;
    }

    // ---------------------------------------------------------------- clique
    void TratarClique()
    {
        if (!CliqueNoFrame()) return;
        Camera cam = Camera.main;
        if (cam == null || !cam.isActiveAndEnabled) return;

        Vector3 pos;
        if (MobileInput.Ativo) pos = MobileInput.PosicaoClique(PosMouse());
        else if (Cursor.lockState == CursorLockMode.Locked) pos = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        else pos = PosMouse();
        Ray ray = cam.ScreenPointToRay(pos);

        float folga = MobileInput.Ativo ? folgaIconeCelular : folgaIcone;
        float dV, dT;
        bool hV = Acertou(iconeVideo, ray, folga, out dV);
        bool hT = Acertou(iconeTexto, ray, folga, out dT);
        if (hV || hT)
        {
            Tipo t = hV && (!hT || dV <= dT) ? Tipo.Video : Tipo.Imagem;
            CliqueNoIcone(t);
            return;
        }

        float dB;
        if (cliqueNoVideo != AcaoCliqueVideo.Nada && board.rendererImagem != null && VideoNoBoard() && Acertou(board.rendererImagem, ray, 1f, out dB))
            CliqueNoVideoDoBoard();
    }

    static bool CliqueNoFrame()
    {
        bool original = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        try { original = Input.GetMouseButtonDown(0); } catch (Exception) { }
#elif ENABLE_INPUT_SYSTEM
        try { var m = UnityEngine.InputSystem.Mouse.current; original = m != null && m.leftButton.wasPressedThisFrame; } catch (Exception) { }
#endif
        return MobileInput.Clique(original);
    }

    static Vector3 PosMouse()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        try { return Input.mousePosition; } catch (Exception) { }
#elif ENABLE_INPUT_SYSTEM
        try { var m = UnityEngine.InputSystem.Mouse.current; if (m != null) { Vector2 p = m.position.ReadValue(); return new Vector3(p.x, p.y, 0f); } } catch (Exception) { }
#endif
        return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
    }

    // Raio contra a caixa local do renderer (sprite ou mesh), girada/escalada com o objeto.
    bool Acertou(Renderer r, Ray rayMundo, float folga, out float distancia)
    {
        distancia = float.MaxValue;
        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return false;
        Transform t = r.transform;
        Bounds b = r.localBounds;
        Vector3 ext = b.extents;
        ext.x *= folga;
        ext.y *= folga;
        float maior = Mathf.Max(ext.x, ext.y);
        ext.z = Mathf.Max(ext.z, maior * 0.05f);
        b.extents = ext;
        Vector3 o = t.InverseTransformPoint(rayMundo.origin);
        Vector3 d = t.InverseTransformVector(rayMundo.direction);
        if (d.sqrMagnitude < 1e-12f) return false;
        Ray local = new Ray(o, d);
        float dl;
        if (!b.IntersectRay(local, out dl)) return false;
        Vector3 pMundo = t.TransformPoint(local.GetPoint(dl));
        distancia = Vector3.Distance(rayMundo.origin, pMundo);
        return distancia <= distanciaMaximaClique;
    }

    void CliqueNoIcone(Tipo t)
    {
        if ((tocando || carregando) && tipoAtual == t) { Parar("clique de novo"); return; }
        Tocar(t);
    }

    bool VideoNoBoard()
    {
        VideoPlayer vp = board.videoPlayer;
        if (vp == null || !board.TemVideo) return false;
        return vp.isPlaying || pausadoPeloClique || vp.isPrepared;
    }

    void CliqueNoVideoDoBoard()
    {
        VideoPlayer vp = board.videoPlayer;
        if (cliqueNoVideo == AcaoCliqueVideo.Mutar)
        {
            mutadoPeloClique = !mutadoPeloClique;
            Log("clique no video: " + (mutadoPeloClique ? "mudo" : "som de volta"));
        }
        else if (cliqueNoVideo == AcaoCliqueVideo.Pausar && vp != null)
        {
            if (vp.isPlaying) { vp.Pause(); pausadoPeloClique = true; Log("clique no video: pausado"); }
            else { vp.Play(); pausadoPeloClique = false; Log("clique no video: tocando"); }
        }
    }

    // ---------------------------------------------------------------- tocar
    void Tocar(Tipo t)
    {
        Parar(null);
        foreach (var outro in todos.ToArray()) if (outro != this && outro != null) outro.Parar("outro board comecou a falar");

        string id = board.IdAtual;
        string url;
        if (!ResolverUrl(id, t, Idioma.Ingles, out url))
        {
            Log("card " + id + " sem audiodescricao (" + t + ")");
            return;
        }
        tipoAtual = t;
        idTocando = id;
        carregando = true;
        int meu = ++pedido;
        Log("pedindo audiodescricao " + t + " " + (Idioma.Ingles ? "EN" : "PT") + " card=" + id + " arquivo=" + NomeArquivo(url));
        Pedir(url, clip =>
        {
            if (this == null || meu != pedido || !carregando) return;
            carregando = false;
            if (clip == null) { Log("falhou baixar a audiodescricao"); return; }
            fonte.clip = clip;
            fonte.volume = volume;
            fonte.Play();
            tocando = true;
            Log("tocando " + t + " (" + clip.length.ToString("0.0") + "s)");
        });
    }

    public void Parar(string motivo)
    {
        pedido++;
        bool estava = tocando || carregando;
        tocando = false;
        carregando = false;
        sumindo = false;
        if (fonte != null) { if (fonte.isPlaying) fonte.Stop(); fonte.volume = volume; }
        if (estava && motivo != null) Log("parou: " + motivo);
    }

    // Para com fade curto: icone volta na hora, o som do video volta (o FinalCardBoard o leva a 0 fora do trigger)
    // e a fala desce ate 0 em tempoFadeSaida segundos.
    public void PararComFade(string motivo)
    {
        if (fonte == null || !fonte.isPlaying || tempoFadeSaida <= 0f) { Parar(motivo); return; }
        pedido++; // cancela download em andamento
        bool estava = tocando || carregando;
        tocando = false;
        carregando = false;
        sumindo = true;
        if (estava && motivo != null) Log("parou (fade): " + motivo);
    }

    void AtualizarSumindo()
    {
        if (!sumindo) return;
        if (fonte == null) { sumindo = false; return; }
        fonte.volume = Mathf.MoveTowards(fonte.volume, 0f, Time.unscaledDeltaTime * Mathf.Max(0.01f, volume) / Mathf.Max(0.01f, tempoFadeSaida));
        if (fonte.volume <= 0.001f || !fonte.isPlaying)
        {
            fonte.Stop();
            fonte.volume = volume;
            sumindo = false;
        }
    }

    // API para botoes/UnityEvent.
    public void TocarVideo() { CliqueNoIcone(Tipo.Video); }
    public void TocarImagem() { CliqueNoIcone(Tipo.Imagem); }
    public void AlternarMuteVideo() { mutadoPeloClique = !mutadoPeloClique; }

    // ---------------------------------------------------------------- som do video
    void AtualizarFade()
    {
        // Abaixa quando a fala comeca de verdade (enquanto baixa, o video segue normal).
        float alvo = (tocando || mutadoPeloClique) ? 0f : 1f;
        mult = Mathf.MoveTowards(mult, alvo, Time.unscaledDeltaTime / Mathf.Max(0.01f, tempoFadeVideo));
        AplicarMult();
    }

    void AplicarMult()
    {
        if (board == null) return;
        if (Mathf.Approximately(mult, multAplicado)) return;
        multAplicado = mult;
        board.multiplicadorSomExterno = mult;   // modo Direct: o FinalCardBoard aplica (com mute em 0 para o iPhone)
        board.ReaplicarSom();

        // Video tocando por AudioSource: abaixa a propria fonte.
        VideoPlayer vp = board.videoPlayer;
        AudioSource asVid = vp != null && vp.audioOutputMode == VideoAudioOutputMode.AudioSource ? vp.GetTargetAudioSource(0) : null;
        if (asVid != fonteVideoAs)
        {
            if (fonteVideoAs != null && abaixouAs) fonteVideoAs.volume = volumeBaseAs;
            fonteVideoAs = asVid;
            abaixouAs = false;
        }
        if (fonteVideoAs != null)
        {
            if (!abaixouAs) volumeBaseAs = fonteVideoAs.volume; // volume normal (pode mudar por fora)
            if (mult >= 0.999f) { fonteVideoAs.volume = volumeBaseAs; abaixouAs = false; }
            else { fonteVideoAs.volume = volumeBaseAs * mult; abaixouAs = true; }
        }
    }

    // ---------------------------------------------------------------- dados do site
    static FieldInfo campoVideoPt, campoVideoEn;
    static bool procurouCampos;

    static void ProcurarCampos()
    {
        if (procurouCampos) return;
        procurouCampos = true;
        Type t = typeof(EmuseuCard);
        campoVideoPt = t.GetField("videoAudioDescriptionUrl");
        campoVideoEn = t.GetField("videoAudioDescriptionUrlEn");
    }

    static string Ler(FieldInfo f, EmuseuCard c)
    {
        if (f == null || c == null) return null;
        return f.GetValue(c) as string;
    }

    bool ResolverUrl(string id, Tipo tipo, bool ingles, out string url)
    {
        url = null;
        if (string.IsNullOrEmpty(id) || EmuseuBridge.PlayConfig == null) return false;
        EmuseuCard ativo;
        CardRemoteCatalog.TryGet(id, out ativo);
        var campaign = EmuseuBridge.PlayConfig.campaign;
        EmuseuCard pt = Achar(campaign != null && campaign.ptbr != null ? campaign.ptbr.albums : null, id, ativo);
        EmuseuCard en = Achar(campaign != null && campaign.eng != null ? campaign.eng.albums : null, id, ativo);
        if (pt == null && en == null) pt = ativo; // payload antigo: so campaign.albums

        string urlPt, urlEn;
        if (tipo == Tipo.Video)
        {
            ProcurarCampos();
            urlPt = Primeiro(Ler(campoVideoPt, pt));
            urlEn = Primeiro(Ler(campoVideoEn, pt), Ler(campoVideoPt, en), Ler(campoVideoEn, en));
            if (Vazio(urlPt) && Vazio(urlEn) && videoUsaAudioDaImagemSeFaltar)
                return ResolverUrl(id, Tipo.Imagem, ingles, out url);
        }
        else
        {
            urlPt = Primeiro(pt != null ? pt.audioDescriptionUrl : null);
            urlEn = Primeiro(pt != null ? pt.audioDescriptionUrlEn : null, en != null ? en.audioDescriptionUrl : null, en != null ? en.audioDescriptionUrlEn : null);
        }
        url = ingles && !Vazio(urlEn) ? urlEn : urlPt; // sem EN: PT
        return !Vazio(url);
    }

    bool CardRemotoTemVideo(string id)
    {
        EmuseuCard c;
        if (!CardRemoteCatalog.TryGet(id, out c) || c == null) return false;
        return !Vazio(c.videoUrl) || !Vazio(c.videoUrlEn);
    }

    // Card do pacote com o mesmo frame (prefere o mesmo id do card que o catalogo escolheu).
    static EmuseuCard Achar(List<EmuseuAlbum> albums, string frame, EmuseuCard preferido)
    {
        if (albums == null) return null;
        EmuseuCard primeiro = null;
        foreach (var a in albums)
        {
            if (a == null || a.pages == null) continue;
            foreach (var p in a.pages)
            {
                if (p == null || p.cards == null) continue;
                foreach (var c in p.cards)
                {
                    if (c == null || string.IsNullOrEmpty(c.frame)) continue;
                    if (!string.Equals(c.frame.Trim(), frame.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    if (preferido != null && !string.IsNullOrEmpty(preferido.id) && c.id == preferido.id) return c;
                    if (primeiro == null) primeiro = c;
                }
            }
        }
        return primeiro;
    }

    static string Primeiro(params string[] v)
    {
        foreach (var s in v) if (!Vazio(s)) return s.Trim();
        return null;
    }

    static bool Vazio(string s) { return string.IsNullOrWhiteSpace(s); }

    // ---------------------------------------------------------------- download (mp3, WebGL ok)
    static string SemQuery(string url)
    {
        int q = url.IndexOf('?');
        return q >= 0 ? url.Substring(0, q) : url;
    }

    // So o nome do arquivo (a URL e assinada: nunca vai para o log).
    static string NomeArquivo(string url)
    {
        string b = SemQuery(url);
        int i = b.LastIndexOf('/');
        return i >= 0 ? b.Substring(i + 1) : b;
    }

    void Pedir(string url, Action<AudioClip> pronto)
    {
        string chave = SemQuery(url);
        AudioClip clip;
        if (cache.TryGetValue(chave, out clip) && clip != null) { pronto(clip); return; }
        List<Action<AudioClip>> fila;
        if (baixando.TryGetValue(chave, out fila)) { fila.Add(pronto); return; }
        baixando[chave] = new List<Action<AudioClip>> { pronto };
        AudioDescricaoBaixador.Iniciar(Baixar(url, chave));
    }

    static IEnumerator Baixar(string url, string chave)
    {
        AudioClip clip = null;
        using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            // Mantem o MP3 comprimido na memoria (CompressedInMemory) em vez de decodificar para PCM. Ajuda o iPhone.
            ((DownloadHandlerAudioClip)req.downloadHandler).compressed = true;
            req.timeout = 60;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try { clip = DownloadHandlerAudioClip.GetContent(req); }
                catch (Exception ex) { Debug.LogWarning("[AudioDescricao] erro ao ler mp3 " + NomeArquivo(url) + ": " + ex.Message); }
            }
            else Debug.LogWarning("[AudioDescricao] download falhou " + NomeArquivo(url) + " (http " + req.responseCode + "): " + req.error);
        }
        if (clip != null) { clip.name = "AudioDescricao_" + NomeArquivo(url); cache[chave] = clip; }
        List<Action<AudioClip>> fila;
        if (baixando.TryGetValue(chave, out fila))
        {
            baixando.Remove(chave);
            foreach (var cb in fila)
            {
                try { cb(clip); } catch (Exception ex) { Debug.LogWarning("[AudioDescricao] callback falhou: " + ex.Message); }
            }
        }
    }

    void Log(string m) { if (mostrarLogs) Debug.Log("[AudioDescricao] " + name + ": " + m, this); }
}
