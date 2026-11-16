// MobileTouchControls.cs
// Controles de toque do E_Virtual no WebGL de celular (mesma build do PC).
// - Cria-se sozinho (RuntimeInitializeOnLoadMethod AfterSceneLoad + DontDestroyOnLoad). Nao precisa de cena, prefab nem Inspector.
// - So monta e mostra a UI quando MobileInput.Ativo (celular, ?mobile=1, menu do Editor ou SetMobile).
// - Joystick esquerdo = andar, joystick direito = girar a camera, botao Interagir = clique na mira (centro da tela).
// - Alimenta o StarterAssetsInputs do PlayerCapsule (move/look). O controle do PC continua igual.
// - Esconde sozinho quando nao tem jogador, o jogo esta pausado pelo site, ou um painel/cinematica desliga o look
//   (StarterAssetsInputs.cursorInputForLook = false). Esconder por codigo: MobileInput.ControlesVisiveis / BotaoInteragirVisivel.
// - Site pode forcar: SendMessage("MobileTouchControls", "SetMobile", "1" | "0" | "auto").
// - Ajuste provisorio de sensibilidade da camera (< 1.0 >) no canto de cima: MobileTouchControls.Sensibilidade.cs
// - Letras no centro dos controles (M / C / I): MobileTouchControls.Rotulos.cs
// - Camera do joystick direito gira direto (sem o limite minimo do FirstPersonController): MobileTouchControls.Camera.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StarterAssets;

[DefaultExecutionOrder(-1000)]
public partial class MobileTouchControls : MonoBehaviour
{
    public static MobileTouchControls Instancia { get; private set; }

    [Header("Olhar (joystick direito)")]
    [Tooltip("Graus por segundo girando para os lados com o joystick no maximo (sensibilidade 1.0). Era 150, -30%")]
    public float grausPorSegundoX = 105f;
    [Tooltip("Graus por segundo olhando para cima/baixo com o joystick no maximo (sensibilidade 1.0). Era 100, -30%")]
    public float grausPorSegundoY = 70f;
    [Tooltip("1 = linear. Maior que 1 = mais precisao perto do centro")]
    public float curvaResposta = 1.6f;
    public bool inverterY = false;

    [Header("Visual")]
    public Color corBase = new Color(1f, 1f, 1f, 0.16f);
    public Color corAnel = new Color(1f, 1f, 1f, 0.45f);
    public Color corKnob = new Color(1f, 1f, 1f, 0.45f);
    public Color corKnobAtivo = new Color(1f, 1f, 1f, 0.75f);
    public Color corBotao = new Color(1f, 1f, 1f, 0.22f);
    public Color corBotaoAtivo = new Color(1f, 1f, 1f, 0.5f);

    public bool logNoConsole = true;

    // jogador
    FirstPersonController fpc;
    StarterAssetsInputs sai;
#if ENABLE_INPUT_SYSTEM
    UnityEngine.InputSystem.PlayerInput playerInput;
#endif
    float proximaBusca;
    bool escrevendoMove, escrevendoLook, analogForcado, analogOriginal;
    bool estavaAtivo;

    // UI
    Canvas canvas;
    RectTransform baseE, anelE, knobE, baseD, anelD, knobD, botao, botaoAnel, botaoIcone;
    Image imgKnobE, imgKnobD, imgBotao;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetarDominio()
    {
        Instancia = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        Garantir();
    }

