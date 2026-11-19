// MobileAjudaPisca.cs
// Celular: quando o jogador fica parado mais de X segundos (padrao 5), o botao "?" da ajuda (MobileAjuda) pisca:
// cresce e diminui (1.0 -> 1.2) e alterna entre branco e verde. Para na hora em que o jogador anda, gira a camera,
// toca na tela, aperta Interagir ou abre a ajuda, e devolve escala e cores originais.
// - Cria-se sozinho (RuntimeInitializeOnLoadMethod + sceneLoaded), igual ao MobileAjuda. Nao precisa de cena,
//   prefab nem Inspector. So roda quando MobileInput.Ativo (celular, ?mobile=1 ou menu do Editor
//   Mobile > Tools > Simular controles touch no Play). No PC nada e criado.
// - Acha o botao sozinho: MobileAjuda.Instancia -> filho "BotaoAjuda" (+ "BotaoAjuda_Anel" e "BotaoAjuda_Texto").
// - Nao pisca quando o botao "?" nao esta na tela (MobileInput.BotaoAjudaNaTela = false): ajuda aberta, card,
//   puzzle/cinematica que desliga o look, jogo pausado pelo site, celular em pe. Ao voltar, conta de novo do zero.
// - Desligar por codigo: MobileAjudaPisca.Ligado = false. Ajustes no Play: objeto "MobileAjudaPisca" (DontDestroyOnLoad).
// - Usa tempo sem escala (Time.unscaledTime), funciona com o jogo em Time.timeScale = 0.
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using StarterAssets;

[DefaultExecutionOrder(500)] // depois do MobileAjuda (-990); a cor e aplicada no LateUpdate para ganhar do Update dele
public class MobileAjudaPisca : MonoBehaviour
{
    /// <summary>Chave geral. false = nunca pisca (e restaura o botao se estiver piscando).</summary>
    public static bool Ligado = true;

    public static MobileAjudaPisca Instancia { get; private set; }

    [Header("Quando piscar")]
    [Tooltip("Segundos parado (sem andar, sem girar a camera e sem tocar na tela) ate o \"?\" comecar a piscar.")]
    public float segundosParado = 5f;
    [Tooltip("Quanto o jogador precisa andar (metros) para contar como movimento.")]
    public float distanciaMinima = 0.03f;
    [Tooltip("Quanto a camera precisa girar (graus) para contar como movimento.")]
    public float anguloMinimo = 1f;

    [Header("Como piscar")]
    [Tooltip("Escala maxima do botao no pulso (1 = tamanho normal).")]
    public float escalaMaxima = 1.2f;
    [Tooltip("Pulsos por segundo.")]
    public float pulsosPorSegundo = 1.2f;
    [Tooltip("Verde do pulso (#2ECC40).")]
    public Color corVerde = new Color(46f / 255f, 204f / 255f, 64f / 255f, 1f);
    [Tooltip("Opacidade do fundo do botao no lado branco do pulso (o normal e 0.22, quase invisivel).")]
    [Range(0f, 1f)] public float alfaFundoBranco = 0.45f;
    [Tooltip("Opacidade do fundo do botao no lado verde do pulso.")]
    [Range(0f, 1f)] public float alfaFundoVerde = 0.9f;

    public bool logNoConsole = true;

    // estado
    float ultimoMovimento;
    bool piscando;
    float inicioPisca;
    bool temReferencia;
    Vector3 posRef;
    Vector3 frenteCamRef;

    // botao
    RectTransform botao;
    Image imgFundo, imgAnel;
    TextMeshProUGUI txt;
    Vector3 escalaOriginal = Vector3.one;
    Color corFundoOriginal, corAnelOriginal, corTextoOriginal;

    // jogador
    FirstPersonController fpc;
    float proximaBusca;

    static bool inscrito;

    // ------------------------------------------------------------------ criacao
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetarDominio()
    {
        Instancia = null;
        inscrito = false;
        Ligado = true;
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

    static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (MobileInput.Ativo) Garantir();
        if (Instancia != null) Instancia.ZerarContagem();
    }

