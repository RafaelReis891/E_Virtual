using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Video;

// Debug do site emuseu (so diagnostico, nao muda comportamento do jogo).
// - Todo log tem o prefixo [Emuseu] e aparece no console do navegador (F12) no WebGL.
// - Guarda os ultimos eventos em memoria (anel de 200) e mostra no overlay (F9).
// - No WebGL manda tudo para window.emuseuDebug (ver Assets/Plugins/WebGL/EmuseuDebug.jslib).
// Nenhuma configuracao no Inspector: e uma classe estatica.
public static class EmuseuDebug
{
    public const string Prefix = "[Emuseu]";
    public const int MaxEventos = 200;
    public const int MaxCharsPayloadNoLog = 2000;
    public const float SegundosAvisoVideo = 10f;
    // Faixa de frames dos Final Cards que esperamos ver no site.
    public const int FrameMin = 11;
    public const int FrameMax = 50;

    public struct Evento
    {
        public float tempo;
        public int nivel;     // 0 info, 1 aviso, 2 erro
        public string tag;
        public string msg;
    }

    class VideoPendente
    {
        public VideoPlayer vp;
        public string id;
        public string url;
        public float inicio;
        public bool avisou;
    }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---------- estado ----------
    static Evento[] anel = new Evento[MaxEventos];
    static int proximo;
    static int total;
    static bool iniciado;

    static bool bridgeCriado;
    static int requests;
    static float tempoUltimoRequest;
    static int recebidos;
    static int ultimoLen;
    static float tempoUltimoPayload;
    static string ultimoPayload;
    static bool payloadBrutoLogado;
    static string ultimoParse;
    static int avisosParse;
    static string usuarioResumo;
    static string campanhaResumo;
    static readonly List<string> albumLinhas = new List<string>();
    static string catalogoTexto;
    static int cardsMostrados;
    static int imgOk, imgFalha;
    static int vidPreparado, vidErro, vidFallback;
    static string ultimoErro;

    static readonly Dictionary<string, float> inicioImagem = new Dictionary<string, float>();
    static readonly List<VideoPendente> pendentes = new List<VideoPendente>();
    static readonly Dictionary<string, int> logsVistos = new Dictionary<string, int>();

    public static int TotalEventos { get { return total; } }
    public static string UltimoPayload { get { return ultimoPayload; } }

    // ---------- JS (so no WebGL de verdade) ----------
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void EmuseuDebug_SetPayload(string json);
    [DllImport("__Internal")] static extern void EmuseuDebug_Push(string linha);
    [DllImport("__Internal")] static extern void EmuseuDebug_SetSummary(string texto);
#endif

    static void JsSetPayload(string s)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { EmuseuDebug_SetPayload(s ?? ""); } catch (Exception) { }
#endif
    }

    static void JsPush(string linha)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { EmuseuDebug_Push(linha ?? ""); } catch (Exception) { }
#endif
    }

    static void JsSummary()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { EmuseuDebug_SetSummary(BuildSummary()); } catch (Exception) { }
