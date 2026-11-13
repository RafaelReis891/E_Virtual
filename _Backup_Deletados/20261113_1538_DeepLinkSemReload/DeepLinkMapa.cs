using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

// Deep link de mapa: o site abre o jogo direto numa cidade.
//   https://.../game?mapa=rio-de-janeiro  (sao-paulo, belo-horizonte, porto-alegre, salvador, recife, fortaleza, brasilia)
//
// Como funciona (sem editar cena nem prefab, sem Inspector):
//  - Cria-se sozinho (RuntimeInitializeOnLoadMethod BeforeSceneLoad) num GameObject "DeepLinkMapa" (DontDestroyOnLoad).
//  - Le "mapa" da URL logo no inicio (Application.absoluteURL). No WebGL tambem pergunta ao navegador
//    (DeepLinkMapa.jslib: window.location, window.parent se for do mesmo dominio, e sessionStorage "emuseu.mapa").
//  - Quando a CenaArena (1a cena) termina de carregar e o play-config do site chegou (ou passou o tempo limite),
//    faz o mesmo caminho do portal do HUB: liga o "FadeIn" da cena, espera 1 s e chama SceneManager.LoadScene(cena)
//    (igual ao TimedEvent -> SimpleLoadScene.LoadScene dos Trigger_Teleport_*).
//  - Roda uma vez so por carga da pagina. Slug desconhecido ou sem slug = fluxo normal.
//  - O site tambem pode chamar depois: SendMessage("DeepLinkMapa", "IrParaMapa", "rio-de-janeiro").
//  - Editor: menu Tools/Emuseu/Testar deep link mapa (DeepLinkMapaMenu.cs).
// Logs com prefixo [DeepLinkMapa].
public sealed class DeepLinkMapa : MonoBehaviour
{
    public const string GameObjectName = "DeepLinkMapa";
    const string Tag = "[DeepLinkMapa] ";
    const string CenaInicial = "CenaArena";
    const string NomeFade = "FadeIn";
    const float AtrasoFade = 1f;            // igual ao TimedEvent.timer dos portais
    const float EsperaMaxPlayConfig = 8f;   // segundos (tempo real) esperando o play-config do site
    public const string ChaveTesteEditor = "DeepLinkMapa.Teste";

    // slug normalizado (minusculo, sem acento, sem '-', '_' ou espaco) -> nome da cena na build
    static readonly Dictionary<string, string> Mapa = new Dictionary<string, string>
    {
        { "riodejaneiro", "CenaRiodeJaneiro" }, { "rio", "CenaRiodeJaneiro" }, { "rj", "CenaRiodeJaneiro" },
        { "saopaulo", "CenaSaoPaulo" }, { "sp", "CenaSaoPaulo" },
        { "belohorizonte", "CenaBeloHorizonte" }, { "bh", "CenaBeloHorizonte" }, { "mg", "CenaBeloHorizonte" },
        { "portoalegre", "CenaPortoAlegre" }, { "poa", "CenaPortoAlegre" }, { "rs", "CenaPortoAlegre" },
        { "salvador", "CenaSalvador" }, { "ba", "CenaSalvador" },
        { "recife", "CenaRecife" }, { "pe", "CenaRecife" },
        { "fortaleza", "CenaFortaleza" }, { "ce", "CenaFortaleza" },
        { "brasilia", "CenaBrasilia" }, { "df", "CenaBrasilia" },
        { "arena", CenaInicial }, { "hub", CenaInicial },
    };

    static DeepLinkMapa instancia;
    static bool usado;              // o deep link da URL ja foi consumido nesta carga da pagina
    static string slugInicial;      // lido da URL / navegador / teste do Editor
    static string fonteInicial = "-";

    string pedidoSite;              // IrParaMapa chamado antes da cena estar pronta
    bool carregando;
    bool viuCena;                   // ja tratou o sceneLoaded da 1a cena
    Coroutine rotina;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern string DeepLinkMapa_LerParametro(string nome);
#endif

    public static DeepLinkMapa Instance => instancia;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar()
    {
        instancia = null; usado = false; slugInicial = null; fonteInicial = "-";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar()
    {
        if (instancia != null) return;
        LerSlugInicial();   // o quanto antes: o site pode trocar a URL depois (history.replaceState)
        var go = new GameObject(GameObjectName);
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<DeepLinkMapa>();
    }

    void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;
        if (gameObject.name != GameObjectName) gameObject.name = GameObjectName; // alvo do SendMessage do site
    }

    void OnEnable() { SceneManager.sceneLoaded += AoCarregarCena; }
    void OnDisable() { SceneManager.sceneLoaded -= AoCarregarCena; }
    void OnDestroy() { if (instancia == this) instancia = null; }

