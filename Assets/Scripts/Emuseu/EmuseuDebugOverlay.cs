using UnityEngine;
using UnityEngine.Scripting;

// Overlay de debug do site emuseu (so diagnostico).
// - Se cria sozinho (nao precisa por na cena nem configurar no Inspector).
// - Escondido por padrao. F9 liga/desliga. Tambem abre sozinho se a URL tiver ?emuseuDebug=1.
// - Suporta Input antigo (ENABLE_LEGACY_INPUT_MANAGER) e novo Input System (ENABLE_INPUT_SYSTEM).
// - Chama EmuseuDebug.Tick() todo frame (aviso de video que nao prepara).
// - Mostra o resumo e os ultimos 30 eventos (mais novo primeiro).
public sealed class EmuseuDebugOverlay : MonoBehaviour
{
    public const string GameObjectName = "EmuseuDebugOverlay";
    const int EventosMostrados = 30;
    const int MaxCharsPorEvento = 600;

    static EmuseuDebugOverlay _instance;

    bool visivel;
    Vector2 rolagem;
    string textoResumo = "";
    string textoEventos = "";
    float proximaAtualizacao;

    GUIStyle estiloCaixa, estiloTexto, estiloTitulo;
    Texture2D fundo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { _instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Criar()
    {
        if (_instance != null) return;
        var go = new GameObject(GameObjectName);
        go.AddComponent<EmuseuDebugOverlay>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        string url = "";
        try { url = Application.absoluteURL ?? ""; } catch (System.Exception) { }
        if (url.IndexOf("emuseuDebug=1", System.StringComparison.OrdinalIgnoreCase) >= 0) visivel = true;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (fundo != null) Destroy(fundo);
    }

    void Update()
    {
        EmuseuDebug.Tick();
        if (TeclaF9()) Alternar();
    }

    // Tambem pode ser chamado de fora (ex: SendMessage("EmuseuDebugOverlay", "Alternar")).
    [Preserve]
    public void Alternar()
    {
        visivel = !visivel;
        proximaAtualizacao = 0f;
        EmuseuDebug.Info("overlay", visivel ? "overlay ligado" : "overlay desligado");
    }

    static bool TeclaF9()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F9)) return true;
#endif
        return false;
    }

    void AtualizarTextos()
    {
        if (Time.unscaledTime < proximaAtualizacao) return;
        proximaAtualizacao = Time.unscaledTime + 0.5f;

        textoResumo = EmuseuDebug.BuildSummary();
        var sb = new System.Text.StringBuilder(2048);
        foreach (var e in EmuseuDebug.Recentes(EventosMostrados))
        {
            string l = EmuseuDebug.Formatar(e);
            if (l.Length > MaxCharsPorEvento) l = l.Substring(0, MaxCharsPorEvento) + "...";
            sb.Append(l).Append('\n');
        }
        textoEventos = sb.ToString();
    }

    void PrepararEstilos()
    {
        if (estiloCaixa != null) return;
        fundo = new Texture2D(1, 1);
        fundo.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.88f));
        fundo.Apply();
        fundo.hideFlags = HideFlags.HideAndDontSave;

        estiloCaixa = new GUIStyle(GUI.skin.box);
        estiloCaixa.normal.background = fundo;

        estiloTexto = new GUIStyle(GUI.skin.label);
        estiloTexto.fontSize = 12;
        estiloTexto.wordWrap = true;
        estiloTexto.richText = false;
        estiloTexto.normal.textColor = Color.white;

        estiloTitulo = new GUIStyle(estiloTexto);
        estiloTitulo.fontSize = 14;
        estiloTitulo.fontStyle = FontStyle.Bold;
        estiloTitulo.normal.textColor = new Color(1f, 0.85f, 0.3f);
    }

    void OnGUI()
    {
        if (!visivel) return;
        PrepararEstilos();
        AtualizarTextos();

        var matrizAntiga = GUI.matrix;
        float escala = Mathf.Clamp(Screen.height / 720f, 1f, 3f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));

        float w = Screen.width / escala;
        float h = Screen.height / escala;
        var area = new Rect(10f, 10f, Mathf.Min(w - 20f, 980f), h - 20f);
        GUI.Box(area, GUIContent.none, estiloCaixa);

        GUILayout.BeginArea(new Rect(area.x + 8f, area.y + 8f, area.width - 16f, area.height - 16f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("EMUSEU DEBUG  (F9 fecha)", estiloTitulo);
        if (GUILayout.Button("Copiar tudo", GUILayout.Width(110f))) GUIUtility.systemCopyBuffer = EmuseuDebug.TextoCompleto();
        if (GUILayout.Button("Fechar", GUILayout.Width(80f))) visivel = false;
        GUILayout.EndHorizontal();

        rolagem = GUILayout.BeginScrollView(rolagem);
        GUILayout.Label(textoResumo, estiloTexto);
        GUILayout.Space(8f);
        GUILayout.Label("---- ultimos " + EventosMostrados + " eventos (mais novo primeiro) ----", estiloTitulo);
        GUILayout.Label(textoEventos, estiloTexto);
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUI.matrix = matrizAntiga;
    }
}