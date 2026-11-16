// MobileTela.cs
// Tela no celular (WebGL, mesma build do PC):
// 1) Tela cheia + travar na horizontal no PRIMEIRO toque na pagina (o navegador so deixa dentro de um gesto do usuario).
//    O ouvinte de toque fica no JavaScript (Assets/Plugins/WebGL/MobileTela.jslib), nao no Update do Unity,
//    para rodar dentro do gesto de verdade. Se o usuario sair da tela cheia, arma de novo para o proximo toque.
//    - Android Chrome: tela cheia (some a barra de endereco) e trava na horizontal.
//    - iPad Safari: tela cheia com webkitRequestFullscreen; nao trava a orientacao (o iPad nao deixa).
//    - iPhone Safari: nao existe tela cheia de pagina nem trava de orientacao; so o aviso abaixo.
// 2) Celular em pe (Screen.height > Screen.width): mostra "Gire o celular para jogar na horizontal" e esconde os
//    controles de toque (MobileInput.TelaBloqueada) enquanto o aviso estiver na tela.
// - Cria-se sozinho quando MobileInput.Ativo (igual ao MobileTouchControls). No PC nada acontece.
// - No Editor nao chama JavaScript (so o aviso, util com "Simular controles touch" e Game view em pe).
// - Desligar por codigo: MobileTela.TelaCheiaAutomatica / TravarHorizontal / AvisoGirarCelular = false.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

[DefaultExecutionOrder(-1100)]
public class MobileTela : MonoBehaviour
{
    /// <summary>Pede tela cheia no primeiro toque (Android / iPad).</summary>
    public static bool TelaCheiaAutomatica = true;
    /// <summary>Trava na horizontal depois de entrar em tela cheia (so Android Chrome aceita).</summary>
    public static bool TravarHorizontal = true;
    /// <summary>Mostra o aviso "gire o celular" quando a tela esta em pe.</summary>
    public static bool AvisoGirarCelular = true;
    /// <summary>
    /// true = tela cheia/trava so em celular/tablet de verdade (Application.isMobilePlatform).
    /// Assim ?mobile=1 no PC (teste) nao entra em tela cheia. O aviso de girar vale sempre que MobileInput.Ativo.
    /// </summary>
    public static bool SoEmCelularDeVerdade = true;
    public static bool LogNoConsole = true;

    public static MobileTela Instancia { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void MobileTela_Armar(int telaCheia, int travar, int log);
    [DllImport("__Internal")] static extern void MobileTela_Desarmar();
    [DllImport("__Internal")] static extern int MobileTela_EmTelaCheia();
#endif

    bool armado;
    bool avisoNaTela;
    bool? idiomaMostrado;
    Canvas canvasAviso;
    Text textoAviso;

    static bool inscrito;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetarDominio()
    {
        Instancia = null;
        inscrito = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (!inscrito)
        {
            inscrito = true;
            MobileInput.AoMudarAtivo += AoMudarAtivo;
        }
        if (MobileInput.Ativo) Garantir();
    }

    static void AoMudarAtivo(bool ativo)
    {
        if (ativo) Garantir();
    }

    public static void Garantir()
    {
        if (Instancia != null) return;
        var go = new GameObject("MobileTela");
        DontDestroyOnLoad(go);
        Instancia = go.AddComponent<MobileTela>();
    }

    /// <summary>True se a pagina esta em tela cheia agora (sempre false no Editor).</summary>
    public static bool EmTelaCheia
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return MobileTela_EmTelaCheia() != 0; } catch (Exception) { return false; }
#else
            return false;
#endif
        }
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        if (LogNoConsole) Debug.Log("[MobileTela] criado (ativo=" + MobileInput.Ativo + ", celular=" + Application.isMobilePlatform + ")");
    }

    void OnDestroy()
    {
        if (Instancia != this) return;
        Desarmar();
        MobileInput.TelaBloqueada = false;
        Instancia = null;
    }

    void Update()
    {
        bool ativo = MobileInput.Ativo;

        // tela cheia + horizontal (no JavaScript, no proximo toque)
        bool querArmar = ativo
            && (TelaCheiaAutomatica || TravarHorizontal)
            && (!SoEmCelularDeVerdade || Application.isMobilePlatform);
        if (querArmar && !armado) Armar();
        else if (!querArmar && armado) Desarmar();

        // aviso de girar o celular
        bool retrato = ativo && AvisoGirarCelular && Screen.height > Screen.width;
        if (retrato != avisoNaTela)
        {
            avisoNaTela = retrato;
            if (LogNoConsole) Debug.Log("[MobileTela] " + (retrato ? "tela em pe: aviso de girar ligado" : "tela deitada: aviso desligado") + " (" + Screen.width + "x" + Screen.height + ")");
        }
        MobileInput.TelaBloqueada = retrato;

        if (retrato && canvasAviso == null) ConstruirAviso();
        if (canvasAviso != null)
        {
            if (canvasAviso.enabled != retrato) canvasAviso.enabled = retrato;
            if (retrato) AtualizarTextoAviso();
        }
    }

    void Armar()
    {
        armado = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            MobileTela_Armar(TelaCheiaAutomatica ? 1 : 0, TravarHorizontal ? 1 : 0, LogNoConsole ? 1 : 0);
            if (LogNoConsole) Debug.Log("[MobileTela] armado: tela cheia=" + TelaCheiaAutomatica + ", travar horizontal=" + TravarHorizontal + " (no proximo toque)");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[MobileTela] nao consegui armar: " + e.Message);
        }
