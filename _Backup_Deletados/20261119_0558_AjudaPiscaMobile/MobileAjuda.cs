// MobileAjuda.cs
// Tela de ajuda "Como jogar" do celular (WebGL, mesma build do PC).
// - Cria-se sozinho quando MobileInput.Ativo (igual ao MobileTela). No PC nada acontece e nada e criado.
//   Nao precisa de cena, prefab nem Inspector. (Se alguem colocar um MobileAjuda numa cena, ele e usado no lugar.)
// - Botao redondo "?" no canto de baixo a direita, no estilo dos circulos M / C / I, acima do controle C:
//   longe do botao I (Interagir), da area do controle C e do texto "DESAFIOS 0/5". Os cantos de cima ficam livres
//   para os icones do site (logo, perfil, link, PT/EN).
// - Tocar no "?" abre a tela por cima do jogo: titulo, texto com rolagem (arrastar o dedo), botao X e botao Fechar.
//   Com a ajuda aberta os controles somem e nenhum toque chega no jogo (MobileInput.AjudaAberta, ver MobileInput.Ajuda.cs).
// - Textos em PT e EN; segue o idioma do jogo (Idioma.Ingles, inclusive se o site trocar com a ajuda aberta).
// - Textos, posicao e tamanho: campos abaixo (valores padrao no codigo; no Play da para mexer no objeto "MobileAjuda").
// - Por codigo: MobileAjuda.AbrirAjuda() / MobileAjuda.FecharAjuda().
// - Editor: menu Mobile > Tools > Simular controles touch no Play (o mouse faz o papel do dedo; roda do mouse rola o texto).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-990)] // logo depois do MobileTouchControls (-1000), antes dos puzzles
public class MobileAjuda : MonoBehaviour
{
    [Serializable]
    public class Secao
    {
        [Tooltip("Letras dos circulos ao lado do texto, separadas por espaco (ex.: \"M\", \"C I\").")]
        public string icones = "";
        public string tituloPT = "";
        [TextArea(2, 6)] public string textoPT = "";
        public string tituloEN = "";
        [TextArea(2, 6)] public string textoEN = "";

        public Secao() { }
        public Secao(string icones, string tituloPT, string textoPT, string tituloEN, string textoEN)
        {
            this.icones = icones;
            this.tituloPT = tituloPT;
            this.textoPT = textoPT;
            this.tituloEN = tituloEN;
            this.textoEN = textoEN;
        }
    }

    public const string PrefAjudaVista = "EVirtual.Mobile.AjudaVista";

    public static MobileAjuda Instancia { get; private set; }

    [Header("Opcoes")]
    [Tooltip("Mostra o botao \"?\" no celular.")]
    public bool MostrarBotaoAjuda = true;
    [Tooltip("Abre a ajuda sozinha na primeira vez que o jogador fica no controle pelo celular (lembra no PlayerPrefs).")]
    public bool AbrirAjudaNaPrimeiraVez = false;

    [Header("Botao \"?\" (unidades do canvas 1920x1080, a partir do canto inferior direito)")]
    [Tooltip("Centro do botao. Botao I = (500, 480) e controle C = (250, 250) com area de toque de raio 208: (100, 490) fica livre dos dois e bem acima do texto DESAFIOS.")]
    public Vector2 posicaoBotao = new Vector2(100f, 490f);
    [Tooltip("Raio do botao (o I tem 88).")]
    public float raioBotao = 52f;
    public string textoBotao = "?";

    [Header("Visual (igual aos controles de toque)")]
    public Color corBotao = new Color(1f, 1f, 1f, 0.22f);
    public Color corBotaoAtivo = new Color(1f, 1f, 1f, 0.5f);
    public Color corAnel = new Color(1f, 1f, 1f, 0.45f);
    public Color corLetra = new Color(1f, 1f, 1f, 0.85f);
    public Color corFundo = new Color(0f, 0f, 0f, 0.72f);
    public Color corPainel = new Color(0.07f, 0.09f, 0.13f, 0.95f);
    public Color corTexto = new Color(1f, 1f, 1f, 0.95f);
    public Color corBotaoFechar = new Color(1f, 1f, 1f, 0.16f);
    [Tooltip("Fonte TMP. Vazio = procura a Barlow carregada no jogo; senao usa a fonte padrao do TextMeshPro.")]
    public TMP_FontAsset fonte;

