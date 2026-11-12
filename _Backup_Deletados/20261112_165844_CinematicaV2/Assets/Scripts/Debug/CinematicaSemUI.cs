using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Modo gravacao de cinematica: nas cenas listadas em Cenas, esconde por padrao
//  - o painel de FPS (FpsDebugOverlay)   -> F3 mostra de novo (depois o F3 normal alterna)
//  - o aviso de admin e a barra do god mode (AdminToolsPanel/AdminToolsGui)
//    -> o painel de admin continua abrindo com F2 (fecha com F2/Esc).
// Cria-se sozinho (DontDestroyOnLoad), sem Inspector e sem editar cena nem os scripts originais.
// Nas outras cenas nao faz nada.
public sealed class CinematicaSemUI : MonoBehaviour
{
    // nomes das cenas (sem .unity) onde a UI de debug/admin fica escondida
    public static readonly string[] Cenas = { "CenaArena_TESTECINE" };

    static CinematicaSemUI instancia;

    bool ativo;
    bool fpsVisivel;
    FpsDebugOverlay fps;
    AdminToolsGui adminGui;
    AdminPlayerControl adminControle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { instancia = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (instancia != null) return;
        var go = new GameObject("CinematicaSemUI");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<CinematicaSemUI>();
    }

    void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;
        SceneManager.sceneLoaded += AoCarregarCena;
        Aplicar(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        if (instancia == this) instancia = null;
    }

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (modo == LoadSceneMode.Single) Aplicar(cena.name);
    }

    static bool EhCenaCinematica(string nome)
    {
        for (int i = 0; i < Cenas.Length; i++)
            if (string.Equals(Cenas[i], nome, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    void Aplicar(string nomeCena)
    {
        bool eraAtivo = ativo;
        ativo = EhCenaCinematica(nomeCena);
        Buscar();
        if (ativo)
        {
            fpsVisivel = false;
            if (fps != null) fps.enabled = false;
            Debug.Log("[Cinematica] UI de debug/admin escondida em " + nomeCena + " (F3 mostra o FPS, F2 abre o admin)");
        }
        else if (eraAtivo)
        {
            // saiu da cena de cinematica: devolve o FPS como estava
            if (fps != null) fps.enabled = true;
        }
    }

    void Buscar()
    {
        if (fps == null) fps = FindAnyObjectByType<FpsDebugOverlay>();
        if (adminGui == null) adminGui = FindAnyObjectByType<AdminToolsGui>();
        if (adminControle == null) adminControle = FindAnyObjectByType<AdminPlayerControl>();
    }

    void Update()
    {
        if (!ativo) return;
        if (fps == null || adminGui == null || adminControle == null) Buscar();
    }
    // roda depois do Update do AdminToolsPanel e antes do OnGUI:
    // so deixa desenhar quando o painel de admin esta aberto (F2)
    void LateUpdate()
    {
        if (!ativo) return;

        // FPS: escondido ate o primeiro F3. Liga aqui no LateUpdate (o Update do overlay ja passou,
        // entao o F3 deste frame nao e contado duas vezes); dai em diante o proprio F3 do overlay alterna.
        if (!fpsVisivel && TeclaF3())
        {
            fpsVisivel = true;
            if (fps != null) fps.enabled = true;
        }

        if (adminGui == null) return;
        bool painelAberto = adminControle != null && adminControle.PainelAberto;
        if (!painelAberto && adminGui.enabled) adminGui.enabled = false;
    }

    static bool TeclaF3()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.f3Key.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F3)) return true;
#endif
        return false;
    }
}