#else
        if (LogNoConsole) Debug.Log("[MobileTela] (Editor) tela cheia/horizontal so funcionam no WebGL do celular");
#endif
    }

    void Desarmar()
    {
        if (!armado) return;
        armado = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        try { MobileTela_Desarmar(); } catch (Exception) { }
#endif
        if (LogNoConsole) Debug.Log("[MobileTela] desarmado");
    }

    // ------------------------------------------------------------------ aviso
    void ConstruirAviso()
    {
        var go = new GameObject("MobileTela_AvisoGirar", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvasAviso = go.AddComponent<Canvas>();
        canvasAviso.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasAviso.sortingOrder = 1000; // acima dos controles de toque (900)
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080f, 1920f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>(); // bloqueia botoes de outras UIs enquanto o aviso estiver na tela

        var fundo = new GameObject("Fundo", typeof(RectTransform));
        fundo.transform.SetParent(go.transform, false);
        var rtF = (RectTransform)fundo.transform;
        rtF.anchorMin = Vector2.zero;
        rtF.anchorMax = Vector2.one;
        rtF.offsetMin = rtF.offsetMax = Vector2.zero;
        var img = fundo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.88f);
        img.raycastTarget = true;

        var txt = new GameObject("Texto", typeof(RectTransform));
        txt.transform.SetParent(fundo.transform, false);
        var rtT = (RectTransform)txt.transform;
        rtT.anchorMin = new Vector2(0.1f, 0.3f);
        rtT.anchorMax = new Vector2(0.9f, 0.7f);
        rtT.offsetMin = rtT.offsetMax = Vector2.zero;
        textoAviso = txt.AddComponent<Text>();
        Font f = Fonte();
        if (f != null) textoAviso.font = f;
        textoAviso.alignment = TextAnchor.MiddleCenter;
        textoAviso.color = Color.white;
        textoAviso.fontStyle = FontStyle.Bold;
        textoAviso.fontSize = 64;
        textoAviso.resizeTextForBestFit = true;
        textoAviso.resizeTextMinSize = 18;
        textoAviso.resizeTextMaxSize = 72;
        textoAviso.horizontalOverflow = HorizontalWrapMode.Wrap;
        textoAviso.verticalOverflow = VerticalWrapMode.Truncate;
        textoAviso.raycastTarget = false;
        idiomaMostrado = null;
        AtualizarTextoAviso();

        canvasAviso.enabled = false;
    }

    void AtualizarTextoAviso()
    {
        if (textoAviso == null) return;
        bool ingles = Idioma.Ingles;
        if (idiomaMostrado.HasValue && idiomaMostrado.Value == ingles) return;
        idiomaMostrado = ingles;
        textoAviso.text = ingles ? "Rotate your phone to landscape" : "Gire o celular para jogar na horizontal";
    }

    static Font Fonte()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { f = null; }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { f = null; }
        }
        return f;
    }
}