    [Header("Tamanhos (unidades do canvas 1920x1080)")]
    public float tamanhoTitulo = 58f;
    public float tamanhoTexto = 40f;
    public float tamanhoIcone = 88f;
    public float espacoEntreSecoes = 34f;

    [Header("Textos")]
    public string tituloPT = "Como jogar";
    public string tituloEN = "How to play";
    public string fecharPT = "Fechar";
    public string fecharEN = "Close";
    public List<Secao> secoes = SecoesPadrao();

    public static List<Secao> SecoesPadrao()
    {
        return new List<Secao>
        {
            new Secao("M", "Mover (M)",
                "Arraste o círculo M (lado esquerdo da tela) para andar.",
                "Move (M)",
                "Drag the M circle (left side of the screen) to walk."),
            new Secao("C", "Olhar (C)",
                "Arraste o círculo C (lado direito da tela) para olhar em volta.",
                "Look (C)",
                "Drag the C circle (right side of the screen) to look around."),
            new Secao("I", "Interagir (I)",
                "Chegue perto de um objeto, mire nele (centro da tela) e toque no I.",
                "Interact (I)",
                "Get close to an object, aim at it (center of the screen) and tap I."),
            new Secao("C I", "Cadeado",
                "Mire em cada roda de números do cadeado movendo a câmera com o controle C e toque no I (Interagir): cada toque gira um número. Repita até achar a combinação.",
                "Padlock",
                "Aim at each number wheel of the padlock by moving the camera with the C control and tap I (Interact): each tap turns it one number. Repeat until you find the combination."),
            new Secao("C I", "Varal e quebra-cabeça",
                "Mire na peça e toque no I (Interagir) para pegar. A peça segue o centro da tela: mova a câmera com o controle C até o lugar certo e toque no I de novo para soltar. Se a peça estiver no lugar certo, ela encaixa sozinha. No varal, soltar uma camiseta em cima de outra troca as duas de lugar.",
                "Clothesline and puzzles",
                "Aim at a piece and tap I (Interact) to pick it up. The piece follows the center of the screen: move the camera with the C control to the right spot and tap I again to drop it. If the piece is in the right spot, it snaps into place by itself. On the clothesline, dropping a shirt onto another one swaps them."),
        };
    }

    public bool logNoConsole = true;

    // ------------------------------------------------------------------ estado
    bool aberta;
    bool primeiraVezChecada;
    bool? idiomaMostrado;
    float larguraMontada = -1f;
    bool precisaMontar = true;

    float rolagem, velocidade, alturaConteudo;
    bool dedoNaAjuda, ehToque, comecouNoX, comecouNoFechar;
    Vector2 inicioDedo, ultimoDedo;

    // UI
    Canvas canvas;
    RectTransform botao, painel, cartao, viewport, conteudo, barraTrilho, barraAlca, botaoX, botaoFechar;
    Image imgBotao, imgX, imgFechar;
    TextMeshProUGUI txtBotao, txtTitulo, txtFechar;
    Sprite spriteCirculo, spriteAnel, spriteArredondado;
    readonly List<RectTransform> linhas = new List<RectTransform>();
    readonly List<TextMeshProUGUI> textosLinhas = new List<TextMeshProUGUI>();

    static bool inscrito;