    void Start()
    {
        // Play direto com a cena ja carregada (o sceneLoaded da 1a cena pode ter passado antes do OnEnable).
        Scene s = SceneManager.GetActiveScene();
        if (!viuCena && s.IsValid() && s.isLoaded) AoCarregarCena(s, LoadSceneMode.Single);
    }

    // ---------- leitura do slug ----------

    static void LerSlugInicial()
    {
        string v = null;
        string url = null;
        try { url = Application.absoluteURL; } catch { }
        v = ParametroDaUrl(url, "mapa");
        if (!string.IsNullOrEmpty(v)) fonteInicial = "Application.absoluteURL";

#if UNITY_WEBGL && !UNITY_EDITOR
        if (string.IsNullOrEmpty(v))
        {
            try { v = DeepLinkMapa_LerParametro("mapa"); } catch (Exception ex) { Debug.LogWarning(Tag + "jslib falhou: " + ex.Message); }
            if (!string.IsNullOrEmpty(v)) fonteInicial = "navegador (jslib)";
        }
#endif

#if UNITY_EDITOR
        if (string.IsNullOrEmpty(v))
        {
            v = UnityEditor.SessionState.GetString(ChaveTesteEditor, "");
            UnityEditor.SessionState.EraseString(ChaveTesteEditor); // teste vale para um Play so
            if (!string.IsNullOrEmpty(v)) fonteInicial = "teste do Editor";
        }
#endif
        slugInicial = string.IsNullOrEmpty(v) ? null : v.Trim();
        Debug.Log(Tag + "url='" + (url ?? "") + "' mapa='" + (slugInicial ?? "") + "' fonte=" + fonteInicial);
    }