    public static void Garantir()
    {
        if (Instancia != null) return;
        var go = new GameObject("MobileAjudaPisca");
        DontDestroyOnLoad(go);
        Instancia = go.AddComponent<MobileAjudaPisca>();
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(this); return; }
        Instancia = this;
        ZerarContagem();
        if (logNoConsole) Debug.Log("[MobileAjudaPisca] criado (" + segundosParado + "s parado -> \"?\" pisca)");
    }

    void OnDisable() { Parar(); }

    void OnDestroy()
    {
        Parar();
        if (Instancia == this) Instancia = null;
    }

    /// <summary>Recomeca a contagem dos segundos parado (ex.: troca de cena).</summary>
    public void ZerarContagem()
    {
        ultimoMovimento = Time.unscaledTime;
        temReferencia = false;
        fpc = null;
        proximaBusca = 0f;
        Parar();
    }

    // ------------------------------------------------------------------ frame
    void LateUpdate()
    {
        if (!Ligado || !MobileInput.Ativo || !BotaoVisivel())
        {
            // ajuda aberta, card/puzzle/cinematica, pausa, celular em pe ou PC: nao pisca e conta de novo depois
            Parar();
            ultimoMovimento = Time.unscaledTime;
            temReferencia = false;
            return;
        }

        if (JogadorMexeu())
        {
            ultimoMovimento = Time.unscaledTime;
            Parar();
            return;
        }

        if (!piscando)
        {
            if (Time.unscaledTime - ultimoMovimento >= Mathf.Max(0.1f, segundosParado)) Comecar();
            else return;
        }
        Aplicar();
    }

    bool BotaoVisivel()
    {
        if (!MobileInput.BotaoAjudaNaTela || MobileInput.AjudaAberta) return false;
        if (!AcharBotao()) return false;
        if (!botao.gameObject.activeInHierarchy) return false;
        Canvas c = botao.GetComponentInParent<Canvas>();
        return c != null && c.enabled;
    }

    bool AcharBotao()
    {
        if (botao != null && imgFundo != null) return true;
        botao = null; imgFundo = null; imgAnel = null; txt = null; piscando = false;
        MobileAjuda aj = MobileAjuda.Instancia;
        if (aj == null) return false;
        foreach (RectTransform rt in aj.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.name == "BotaoAjuda") { botao = rt; break; }
        }
        if (botao == null) return false; // a UI do MobileAjuda ainda nao foi montada
        imgFundo = botao.GetComponent<Image>();
        Transform anel = botao.Find("BotaoAjuda_Anel");
        if (anel != null) imgAnel = anel.GetComponent<Image>();
        Transform t = botao.Find("BotaoAjuda_Texto");
        if (t != null) txt = t.GetComponent<TextMeshProUGUI>();
        if (imgFundo == null) { botao = null; return false; }
        if (logNoConsole) Debug.Log("[MobileAjudaPisca] botao \"?\" encontrado");
        return true;
    }

    // ------------------------------------------------------------------ movimento
    bool JogadorMexeu()
    {
        // toques: joysticks M / C, botao I, toque no mundo, toque no "?"
        if (MobileInput.Esquerdo.Ativo || MobileInput.Direito.Ativo) return true;
        if (MobileInput.Mover.sqrMagnitude > 0.0001f || MobileInput.Olhar.sqrMagnitude > 0.0001f) return true;
        if (MobileInput.InteragirSegurado || MobileInput.Clique(false)) return true;
        if (Time.unscaledTime - MobileInput.UltimoToqueAjuda < 0.2f) return true;

        // posicao do jogador e direcao da camera (pega tambem empurroes, plataformas, etc.)
        BuscarJogador();
        Camera cam = Camera.main;
        Vector3 pos = fpc != null ? fpc.transform.position : (cam != null ? cam.transform.position : Vector3.zero);
        Vector3 frente = cam != null ? cam.transform.forward : (fpc != null ? fpc.transform.forward : Vector3.forward);
        if (!temReferencia)
        {
            temReferencia = true;
            posRef = pos;
            frenteCamRef = frente;
            return false;
        }
        bool mexeu = false;
        if ((pos - posRef).sqrMagnitude > distanciaMinima * distanciaMinima) mexeu = true;
        if (Vector3.Angle(frente, frenteCamRef) > anguloMinimo) mexeu = true;
        if (mexeu) { posRef = pos; frenteCamRef = frente; }
        return mexeu;
    }

    void BuscarJogador()
    {
        if (fpc != null) return;
        if (Time.unscaledTime < proximaBusca) return;
        proximaBusca = Time.unscaledTime + 0.5f;
        fpc = FindAnyObjectByType<FirstPersonController>();
        if (fpc != null) temReferencia = false;
    }

    // ------------------------------------------------------------------ pulso
    void Comecar()
    {
        if (botao == null || imgFundo == null) return;
        escalaOriginal = botao.localScale;
        corFundoOriginal = imgFundo.color;
        corAnelOriginal = imgAnel != null ? imgAnel.color : Color.white;
        corTextoOriginal = txt != null ? txt.color : Color.white;
        inicioPisca = Time.unscaledTime;
        piscando = true;
        if (logNoConsole) Debug.Log("[MobileAjudaPisca] jogador parado " + segundosParado + "s: \"?\" piscando");
    }

    void Aplicar()
    {
        if (botao == null || imgFundo == null) { piscando = false; return; }
        float tempo = Time.unscaledTime - inicioPisca;
        // 0 -> 1 -> 0 suave, comecando do tamanho/cor normal
        float k = 0.5f - 0.5f * Mathf.Cos(tempo * Mathf.Max(0.05f, pulsosPorSegundo) * Mathf.PI * 2f);

        botao.localScale = escalaOriginal * Mathf.Lerp(1f, escalaMaxima, k);

        Color branco = Color.white;
        Color fundoBranco = branco; fundoBranco.a = Mathf.Max(corFundoOriginal.a, alfaFundoBranco);
        Color fundoVerde = corVerde; fundoVerde.a = alfaFundoVerde;
        imgFundo.color = Color.Lerp(fundoBranco, fundoVerde, k);

        if (imgAnel != null)
        {
            Color anelBranco = branco; anelBranco.a = Mathf.Max(corAnelOriginal.a, 0.9f);
            Color anelVerde = corVerde; anelVerde.a = 1f;
            imgAnel.color = Color.Lerp(anelBranco, anelVerde, k);
        }
        if (txt != null)
        {
            Color letra = branco; letra.a = 1f; // letra branca bem visivel em cima do verde
            txt.color = letra;
        }
    }

    void Parar()
    {
        if (!piscando) return;
        piscando = false;
        if (botao != null) botao.localScale = escalaOriginal;
        if (imgFundo != null) imgFundo.color = corFundoOriginal; // o MobileAjuda tambem reescreve no proximo Update
        if (imgAnel != null) imgAnel.color = corAnelOriginal;
        if (txt != null) txt.color = corTextoOriginal;
        if (logNoConsole) Debug.Log("[MobileAjudaPisca] parou (jogador mexeu ou \"?\" saiu da tela)");
    }
}