    // ------------------------------------------------------------------ criacao
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
            SceneManager.sceneLoaded += AoCarregarCena;
        }
        if (MobileInput.Ativo) Garantir();
    }

    static void AoMudarAtivo(bool ativo) { if (ativo) Garantir(); }
    static void AoCarregarCena(Scene cena, LoadSceneMode modo) { if (MobileInput.Ativo) Garantir(); }

    public static void Garantir()
    {
        if (Instancia != null) return;
        var existente = FindAnyObjectByType<MobileAjuda>();
        if (existente != null) { Instancia = existente; return; }
        var go = new GameObject("MobileAjuda");
        DontDestroyOnLoad(go);
        Instancia = go.AddComponent<MobileAjuda>();
    }

    /// <summary>Abre a ajuda (so no mobile).</summary>
    public static void AbrirAjuda() { if (MobileInput.Ativo) { Garantir(); if (Instancia != null) Instancia.Abrir(); } }
    /// <summary>Fecha a ajuda.</summary>
    public static void FecharAjuda() { if (Instancia != null) Instancia.Fechar(); }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(this); return; }
        Instancia = this;
        if (logNoConsole) Debug.Log("[MobileAjuda] criado (ativo=" + MobileInput.Ativo + ")");
    }

    void OnDestroy()
    {
        if (Instancia != this) return;
        MobileInput.AjudaAberta = false;
        MobileInput.BotaoAjudaLigado = false;
        Instancia = null;
    }

    // ------------------------------------------------------------------ abrir / fechar
    public void Abrir()
    {
        if (!MobileInput.Ativo) return;
        if (canvas == null) ConstruirUI();
        aberta = true;
        MobileInput.AjudaAberta = true;
        rolagem = 0f;
        velocidade = 0f;
        dedoNaAjuda = false;
        precisaMontar = true;
        painel.gameObject.SetActive(true);
        try { PlayerPrefs.SetInt(PrefAjudaVista, 1); PlayerPrefs.Save(); } catch (Exception) { }
        if (logNoConsole) Debug.Log("[MobileAjuda] aberta");
    }

    public void Fechar()
    {
        if (!aberta) return;
        aberta = false;
        MobileInput.AjudaAberta = false;
        dedoNaAjuda = false;
        if (painel != null) painel.gameObject.SetActive(false);
        if (logNoConsole) Debug.Log("[MobileAjuda] fechada");
    }

    // ------------------------------------------------------------------ frame
    void Update()
    {
        if (!MobileInput.Ativo)
        {
            if (aberta) Fechar();
            MobileInput.BotaoAjudaLigado = false;
            if (canvas != null && canvas.enabled) canvas.enabled = false;
            return;
        }

        // Inspector -> entrada (posicao do botao e se ele existe)
        MobileInput.PosicaoBotaoAjuda = posicaoBotao;
        MobileInput.RaioBotaoAjuda = raioBotao;
        MobileInput.BotaoAjudaLigado = MostrarBotaoAjuda;

        MobileInput.Processar();

        if (!aberta)
        {
            if (MobileInput.TocouBotaoAjudaNoFrame) Abrir();
            else if (AbrirAjudaNaPrimeiraVez && !primeiraVezChecada && MobileInput.ControlesNaTela)
            {
                primeiraVezChecada = true;
                int vista = 0;
                try { vista = PlayerPrefs.GetInt(PrefAjudaVista, 0); } catch (Exception) { vista = 0; }
                if (vista == 0) Abrir();
            }
        }
        else
        {
            MobileInput.AjudaAberta = true;
        }

        bool mostrarBotao = MobileInput.BotaoAjudaNaTela;
        bool mostrar = aberta || mostrarBotao;
        if (!mostrar)
        {
            if (canvas != null && canvas.enabled) canvas.enabled = false;
            return;
        }
        if (canvas == null) ConstruirUI();
        if (!canvas.enabled) canvas.enabled = true;

        float sf = canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;

        if (botao.gameObject.activeSelf != mostrarBotao) botao.gameObject.SetActive(mostrarBotao);
        if (mostrarBotao)
        {
            float d = MobileInput.BotaoAjudaRaioPx * 2f / sf;
            botao.anchoredPosition = MobileInput.BotaoAjudaCentro / sf;
            botao.sizeDelta = new Vector2(d, d);
            imgBotao.color = Time.unscaledTime - MobileInput.UltimoToqueAjuda < 0.15f ? corBotaoAtivo : corBotao;
            if (txtBotao.text != textoBotao) txtBotao.text = textoBotao;
        }

        if (aberta) AtualizarPainel(sf);
    }

    // ------------------------------------------------------------------ painel
    void AtualizarPainel(float sf)
    {
        bool ingles = Idioma.Ingles;
        if (!idiomaMostrado.HasValue || idiomaMostrado.Value != ingles)
        {
            idiomaMostrado = ingles;
            precisaMontar = true;
        }
        float largura = viewport.rect.width;
        if (largura > 1f && (precisaMontar || Mathf.Abs(largura - larguraMontada) > 0.5f)) Montar(largura, ingles);

        // toques (so o dedo da ajuda chega aqui)
        MobileInput.ToqueAjuda t = MobileInput.ToqueNaAjuda;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        if (t.ativo && t.comecou)
        {
            dedoNaAjuda = true;
            ehToque = true;
            inicioDedo = ultimoDedo = t.pos;
            velocidade = 0f;
            comecouNoX = Contem(botaoX, t.pos);
            comecouNoFechar = Contem(botaoFechar, t.pos);
        }
        else if (t.ativo && dedoNaAjuda)
        {
            float dy = (t.pos.y - ultimoDedo.y) / sf;
            ultimoDedo = t.pos;
            float lim = Mathf.Min(Screen.width, Screen.height) * 0.03f;
            if ((t.pos - inicioDedo).sqrMagnitude > lim * lim) ehToque = false;
            if (!ehToque)
            {
                rolagem += dy;
                velocidade = Mathf.Lerp(velocidade, dy / dt, 0.5f);
            }
        }
        if (t.terminou && dedoNaAjuda)
        {
            dedoNaAjuda = false;
            if (ehToque)
            {
                if ((comecouNoX && Contem(botaoX, t.pos)) || (comecouNoFechar && Contem(botaoFechar, t.pos)))
                {
                    comecouNoX = comecouNoFechar = false;
                    Fechar();
                    return;
                }
                velocidade = 0f;
            }
            comecouNoX = comecouNoFechar = false;
        }
        if (!t.ativo && dedoNaAjuda) dedoNaAjuda = false; // dedo sumiu sem "terminou"

#if ENABLE_LEGACY_INPUT_MANAGER
        // roda do mouse (Editor / PC com ?mobile=1)
        try
        {
            float roda = Input.mouseScrollDelta.y;
            if (Mathf.Abs(roda) > 0.01f) { rolagem -= roda * 80f; velocidade = 0f; }
        }
        catch (Exception) { }
#endif

        // inercia
        if (!dedoNaAjuda && Mathf.Abs(velocidade) > 1f)
        {
            rolagem += velocidade * dt;
            velocidade *= Mathf.Exp(-5f * dt);
        }

        float max = Mathf.Max(0f, alturaConteudo - viewport.rect.height);
        if (rolagem < 0f) { rolagem = 0f; velocidade = 0f; }
        else if (rolagem > max) { rolagem = max; velocidade = 0f; }
        conteudo.anchoredPosition = new Vector2(0f, rolagem);

        // barra de rolagem (so aparece se o texto nao couber)
        bool precisaBarra = max > 1f;
        if (barraTrilho.gameObject.activeSelf != precisaBarra) barraTrilho.gameObject.SetActive(precisaBarra);
        if (precisaBarra)
        {
            float frac = Mathf.Clamp01(viewport.rect.height / Mathf.Max(1f, alturaConteudo));
            float pos = max > 0f ? rolagem / max : 0f;
            float topo = 1f - (1f - frac) * pos;
            barraAlca.anchorMin = new Vector2(0f, topo - frac);
            barraAlca.anchorMax = new Vector2(1f, topo);
            barraAlca.offsetMin = barraAlca.offsetMax = Vector2.zero;
        }

        bool apertandoX = dedoNaAjuda && ehToque && comecouNoX;
        bool apertandoFechar = dedoNaAjuda && ehToque && comecouNoFechar;
        imgX.color = apertandoX ? corBotaoAtivo : corBotaoFechar;
        imgFechar.color = apertandoFechar ? corBotaoAtivo : corBotaoFechar;
    }

    static bool Contem(RectTransform rt, Vector2 telaPx)
    {
        return rt != null && rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rt, telaPx, null);
    }

    /// <summary>Monta as secoes (icones + texto) para a largura atual do viewport.</summary>
    void Montar(float largura, bool ingles)
    {
        precisaMontar = false;
        larguraMontada = largura;
        txtTitulo.text = ingles ? tituloEN : tituloPT;
        txtFechar.text = ingles ? fecharEN : fecharPT;

        for (int i = 0; i < linhas.Count; i++) if (linhas[i] != null) Destroy(linhas[i].gameObject);
        linhas.Clear();
        textosLinhas.Clear();

        float ic = Mathf.Max(20f, tamanhoIcone);
        float gap = ic * 0.14f;
        float colunaIcones = ic * 2f + gap;
        float xTexto = colunaIcones + 36f;
        float larguraTexto = Mathf.Max(100f, largura - xTexto - 10f);

        float y = 10f;
        if (secoes != null)
        {
            for (int s = 0; s < secoes.Count; s++)
            {
                Secao sec = secoes[s];
                if (sec == null) continue;
                string titulo = ingles ? sec.tituloEN : sec.tituloPT;
                string texto = ingles ? sec.textoEN : sec.textoPT;
                if (string.IsNullOrEmpty(titulo) && string.IsNullOrEmpty(texto)) continue;

                var linhaGo = new GameObject("Secao_" + s, typeof(RectTransform));
                linhaGo.transform.SetParent(conteudo, false);
                var linha = (RectTransform)linhaGo.transform;
                linha.anchorMin = new Vector2(0f, 1f);
                linha.anchorMax = new Vector2(1f, 1f);
                linha.pivot = new Vector2(0.5f, 1f);

                // icones (circulos com letra), centralizados na coluna
                string[] letras = (sec.icones ?? "").Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                int n = Mathf.Min(letras.Length, 2);
                float larguraIcones = n * ic + Mathf.Max(0, n - 1) * gap;
                float x0 = (colunaIcones - larguraIcones) * 0.5f;
                for (int k = 0; k < n; k++) CriarIcone(linha, letras[k], new Vector2(x0 + k * (ic + gap), 0f), ic);

                // texto: titulo em negrito + explicacao
                string corpo = string.IsNullOrEmpty(titulo) ? texto : (string.IsNullOrEmpty(texto) ? "<b>" + titulo + "</b>" : "<b>" + titulo + "</b>\n" + texto);
                var txt = CriarTMP("Texto", linha, corpo, tamanhoTexto, TextAlignmentOptions.TopLeft, false);
                float alturaTexto = txt.GetPreferredValues(corpo, larguraTexto, 0f).y;
                float h = Mathf.Max(ic, alturaTexto) + 4f;
                var trt = txt.rectTransform;
                trt.anchorMin = new Vector2(0f, 1f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.offsetMin = new Vector2(xTexto, -h);
                trt.offsetMax = new Vector2(-10f, 0f);

                linha.sizeDelta = new Vector2(0f, h);
                linha.anchoredPosition = new Vector2(0f, -y);
                linhas.Add(linha);
                textosLinhas.Add(txt);
                y += h + espacoEntreSecoes;
            }
        }
        alturaConteudo = y + 10f;
        conteudo.sizeDelta = new Vector2(0f, alturaConteudo);
    }

    void CriarIcone(RectTransform pai, string letra, Vector2 posTopoEsquerda, float d)
    {
        var fundo = CriarImagem("Icone_" + letra, pai, spriteCirculo, corBotao, false, out _);
        fundo.anchorMin = fundo.anchorMax = new Vector2(0f, 1f);
        fundo.pivot = new Vector2(0f, 1f);
        fundo.anchoredPosition = new Vector2(posTopoEsquerda.x, -posTopoEsquerda.y);
        fundo.sizeDelta = new Vector2(d, d);
        var anel = CriarImagem("Anel", fundo, spriteAnel, corAnel, false, out _);
        Esticar(anel, 1f);
        var t = CriarTMP("Letra", fundo, letra, d * 0.5f, TextAlignmentOptions.Center, true);
        t.color = corLetra;
        Esticar(t.rectTransform, 1f);
    }

    // ------------------------------------------------------------------ construcao
    void ConstruirUI()
    {
        spriteCirculo = CriarSpriteCirculo(256, 0f);
        spriteAnel = CriarSpriteCirculo(256, 0.035f);
        spriteArredondado = CriarSpriteArredondado(96, 28);
        if (fonte == null) fonte = AcharFonte();

        var go = new GameObject("MobileAjudaCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950; // acima dos controles de toque (900), abaixo do aviso de girar o celular (1000)
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f; // mesma escala do MobileTouchControls / MobileInput.Escala
        go.AddComponent<GraphicRaycaster>(); // bloqueia botoes de outras UIs debaixo da ajuda

        // botao "?"
        botao = CriarImagem("BotaoAjuda", go.transform, spriteCirculo, corBotao, true, out imgBotao);
        botao.anchorMin = botao.anchorMax = Vector2.zero;
        var anel = CriarImagem("BotaoAjuda_Anel", botao, spriteAnel, corAnel, false, out _);
        Esticar(anel, 1f);
        txtBotao = CriarTMP("BotaoAjuda_Texto", botao, textoBotao, 60f, TextAlignmentOptions.Center, true);
        txtBotao.color = corLetra;
        txtBotao.enableAutoSizing = true;
        txtBotao.fontSizeMin = 8f;
        txtBotao.fontSizeMax = 300f;
        Esticar(txtBotao.rectTransform, 0.62f);

        // painel (tela inteira)
        painel = CriarImagem("Painel", go.transform, null, corFundo, true, out _);
        Esticar(painel, 1f);

        cartao = CriarImagem("Cartao", painel, spriteArredondado, corPainel, true, out Image imgCartao);
        imgCartao.type = Image.Type.Sliced;
        imgCartao.preserveAspect = false;
        cartao.anchorMin = new Vector2(0.1f, 0.05f);
        cartao.anchorMax = new Vector2(0.9f, 0.9f);
        cartao.offsetMin = cartao.offsetMax = Vector2.zero;

        txtTitulo = CriarTMP("Titulo", cartao, tituloPT, tamanhoTitulo, TextAlignmentOptions.Left, true);
        var tt = txtTitulo.rectTransform;
        tt.anchorMin = new Vector2(0f, 1f);
        tt.anchorMax = new Vector2(1f, 1f);
        tt.pivot = new Vector2(0.5f, 1f);
        tt.offsetMin = new Vector2(50f, -115f);
        tt.offsetMax = new Vector2(-150f, -15f);

        botaoX = CriarBotaoTexto("BotaoX", cartao, "X", 46f, out imgX, out _);
        botaoX.anchorMin = botaoX.anchorMax = new Vector2(1f, 1f);
        botaoX.pivot = new Vector2(1f, 1f);
        botaoX.anchoredPosition = new Vector2(-22f, -22f);
        botaoX.sizeDelta = new Vector2(90f, 90f);

        botaoFechar = CriarBotaoTexto("BotaoFechar", cartao, fecharPT, 42f, out imgFechar, out txtFechar);
        botaoFechar.anchorMin = botaoFechar.anchorMax = new Vector2(0.5f, 0f);
        botaoFechar.pivot = new Vector2(0.5f, 0f);
        botaoFechar.anchoredPosition = new Vector2(0f, 24f);
        botaoFechar.sizeDelta = new Vector2(380f, 88f);

        var vpGo = new GameObject("Viewport", typeof(RectTransform));
        vpGo.transform.SetParent(cartao, false);
        viewport = (RectTransform)vpGo.transform;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(50f, 130f);
        viewport.offsetMax = new Vector2(-64f, -125f);
        vpGo.AddComponent<RectMask2D>();

        var ctGo = new GameObject("Conteudo", typeof(RectTransform));
        ctGo.transform.SetParent(viewport, false);
        conteudo = (RectTransform)ctGo.transform;
        conteudo.anchorMin = new Vector2(0f, 1f);
        conteudo.anchorMax = new Vector2(1f, 1f);
        conteudo.pivot = new Vector2(0.5f, 1f);
        conteudo.anchoredPosition = Vector2.zero;
        conteudo.sizeDelta = new Vector2(0f, 100f);

        barraTrilho = CriarImagem("BarraRolagem", cartao, null, new Color(1f, 1f, 1f, 0.12f), false, out _);
        barraTrilho.anchorMin = new Vector2(1f, 0f);
        barraTrilho.anchorMax = new Vector2(1f, 1f);
        barraTrilho.offsetMin = new Vector2(-44f, 130f);
        barraTrilho.offsetMax = new Vector2(-34f, -125f);
        barraAlca = CriarImagem("Alca", barraTrilho, null, new Color(1f, 1f, 1f, 0.55f), false, out _);

        painel.gameObject.SetActive(false);
        canvas.enabled = false;
    }

    RectTransform CriarBotaoTexto(string nome, RectTransform pai, string texto, float tamanho, out Image img, out TextMeshProUGUI txt)
    {
        var rt = CriarImagem(nome, pai, spriteArredondado, corBotaoFechar, true, out img);
        img.type = Image.Type.Sliced;
        img.preserveAspect = false;
        txt = CriarTMP("Texto", rt, texto, tamanho, TextAlignmentOptions.Center, true);
        Esticar(txt.rectTransform, 1f);
        return rt;
    }

    TextMeshProUGUI CriarTMP(string nome, Transform pai, string texto, float tamanho, TextAlignmentOptions alinhamento, bool negrito)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (fonte != null) t.font = fonte;
        t.richText = true;
        t.fontSize = tamanho;
        t.alignment = alinhamento;
        t.color = corTexto;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (negrito) t.fontStyle = FontStyles.Bold;
        t.text = texto ?? "";
        return t;
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
        img.preserveAspect = sprite != null;
        return rt;
    }

    static void Esticar(RectTransform rt, float fracao)
    {
        float m = (1f - fracao) * 0.5f;
        rt.anchorMin = new Vector2(m, m);
        rt.anchorMax = new Vector2(1f - m, 1f - m);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TMP_FontAsset AcharFonte()
    {
        TMP_FontAsset melhor = null;
        int nota = 0;
        try
        {
            foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (f == null) continue;
                string n = f.name;
                int v = 0;
                if (n == "Barlow-Regular SDF") v = 4;
                else if (n.StartsWith("Barlow-Regular")) v = 3;
                else if (n.StartsWith("Barlow") && !n.Contains("Condensed") && !n.Contains("Italic")) v = 2;
                if (v > nota) { nota = v; melhor = f; }
            }
        }
        catch (Exception) { melhor = null; }
        return melhor; // null = fonte padrao do TextMeshPro
    }

    static Sprite CriarSpriteCirculo(int tam, float espessuraAnel)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.name = espessuraAnel > 0f ? "MobileAjuda_Anel" : "MobileAjuda_Circulo";
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

    /// <summary>Retangulo de cantos redondos (9-slice) para o cartao e os botoes.</summary>
    static Sprite CriarSpriteArredondado(int tam, int raio)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.name = "MobileAjuda_Arredondado";
        var px = new Color32[tam * tam];
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, raio, tam - raio);
                float cy = Mathf.Clamp(y + 0.5f, raio, tam - raio);
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(raio - d + 0.5f);
                px[y * tam + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        float b = raio + 2f;
        return Sprite.Create(tex, new Rect(0f, 0f, tam, tam), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }
}