    // Le um parametro de query (?a=1&mapa=x) ou do hash (#/game?mapa=x). Sem Regex/HttpUtility (leve no WebGL).
    public static string ParametroDaUrl(string url, string nome)
    {
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(nome)) return null;
        int q = url.IndexOf('?');
        if (q < 0) return null;
        string query = url.Substring(q + 1);
        foreach (string parte in query.Split('&', '#', '?'))
        {
            if (parte.Length == 0) continue;
            int eq = parte.IndexOf('=');
            string k = eq < 0 ? parte : parte.Substring(0, eq);
            if (!string.Equals(Decodificar(k), nome, StringComparison.OrdinalIgnoreCase)) continue;
            return eq < 0 ? "" : Decodificar(parte.Substring(eq + 1));
        }
        return null;
    }

    static string Decodificar(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace('+', ' ');
        try { return Uri.UnescapeDataString(s); } catch { return s; }
    }

    // "Rio-de-Janeiro", "rio_de_janeiro", "São Paulo", "BRASÍLIA" -> "riodejaneiro", "saopaulo", "brasilia"
    public static string Normalizar(string slug)
    {
        if (string.IsNullOrEmpty(slug)) return "";
        var sb = new StringBuilder(slug.Length);
        foreach (char c0 in slug.Trim().ToLowerInvariant())
        {
            char c = SemAcento(c0);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
        }
        return sb.ToString();
    }

    static char SemAcento(char c)
    {
        switch (c)
        {
            case 'á': case 'à': case 'â': case 'ã': case 'ä': return 'a';
            case 'é': case 'è': case 'ê': case 'ë': return 'e';
            case 'í': case 'ì': case 'î': case 'ï': return 'i';
            case 'ó': case 'ò': case 'ô': case 'õ': case 'ö': return 'o';
            case 'ú': case 'ù': case 'û': case 'ü': return 'u';
            case 'ç': return 'c';
            case 'ñ': return 'n';
            default: return c;
        }
    }

    // Slug -> nome da cena (aceita tambem o nome da cena, ex.: "CenaRecife"). null se desconhecido.
    public static string CenaDoSlug(string slug)
    {
        string n = Normalizar(slug);
        if (n.Length == 0) return null;
        string cena;
        if (Mapa.TryGetValue(n, out cena)) return cena;
        foreach (var kv in Mapa) if (Normalizar(kv.Value) == n) return kv.Value;
        return null;
    }

    // ---------- fluxo ----------

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (modo != LoadSceneMode.Single) return;
        viuCena = true;
        carregando = false;

        if (!string.IsNullOrEmpty(pedidoSite))
        {
            string p = pedidoSite; pedidoSite = null;
            IrPara(p, "site (SendMessage, pendente)");
            return;
        }

        if (usado) return;
        usado = true; // so a 1a cena da pagina decide
        if (string.IsNullOrEmpty(slugInicial)) return;

        if (cena.name != CenaInicial)
        {
            Debug.Log(Tag + "1a cena e '" + cena.name + "' (nao " + CenaInicial + "), deep link ignorado");
            return;
        }
        string alvo = CenaDoSlug(slugInicial);
        if (alvo == null) { Debug.LogWarning(Tag + "mapa '" + slugInicial + "' desconhecido, fluxo normal"); return; }
        if (alvo == CenaInicial) { Debug.Log(Tag + "mapa '" + slugInicial + "' = HUB, fluxo normal"); return; }
        if (!Application.CanStreamedLevelBeLoaded(alvo)) { Debug.LogWarning(Tag + "cena '" + alvo + "' nao esta na build, fluxo normal"); return; }

        Debug.Log(Tag + "mapa '" + slugInicial + "' -> " + alvo + " (esperando a " + CenaInicial + " e o play-config)");
        Comecar(alvo, cena, true);
    }

    // Chamado pelo site a qualquer momento: SendMessage("DeepLinkMapa", "IrParaMapa", "rio-de-janeiro").
    [Preserve]
    public void IrParaMapa(string slug) { IrPara(slug, "site (SendMessage)"); }

    // Para outros scripts e para o menu do Editor.
    public void IrPara(string slug, string fonte)
    {
        string alvo = CenaDoSlug(slug);
        if (alvo == null) { Debug.LogWarning(Tag + "IrParaMapa: mapa '" + slug + "' desconhecido, ignorado"); return; }
        if (!Application.CanStreamedLevelBeLoaded(alvo)) { Debug.LogWarning(Tag + "IrParaMapa: cena '" + alvo + "' nao esta na build"); return; }
        Scene atual = SceneManager.GetActiveScene();
        if (!atual.IsValid() || !atual.isLoaded) { pedidoSite = slug; Debug.Log(Tag + "IrParaMapa '" + slug + "' guardado ate a cena carregar"); return; }
        if (atual.name == alvo) { Debug.Log(Tag + "IrParaMapa: ja esta em " + alvo); return; }
        usado = true; // um pedido do site substitui o da URL
        Debug.Log(Tag + "IrParaMapa '" + slug + "' -> " + alvo + " (fonte: " + fonte + ")");
        Comecar(alvo, atual, false);
    }

    void Comecar(string alvo, Scene origem, bool esperarPlayConfig)
    {
        if (carregando) { Debug.Log(Tag + "ja carregando uma cena, pedido " + alvo + " ignorado"); return; }
        if (rotina != null) StopCoroutine(rotina);
        rotina = StartCoroutine(Ir(alvo, origem, esperarPlayConfig));
    }

    IEnumerator Ir(string alvo, Scene origem, bool esperarPlayConfig)
    {
        // deixa a cena terminar Awake/Start (PularIntroArena marca introVista, IntroCameraHold etc.)
        yield return null;
        yield return null;

        if (esperarPlayConfig)
        {
#if UNITY_EDITOR
            float limite = 1f;   // no Editor nao tem site; use Tools/Emuseu se quiser mandar um payload de teste
#else
            float limite = EsperaMaxPlayConfig;
#endif
            float t0 = Time.realtimeSinceStartup;
            while (EmuseuBridge.PlayConfig == null && Time.realtimeSinceStartup - t0 < limite) yield return null;
            Debug.Log(Tag + (EmuseuBridge.PlayConfig != null ? "play-config ok" : "play-config nao chegou em " + limite + " s, seguindo assim mesmo"));
        }

        // o jogador pode ter saido da cena (portal) enquanto esperavamos
        if (!origem.IsValid() || !origem.isLoaded || SceneManager.GetActiveScene() != origem) { Debug.Log(Tag + "cena mudou antes do deep link, cancelado"); rotina = null; yield break; }
        if (carregando) { rotina = null; yield break; }
        carregando = true;

        // mesmo efeito do portal: FadeIn ligado e LoadScene 1 s depois (tempo real: o site pode ter pausado o jogo)
        GameObject fade = Achar(origem, NomeFade);
        if (fade != null) { fade.SetActive(true); Debug.Log(Tag + "FadeIn ligado, carregando " + alvo + " em " + AtrasoFade + " s"); yield return new WaitForSecondsRealtime(AtrasoFade); }
        else Debug.Log(Tag + "sem '" + NomeFade + "' na cena " + origem.name + ", carregando direto");

        // volta para o HUB depois: pula a intro da Arena (a PularIntroArena ja marca introVista na 1a abertura)
        PularIntroArena.introVista = true;

        Debug.Log(Tag + "SceneManager.LoadScene(" + alvo + ") (igual aos portais)");
        rotina = null;
        try { SceneManager.LoadScene(alvo); }
        catch (Exception ex) { carregando = false; Debug.LogWarning(Tag + "falha ao carregar " + alvo + ": " + ex.Message); }
    }

    static GameObject Achar(Scene cena, string nome)
    {
        foreach (GameObject root in cena.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t.gameObject;
        return null;
    }
}