#endif
    }

    // ---------- inicio ----------

    // Zera tudo (seguro quando o Domain Reload do editor esta desligado).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar()
    {
        Application.logMessageReceived -= AoLogUnity;
        anel = new Evento[MaxEventos];
        proximo = 0; total = 0; iniciado = false;
        bridgeCriado = false; requests = 0; tempoUltimoRequest = 0f;
        recebidos = 0; ultimoLen = 0; tempoUltimoPayload = 0f; ultimoPayload = null;
        payloadBrutoLogado = false; ultimoParse = null; avisosParse = 0;
        usuarioResumo = null; campanhaResumo = null; albumLinhas.Clear(); catalogoTexto = null;
        cardsMostrados = 0; imgOk = 0; imgFalha = 0; vidPreparado = 0; vidErro = 0; vidFallback = 0;
        ultimoErro = null;
        inicioImagem.Clear(); pendentes.Clear(); logsVistos.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void GarantirInicio()
    {
        if (!iniciado) Iniciar();
    }

    static void Iniciar()
    {
        iniciado = true;
        Application.logMessageReceived -= AoLogUnity;
        Application.logMessageReceived += AoLogUnity;

        string url = "";
        try { url = Application.absoluteURL ?? ""; } catch (Exception) { }
        int q = url.IndexOf('?');
        if (q >= 0) url = url.Substring(0, q) + "?(query omitida)";

        Registrar(0, "inicio", "debug ligado | unity=" + Application.unityVersion +
            " plataforma=" + Application.platform +
            " app=" + Application.productName + " v" + Application.version +
            " tela=" + Screen.width + "x" + Screen.height +
            " idioma=" + Application.systemLanguage +
            " url=" + (url.Length == 0 ? "(vazia)" : url), true);
        Registrar(0, "inicio", "dica: F9 abre o overlay | console: emuseuDebug.dump() | copy(emuseuDebug.lastPayload)", true);
    }

    // ---------- registro basico ----------

    public static void Info(string tag, string msg) { Add(0, tag, msg, true); }
    public static void Warn(string tag, string msg) { Add(1, tag, msg, true); }
    public static void Error(string tag, string msg) { Add(2, tag, msg, true); }

    static void Add(int nivel, string tag, string msg, bool imprimir)
    {
        if (!iniciado) Iniciar();
        Registrar(nivel, tag, msg, imprimir);
    }

    static void Registrar(int nivel, string tag, string msg, bool imprimir)
    {
        if (msg == null) msg = "";
        var e = new Evento { tempo = Time.realtimeSinceStartup, nivel = nivel, tag = tag ?? "", msg = msg };
        anel[proximo] = e;
        proximo = (proximo + 1) % MaxEventos;
        total++;

        string linha = Formatar(e);
        if (nivel == 2) ultimoErro = Cortar(linha, 300);

        if (imprimir)
        {
            string completa = Prefix + " " + linha;
            if (nivel == 0) Debug.Log(completa);
            else if (nivel == 1) Debug.LogWarning(completa);
            else Debug.LogError(completa);
        }

        JsPush(linha);
        JsSummary();
    }

    public static string Formatar(Evento e)
    {
        string n = e.nivel == 2 ? "ERRO " : (e.nivel == 1 ? "AVISO " : "");
        return e.tempo.ToString("F2", Inv) + "s " + n + "[" + e.tag + "] " + e.msg;
    }

    // Ultimos n eventos, do mais novo para o mais antigo.
    public static List<Evento> Recentes(int n)
    {
        var lista = new List<Evento>();
        int disponiveis = Math.Min(total, MaxEventos);
        if (n > disponiveis) n = disponiveis;
        for (int i = 0; i < n; i++)
        {
            int idx = ((proximo - 1 - i) % MaxEventos + MaxEventos) % MaxEventos;
            lista.Add(anel[idx]);
        }
        return lista;
    }

    // Copia de tudo (mais novo primeiro) em texto.
    public static string TextoCompleto()
    {
        var sb = new StringBuilder(8192);
        sb.Append(BuildSummary()).Append('\n').Append("---- eventos (mais novo primeiro) ----").Append('\n');
        foreach (var e in Recentes(MaxEventos)) sb.Append(Formatar(e)).Append('\n');
        return sb.ToString();
    }

    // Pega avisos/erros de qualquer parte do Unity (ex: excecoes) para o buffer, sem imprimir de novo.
    static void AoLogUnity(string condicao, string pilha, LogType tipo)
    {
        if (condicao == null) return;
        if (condicao.StartsWith(Prefix, StringComparison.Ordinal)) return; // nosso proprio log
        bool nosso = condicao.StartsWith("[FinalCard]", StringComparison.Ordinal) ||
                     condicao.StartsWith("[EmuseuBridge]", StringComparison.Ordinal) ||
                     condicao.StartsWith("[CardRemoteCatalog]", StringComparison.Ordinal);
        if (tipo == LogType.Log && !nosso) return;

        // Evita encher o buffer com a mesma mensagem repetida.
        string chave = ((int)tipo) + condicao;
        if (chave.Length > 200) chave = chave.Substring(0, 200);
        if (logsVistos.Count > 300) logsVistos.Clear();
        int vezes;
        logsVistos.TryGetValue(chave, out vezes);
        vezes++;
        logsVistos[chave] = vezes;
        if (vezes > 3)
        {
            if (vezes == 4) Registrar(1, "unity", "(mensagem repetida, suprimindo) " + Cortar(condicao, 120), false);
            return;
        }

        int nivel = (tipo == LogType.Warning || tipo == LogType.Log) ? 1 : 2;
        string msg = Cortar(condicao, 500);
        if ((tipo == LogType.Exception || tipo == LogType.Error) && !string.IsNullOrEmpty(pilha))
        {
            string[] linhas = pilha.Split('\n');
            msg += " | pilha: " + Cortar(linhas[0], 160) + (linhas.Length > 1 ? " / " + Cortar(linhas[1], 160) : "");
        }
        Registrar(nivel, "unity", msg, false);
    }

    // ---------- bridge ----------

    public static void BridgeCriado()
    {
        bridgeCriado = true;
        Info("bridge", "EmuseuBridge criado (GameObject '" + EmuseuBridge.GameObjectName + "')");
    }

    public static void Enviado(string tipo, int bytes)
    {
        if (tipo == "play-config-request")
        {
            requests++;
            tempoUltimoRequest = Time.realtimeSinceStartup;
        }
        Info("bridge", "Unity -> React type=" + tipo + " bytes=" + bytes + (tipo == "play-config-request" ? " (pedido #" + requests + ")" : ""));
    }

    public static void PlayConfigRecebido(string json)
    {
        recebidos++;
        ultimoParse = null;
        avisosParse = 0;
        tempoUltimoPayload = Time.realtimeSinceStartup;
        ultimoLen = json == null ? 0 : json.Length;

        if (string.IsNullOrEmpty(json))
        {
            Warn("payload", "OnPlayConfig recebido #" + recebidos + " VAZIO (null ou \"\")");
            ultimoPayload = "";
            JsSetPayload("");
            return;
        }

        Info("payload", "OnPlayConfig recebido #" + recebidos + " json length=" + json.Length);
        ultimoPayload = json;
        JsSetPayload(json);   // json completo em window.emuseuDebug.lastPayload

        if (!payloadBrutoLogado)
        {
            payloadBrutoLogado = true;
            int n = Math.Min(json.Length, MaxCharsPayloadNoLog);
            Info("payload", "RAW (primeiros " + n + " de " + json.Length + " chars): " + json.Substring(0, n));
        }
    }

    public static void ParseErro(string motivo)
    {
        ultimoParse = "FALHOU: " + motivo;
        Error("parse", "falhou: " + motivo);
    }

    // Aviso de campo com tipo estranho (parse continua).
    public static void ParseAviso(string caminho, string msg)
    {
        avisosParse++;
        if (avisosParse <= 20) Warn("parse", "campo '" + caminho + "': " + msg);
        else if (avisosParse == 21) Warn("parse", "(mais avisos de campo suprimidos)");
    }

    public static void ParseResultado(EmuseuPayload p)
    {
        if (p == null)
        {
            if (ultimoParse == null || !ultimoParse.StartsWith("FALHOU", StringComparison.Ordinal))
            {
                ultimoParse = "FALHOU: payload nulo (formato inesperado)";
                Error("parse", "payload nulo (tipo inesperado ou json vazio)");
            }
            return;
        }
        ultimoParse = "ok" + (avisosParse > 0 ? " (com " + avisosParse + " avisos de campo)" : "");
        Info("parse", "parse OK" + (avisosParse > 0 ? " com " + avisosParse + " avisos de campo" : ""));
        DescreverPayload(p);
    }

    static void DescreverPayload(EmuseuPayload p)
    {
        albumLinhas.Clear();

        var u = p.user;
        if (u == null) usuarioResumo = "user=null (payload sem usuario)";
        else
        {
            string roles = (u.roles != null && u.roles.Count > 0) ? string.Join(",", u.roles) : "(sem roles)";
            usuarioResumo = "user id=" + (u.id ?? "(null)") + " roles=" + roles;
        }
        Info("payload", usuarioResumo);

        var c = p.campaign;
        if (c == null)
        {
            campanhaResumo = "campaign=null";
            Warn("payload", "campaign vem null: nada para aplicar");
            return;
        }
        int nAlbuns = c.albums != null ? c.albums.Count : 0;
        int nGames = c.games != null ? c.games.Count : 0;
        int nSponsors = p.sponsors != null ? p.sponsors.Count : 0;
        campanhaResumo = "campaign id=" + (c.id ?? "(null)") + " nome='" + (c.name ?? "") + "' games=" + nGames +
                         " albuns=" + nAlbuns + " sponsors=" + nSponsors;
        Info("payload", campanhaResumo);
        if (nAlbuns == 0) Warn("payload", "campaign sem albuns");

        string exemploImg = null, exemploVideo = null;
        int linhasLogadas = 0;
        for (int i = 0; i < nAlbuns; i++)
        {
            var a = c.albums[i];
            if (a == null) { albumLinhas.Add("album[" + i + "] = null"); continue; }

            int paginas = 0, cards = 0, comFrame = 0, comVideo = 0, comImagem = 0;
            if (a.pages != null)
            {
                foreach (var pg in a.pages)
                {
                    if (pg == null) continue;
                    paginas++;
                    if (pg.cards == null) continue;
                    foreach (var cd in pg.cards)
                    {
                        if (cd == null) continue;
                        cards++;
                        if (!string.IsNullOrWhiteSpace(cd.frame)) comFrame++;
                        if (!string.IsNullOrWhiteSpace(cd.videoUrl)) { comVideo++; if (exemploVideo == null) exemploVideo = cd.videoUrl; }
                        string img = !string.IsNullOrWhiteSpace(cd.imageUrl) ? cd.imageUrl : cd.thumbUrl;
                        if (!string.IsNullOrWhiteSpace(img)) { comImagem++; if (exemploImg == null) exemploImg = img; }
                    }
                }
            }
            string linha = "album[" + i + "] '" + (a.name ?? "") + "' id=" + (a.id ?? "") + " active=" + a.active +
                           " paginas=" + paginas + " cards=" + cards + " comFrame=" + comFrame +
                           " comVideo=" + comVideo + " comImagem=" + comImagem;
            albumLinhas.Add(linha);
            if (linhasLogadas < 30) { Info("album", linha); linhasLogadas++; }
        }
        if (exemploImg != null) Info("payload", "exemplo imageUrl: " + Cortar(exemploImg, 300));
        if (exemploVideo != null) Info("payload", "exemplo videoUrl: " + Cortar(exemploVideo, 300));
    }

    // ---------- catalogo ----------

    public static void CatalogoPreenchido(int albuns, List<string> frames, int totalCards, int semFrame,
                                          int frameInvalido, List<string> invalidos, List<string> duplicados)
    {
        if (frames == null) frames = new List<string>();
        frames.Sort(CompararFrames);
        var set = new HashSet<string>(frames, StringComparer.OrdinalIgnoreCase);

        var faltam = new List<string>();
        for (int n = FrameMin; n <= FrameMax; n++)
        {
            string f = "F" + n;
            if (!set.Contains(f)) faltam.Add(f);
        }
        var fora = new List<string>();
        foreach (var f in frames)
        {
            int num = NumeroDoFrame(f);
            if (num < FrameMin || num > FrameMax) fora.Add(f);
        }

        var sb = new StringBuilder(512);
        sb.Append("catalogo: ").Append(frames.Count).Append(" frames remotos (de ").Append(totalCards).Append(" cards em ").Append(albuns).Append(" albuns)\n");
        sb.Append("frames: ").Append(frames.Count == 0 ? "(nenhum)" : string.Join(",", frames)).Append('\n');
        sb.Append("faltando F").Append(FrameMin).Append("-F").Append(FrameMax).Append(": ").Append(faltam.Count)
          .Append(faltam.Count > 0 ? " -> " + string.Join(",", faltam) : "").Append('\n');
        if (fora.Count > 0) sb.Append("frames fora da faixa F").Append(FrameMin).Append("-F").Append(FrameMax).Append(": ").Append(string.Join(",", fora)).Append('\n');
        sb.Append("duplicados resolvidos: ").Append(duplicados != null ? duplicados.Count : 0).Append('\n');
        sb.Append("cards ignorados sem frame: ").Append(semFrame).Append('\n');
        sb.Append("cards ignorados com frame invalido: ").Append(frameInvalido);
        if (invalidos != null && invalidos.Count > 0) sb.Append(" (").Append(string.Join(",", invalidos)).Append(")");
        catalogoTexto = sb.ToString();

        Info("catalogo", frames.Count + " frames remotos de " + totalCards + " cards (" + albuns + " albuns)");
        Info("catalogo", "frames: " + (frames.Count == 0 ? "(nenhum)" : string.Join(",", frames)));
        if (frames.Count == 0) Warn("catalogo", "catalogo VAZIO: o jogo vai usar so os dados locais");
        Info("catalogo", "faltando F" + FrameMin + "-F" + FrameMax + ": " + faltam.Count + (faltam.Count > 0 ? " -> " + string.Join(",", faltam) : ""));
        if (fora.Count > 0) Info("catalogo", "frames fora da faixa: " + string.Join(",", fora));
        Info("catalogo", "duplicados resolvidos: " + (duplicados != null ? duplicados.Count : 0));
        if (duplicados != null)
            for (int i = 0; i < duplicados.Count && i < 15; i++) Info("catalogo", "dup " + duplicados[i]);
        Info("catalogo", "ignorados sem frame=" + semFrame + " frame invalido=" + frameInvalido +
             (invalidos != null && invalidos.Count > 0 ? " (" + string.Join(",", invalidos) + ")" : ""));
    }

    static int NumeroDoFrame(string f)
    {
        int n;
        if (!string.IsNullOrEmpty(f) && f.Length > 1 && int.TryParse(f.Substring(1), NumberStyles.Integer, Inv, out n)) return n;
        return int.MaxValue;
    }

    static int CompararFrames(string a, string b)
    {
        int na = NumeroDoFrame(a), nb = NumeroDoFrame(b);
        if (na != nb) return na.CompareTo(nb);
        return string.CompareOrdinal(a, b);
    }

    // ---------- FinalCardBoard ----------

    static bool EhHttp(string s)
    {
        return !string.IsNullOrEmpty(s) &&
               (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    public static void CardMostrado(string id, bool remoto, string imagem, string video, string board)
    {
        cardsMostrados++;
        Info("card", "id=" + id + " board=" + board + " fonte=" + (remoto ? "remote" : "local") +
             " imagem(" + (EhHttp(imagem) ? "url" : "local") + ")=" + (string.IsNullOrEmpty(imagem) ? "(vazio)" : Cortar(imagem, 400)) +
             " video(" + (EhHttp(video) ? "url" : (string.IsNullOrEmpty(video) ? "nenhum" : "local")) + ")=" +
             (string.IsNullOrEmpty(video) ? "(nenhum)" : Cortar(video, 400)));
    }

    public static void ImagemSemUrl(string id, string board)
    {
        Warn("imagem", "id=" + id + " board=" + board + " sem caminho de imagem (nada para carregar)");
    }

    public static void ImagemInicio(string id, string url, string board)
    {
        if (inicioImagem.Count > 100) inicioImagem.Clear();
        if (url != null) inicioImagem[url] = Time.realtimeSinceStartup;
        Info("imagem", "baixando id=" + id + " board=" + board + " url=" + Cortar(url, 400));
    }

    public static void ImagemResultado(string id, string url, bool ok, int w, int h, ulong bytes, string erro, long codigo)
    {
        float dt = -1f;
        float t0;
        if (url != null && inicioImagem.TryGetValue(url, out t0)) dt = Time.realtimeSinceStartup - t0;
        string tempo = dt >= 0f ? " tempo=" + dt.ToString("F2", Inv) + "s" : "";

        if (ok)
        {
            imgOk++;
            Info("imagem", "OK id=" + id + " " + w + "x" + h + " bytes=" + bytes + " http=" + codigo + tempo + " url=" + Cortar(url, 200));
            return;
        }

        imgFalha++;
        string dica = "";
        if (codigo == 0) dica = " DICA: http=0 sem resposta (CORS/rede/URL invalida; veja erro de rede no console do navegador)";
        else if (codigo == 401 || codigo == 403) dica = " DICA: sem permissao ou URL assinada expirada";
        else if (codigo == 404) dica = " DICA: arquivo nao existe nessa URL";
        Error("imagem", "FALHOU id=" + id + " erro='" + erro + "' http=" + codigo + tempo + " url=" + Cortar(url, 500) + dica);
    }

    public static void VideoPreparar(VideoPlayer vp, string id, string url)
    {
        if (vp == null) return;
        Info("video", "preparando id=" + id + " url=" + Cortar(url, 400));
        vp.prepareCompleted -= AoPrepararVideo;
        vp.prepareCompleted += AoPrepararVideo;
        vp.errorReceived -= AoErroVideo;
        vp.errorReceived += AoErroVideo;
        pendentes.RemoveAll(x => x.vp == vp);
        pendentes.Add(new VideoPendente { vp = vp, id = id, url = url, inicio = Time.realtimeSinceStartup });
    }

    static void AoPrepararVideo(VideoPlayer vp)
    {
        string id = "?";
        float dt = -1f;
        for (int i = pendentes.Count - 1; i >= 0; i--)
        {
            if (pendentes[i].vp != vp) continue;
            id = pendentes[i].id;
            dt = Time.realtimeSinceStartup - pendentes[i].inicio;
            pendentes.RemoveAt(i);
        }
        vidPreparado++;
        string info = "";
        try
        {
            info = " " + vp.width + "x" + vp.height + " duracao=" + vp.length.ToString("F1", Inv) + "s fps=" + vp.frameRate.ToString("F0", Inv) +
                   " audioTracks=" + vp.audioTrackCount;
        }
        catch (Exception) { }
        Info("video", "prepare OK id=" + id + info + (dt >= 0f ? " tempo=" + dt.ToString("F2", Inv) + "s" : ""));
    }

    static void AoErroVideo(VideoPlayer vp, string msg)
    {
        vidErro++;
        string id = "?";
        for (int i = pendentes.Count - 1; i >= 0; i--)
        {
            if (pendentes[i].vp != vp) continue;
            id = pendentes[i].id;
            pendentes.RemoveAt(i);
        }
        string url = "";
        try { url = vp != null ? vp.url : ""; } catch (Exception) { }
        Error("video", "errorReceived id=" + id + " msg='" + msg + "' url=" + Cortar(url, 500) +
              " DICA: no WebGL confira formato (mp4 H.264/AAC), CORS e se a URL assinada expirou");
    }

    // O board decidiu mostrar a imagem no lugar do video.
    public static void VideoFallback(string id, string url, string motivo)
    {
        vidFallback++;
        Warn("video", "FALLBACK para imagem id=" + id + " motivo='" + motivo + "' url=" + Cortar(url, 300));
    }

    // Chamado todo frame pelo overlay: avisa se o video nao prepara em 10s.
    public static void Tick()
    {
        for (int i = pendentes.Count - 1; i >= 0; i--)
        {
            var p = pendentes[i];
            if (p.vp == null) { pendentes.RemoveAt(i); continue; }
            if (p.avisou) continue;
            if (Time.realtimeSinceStartup - p.inicio < SegundosAvisoVideo) continue;
            p.avisou = true;
            bool pronto = false;
            try { pronto = p.vp.isPrepared; } catch (Exception) { }
            if (pronto) { pendentes.RemoveAt(i); continue; }
            Warn("video", "ainda NAO preparou apos " + SegundosAvisoVideo.ToString("F0", Inv) + "s id=" + p.id +
                 " url=" + Cortar(p.url, 300) + " (veja erros de rede/CORS no console)");
        }
    }

    // ---------- resumo ----------

    public static string BuildSummary()
    {
        var sb = new StringBuilder(1536);
        sb.Append("tempo=").Append(Time.realtimeSinceStartup.ToString("F1", Inv)).Append("s eventos=").Append(total).Append('\n');
        sb.Append("bridge: ").Append(bridgeCriado ? "criado" : "NAO criado").Append('\n');
        sb.Append("pedidos play-config enviados: ").Append(requests);
        if (requests > 0) sb.Append(" (ultimo em ").Append(tempoUltimoRequest.ToString("F1", Inv)).Append("s)");
        sb.Append('\n');
        sb.Append("play-config recebidos: ").Append(recebidos);
        if (recebidos > 0) sb.Append(" | ultimo json length=").Append(ultimoLen).Append(" em ").Append(tempoUltimoPayload.ToString("F1", Inv)).Append("s");
        sb.Append('\n');
        sb.Append("parse: ").Append(ultimoParse ?? "(nenhum payload ainda)").Append('\n');
        if (usuarioResumo != null) sb.Append(usuarioResumo).Append('\n');
        if (campanhaResumo != null) sb.Append(campanhaResumo).Append('\n');
        for (int i = 0; i < albumLinhas.Count && i < 30; i++) sb.Append(albumLinhas[i]).Append('\n');
        sb.Append(catalogoTexto ?? "catalogo: (ainda nao preenchido)").Append('\n');
        sb.Append("cards mostrados: ").Append(cardsMostrados).Append('\n');
        sb.Append("imagens ok/falha: ").Append(imgOk).Append("/").Append(imgFalha).Append('\n');
        sb.Append("videos preparados/erro/fallback: ").Append(vidPreparado).Append("/").Append(vidErro).Append("/").Append(vidFallback).Append('\n');
        sb.Append("pendentes de video: ").Append(pendentes.Count).Append('\n');
        sb.Append("ultimo erro: ").Append(ultimoErro ?? "(nenhum)");
        return sb.ToString();
    }

    static string Cortar(string s, int max)
    {
        if (s == null) return "(null)";
        return s.Length <= max ? s : s.Substring(0, max) + "...(+" + (s.Length - max) + ")";
    }
}