    public static void Garantir()
    {
        if (Instancia != null) return;
        var go = new GameObject("MobileTouchControls");
        DontDestroyOnLoad(go);
        Instancia = go.AddComponent<MobileTouchControls>();
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        SceneManager.sceneLoaded += AoCarregarCena;
        if (logNoConsole) Debug.Log("[MobileTouch] ativo=" + MobileInput.Ativo + " (" + MobileInput.Motivo + ")");
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        if (Instancia == this) Instancia = null;
    }

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        LiberarJogador();
        fpc = null;
        sai = null;
#if ENABLE_INPUT_SYSTEM
        playerInput = null;
#endif
        proximaBusca = 0f;
        MobileInput.ResetarEstado();
    }

    /// <summary>Para o site: SendMessage("MobileTouchControls", "SetMobile", "1" | "0" | "auto").</summary>
    public void SetMobile(string valor)
    {
        string v = (valor ?? "").Trim().ToLowerInvariant();
        if (v == "1" || v == "true" || v == "sim" || v == "on") MobileInput.Forcar(true);
        else if (v == "0" || v == "false" || v == "nao" || v == "off") MobileInput.Forcar(false);
        else MobileInput.Forcar(null);
        if (logNoConsole) Debug.Log("[MobileTouch] SetMobile(" + valor + ") -> ativo=" + MobileInput.Ativo + " (" + MobileInput.Motivo + ")");
    }

    void Update()
    {
        bool ativo = MobileInput.Ativo;
        if (ativo != estavaAtivo)
        {
            estavaAtivo = ativo;
            if (logNoConsole) Debug.Log("[MobileTouch] ativo=" + ativo + " (" + MobileInput.Motivo + ")");
            if (!ativo) LiberarJogador();
        }

        if (!ativo)
        {
            MobileInput.JogadorPronto = false;
            if (canvas != null && canvas.enabled) canvas.enabled = false;
            return;
        }

        if (canvas == null) ConstruirUI();
        BuscarJogador();
        MobileInput.JogadorPronto = JogadorControlavel();
        MobileInput.Processar();
        AplicarNoJogador();
    }

    void LateUpdate()
    {
        if (!MobileInput.Ativo || canvas == null) return;
        ZerarLookAntesDoFPC(); // o arraste do dedo (Pointer delta) nao pode girar a camera por fora do joystick

        // Mouse "livre" para tocar/clicar nos controles (no celular nao existe trava de ponteiro;
        // no Editor o mouse faz o papel do dedo). So quando o jogador esta no controle.
        if (MobileInput.JogadorPronto && (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        AtualizarVisual();
    }

    // ------------------------------------------------------------------ jogador
    void BuscarJogador()
    {
        if (fpc != null && sai != null) return;
        if (Time.unscaledTime < proximaBusca) return;
        proximaBusca = Time.unscaledTime + 0.5f;

        fpc = FindAnyObjectByType<FirstPersonController>();
        if (fpc == null) { sai = null; return; }
        sai = fpc.GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
        playerInput = fpc.GetComponent<UnityEngine.InputSystem.PlayerInput>();
#endif
        analogForcado = false;
        if (logNoConsole) Debug.Log("[MobileTouch] jogador encontrado: " + fpc.name);
    }

    bool JogadorControlavel()
    {
        return fpc != null && fpc.isActiveAndEnabled
            && sai != null && sai.isActiveAndEnabled
            && sai.cursorInputForLook
            && !StarterAssetsInputs.GamePaused;
    }

    void AplicarNoJogador()
    {
        if (!MobileInput.JogadorPronto) { LiberarJogador(); return; }

        if (!analogForcado)
        {
            analogOriginal = sai.analogMovement;
            sai.analogMovement = true; // velocidade proporcional ao quanto o joystick foi empurrado
            analogForcado = true;
        }

        // Andar: so escreve enquanto o joystick esta em uso (e zera uma vez ao soltar),
        // para nao brigar com teclado/autopiloto.
        if (MobileInput.Esquerdo.Ativo)
        {
            sai.MoveInput(MobileInput.Mover);
            escrevendoMove = true;
        }
        else if (escrevendoMove)
        {
            sai.MoveInput(Vector2.zero);
            escrevendoMove = false;
        }

        // Olhar: so o joystick direito gira a camera. Com ele solto, zera todo frame para o arraste
        // do dedo (Pointer/delta) ou o mouse no Editor nao girarem a camera sem querer.
        if (MobileInput.Direito.Ativo)
        {
            if (GirarCameraDireto(MobileInput.Olhar)) sai.LookInput(Vector2.zero); // gira aqui mesmo, suave em qualquer sensibilidade
            else sai.LookInput(CalcularLook(MobileInput.Olhar));
            escrevendoLook = true;
        }
        else
        {
            sai.LookInput(Vector2.zero);
            escrevendoLook = false;
        }
    }

    Vector2 CalcularLook(Vector2 v)
    {
        // O FirstPersonController faz: angulo = look * RotationSpeed * (mouse ? 1 : deltaTime).
        // Aqui o resultado final fica em graus/segundo, independente do esquema de controle atual.
        Vector2 grausPorSeg = GrausPorSegundoOlhar(v);
        if (grausPorSeg == Vector2.zero) return Vector2.zero;
        float rs = fpc != null ? fpc.RotationSpeed : 1f;
        if (Mathf.Abs(rs) < 0.0001f) rs = 1f;
        Vector2 look = grausPorSeg / rs;

        bool esquemaMouse = false;
#if ENABLE_INPUT_SYSTEM
        if (playerInput != null) esquemaMouse = playerInput.currentControlScheme == "KeyboardMouse";
#endif
        if (esquemaMouse) look *= Time.deltaTime;
        return look;
    }

    void LiberarJogador()
    {
        if (sai != null)
        {
            if (escrevendoMove) sai.MoveInput(Vector2.zero);
            if (escrevendoLook) sai.LookInput(Vector2.zero);
            if (analogForcado) sai.analogMovement = analogOriginal;
        }
        escrevendoMove = false;
        escrevendoLook = false;
        analogForcado = false;
    }

    // ------------------------------------------------------------------ UI
    void ConstruirUI()
    {
        var go = new GameObject("MobileTouchCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>(); // bloqueia botoes de outras UIs que estejam debaixo dos controles

        Sprite cheio = CriarSpriteCirculo(256, 0f);
        Sprite anel = CriarSpriteCirculo(256, 0.035f);

        baseE = CriarImagem("JoystickEsquerdo_Base", go.transform, cheio, corBase, true, out _);
        anelE = CriarImagem("JoystickEsquerdo_Anel", go.transform, anel, corAnel, false, out _);
        knobE = CriarImagem("JoystickEsquerdo_Knob", go.transform, cheio, corKnob, false, out imgKnobE);

        baseD = CriarImagem("JoystickDireito_Base", go.transform, cheio, corBase, true, out _);
        anelD = CriarImagem("JoystickDireito_Anel", go.transform, anel, corAnel, false, out _);
        knobD = CriarImagem("JoystickDireito_Knob", go.transform, cheio, corKnob, false, out imgKnobD);

        botao = CriarImagem("BotaoInteragir", go.transform, cheio, corBotao, true, out imgBotao);
        botaoAnel = CriarImagem("BotaoInteragir_Anel", botao, anel, corAnel, false, out _);
        botaoIcone = CriarImagem("BotaoInteragir_Icone", botao, cheio, new Color(1f, 1f, 1f, 0.85f), false, out _);
        Esticar(botaoAnel, 1f);
        Esticar(botaoIcone, 0.28f);

        ConstruirRotulos();
        ConstruirAjusteSensibilidade(go.transform);

        canvas.enabled = false;
    }

    static RectTransform CriarImagem(string nome, Transform pai, Sprite sprite, Color cor, bool raycast, out Image img)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = cor;
        img.raycastTarget = raycast;
        img.preserveAspect = true;
        return rt;
    }

    static void Esticar(RectTransform rt, float fracao)
    {
        float m = (1f - fracao) * 0.5f;
        rt.anchorMin = new Vector2(m, m);
        rt.anchorMax = new Vector2(1f - m, 1f - m);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Sprite CriarSpriteCirculo(int tam, float espessuraAnel)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.name = espessuraAnel > 0f ? "MobileTouch_Anel" : "MobileTouch_Circulo";
        var px = new Color32[tam * tam];
        float c = (tam - 1) * 0.5f;
        float r = tam * 0.5f - 1.5f;
        float rInt = espessuraAnel > 0f ? r - espessuraAnel * tam : -1f;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (rInt > 0f) a *= Mathf.Clamp01(d - rInt + 0.5f);
                px[y * tam + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0f, 0f, tam, tam), new Vector2(0.5f, 0.5f), 100f);
    }

    void AtualizarVisual()
    {
        bool naTela = MobileInput.ControlesNaTela;
        if (canvas.enabled != naTela) canvas.enabled = naTela;
        if (!naTela) return;

        float sf = canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;
        float e = MobileInput.Escala;

        MobileVirtualJoystick je = MobileInput.Esquerdo, jd = MobileInput.Direito;
        Posicionar(baseE, je.Centro, MobileInput.RaioBase * 2f * e, sf);
        Posicionar(anelE, je.Centro, MobileInput.RaioBase * 2f * e, sf);
        Posicionar(knobE, je.Centro + je.OffsetKnob, MobileInput.RaioKnob * 2f * e, sf);
        imgKnobE.color = je.Ativo ? corKnobAtivo : corKnob;

        Posicionar(baseD, jd.Centro, MobileInput.RaioBase * 2f * e, sf);
        Posicionar(anelD, jd.Centro, MobileInput.RaioBase * 2f * e, sf);
        Posicionar(knobD, jd.Centro + jd.OffsetKnob, MobileInput.RaioKnob * 2f * e, sf);
        imgKnobD.color = jd.Ativo ? corKnobAtivo : corKnob;

        bool mostrarBotao = MobileInput.BotaoNaTela;
        if (botao.gameObject.activeSelf != mostrarBotao) botao.gameObject.SetActive(mostrarBotao);
        if (mostrarBotao)
        {
            bool apertado = MobileInput.InteragirSegurado;
            float d = MobileInput.BotaoRaioPx * 2f * (apertado ? 0.92f : 1f);
            Posicionar(botao, MobileInput.BotaoCentro, d, sf);
            imgBotao.color = apertado ? corBotaoAtivo : corBotao;
        }

        AtualizarRotulos();
        AtualizarAjusteSensibilidade(sf);
    }

    static void Posicionar(RectTransform rt, Vector2 centroPx, float diametroPx, float sf)
    {
        rt.anchoredPosition = centroPx / sf;
        rt.sizeDelta = new Vector2(diametroPx / sf, diametroPx / sf);
    }
}
