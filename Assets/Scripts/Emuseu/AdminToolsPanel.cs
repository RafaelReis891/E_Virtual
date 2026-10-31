using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ferramentas de ADMIN (so admin; jogador normal nunca ve nem usa):
//  - F2 abre/fecha o painel (Esc fecha). Painel aberto: cursor livre e input do jogo bloqueado (AdminPlayerControl).
//  - Botoes para cada cena de cidade (desabilitados/cinza se a cena nao esta na build).
//  - God mode: voar com WASD relativo a camera, Space sobe, Ctrl/Q desce, Shift rapido (AdminPlayerControl).
//
// Quem e admin: o mesmo criterio do RemoteBanner/RemoteBannerTour/RemoteBannerClickDriver: EmuseuSettingsCatalog.IsAdmin
// (play-config do EmuseuBridge: user.roles contem "root"). No Editor o painel funciona sempre (para teste).
//
// Cria-se sozinho (RuntimeInitializeOnLoadMethod AfterSceneLoad, DontDestroyOnLoad), sem Inspector, sem editar cena.
// Carregar cena: o mesmo SceneManager.LoadScene(nome) que os portais usam (SceneLoader/SimpleLoadScene).
public sealed class AdminToolsPanel : MonoBehaviour
{
    const string Tag = "admin";

    sealed class Cena
    {
        public string rotulo, nome, caminho;
        public bool ok;
        public Cena(string rotulo, string nome, string caminho) { this.rotulo = rotulo; this.nome = nome; this.caminho = caminho; }
    }

    static readonly Cena[] Cidades =
    {
        new Cena("Arena (Hub)",         "CenaArena",           "Assets/Games/RioDeJaneiro/Default/CenaArena.unity"),
        new Cena("Rio de Janeiro",      "CenaRiodeJaneiro",    "Assets/Games/RioDeJaneiro/Default/CenaRiodeJaneiro.unity"),
        new Cena("Sao Paulo",           "CenaPadrao-SaoPaulo", "Assets/Games/SaoPaulo/CenaPadrao-SaoPaulo.unity"),
        new Cena("Belo Horizonte",      "CenaBeloHorizonte",   "Assets/Games/BeloHorizonte/CenaBeloHorizonte.unity"),
        new Cena("Brasilia (DF)",       "CenaBrasilia",        "Assets/Games/Brasilia/CenaBrasilia.unity"),
        new Cena("Fortaleza",           "CenaFortaleza",       "Assets/Games/Fortaleza/CenaFortaleza.unity"),
        new Cena("Porto Alegre",        "CenaPortoAlegre",     "Assets/Games/PortoAlegre/CenaPortoAlegre.unity"),
        new Cena("Recife",              "CenaRecife",          "Assets/Games/Recife/CenaRecife.unity"),
        new Cena("Salvador",            "CenaSalvador",        "Assets/Games/Salvador/CenaSalvador.unity"),
    };

    static readonly Cena[] Puzzles =
    {
        new Cena("RJ Puzzle E1", "RJ_E1", "Assets/Games/RioDeJaneiro/Puzzle_E1/Scenes/RJ_E1.unity"),
        new Cena("RJ Puzzle E2", "RJ_E2", "Assets/Games/RioDeJaneiro/Puzzle_E2/Scenes/RJ_E2.unity"),
        new Cena("RJ Puzzle E3", "RJ_E3", "Assets/Games/RioDeJaneiro/Puzzle_E3/Scenes/RJ_E3.unity"),
        new Cena("RJ Puzzle E4", "RJ_E4", "Assets/Games/RioDeJaneiro/Puzzle_E4/Scene/RJ_E4.unity"),
        new Cena("RJ Puzzle E5", "RJ_E5", "Assets/Games/RioDeJaneiro/Puzzle_E5/Scenes/RJ_E5.unity"),
    };

    // cenas da build que nao estao nas listas acima, sem nome de teste/backup (cobre cenas novas adicionadas a build)
    static readonly string[] NomesIgnorados = { "test", "teste", "paulo", "backup", "bak", "old", "sample", "recovery", "playground", "tobedeleted", "font_samples" };
    readonly List<Cena> extras = new List<Cena>();

    static AdminToolsPanel instancia;

    AdminPlayerControl controle;
    AdminToolsGui gui;

    bool aberto;
    bool pedidoFechar;
    Cena pedidoCena;
    bool carregando;
    bool pularIntro = true;
    bool eraAdmin;
    float hintAte;
    Vector2 scroll;

    GUIStyle estBox, estBotao, estTitulo, estTexto, estHud;
    Texture2D fundo, fundoHud;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { instancia = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (instancia != null) return;
        var go = new GameObject("AdminToolsPanel");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<AdminToolsPanel>();
    }

    // Mesmo criterio dos scripts de banner (EmuseuSettingsCatalog.IsAdmin) + sempre no Editor.
    public static bool Permitido
    {
        get
        {
#if UNITY_EDITOR
            return true;
#else
            return EmuseuSettingsCatalog.IsAdmin;
#endif
        }
    }

    void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;
        controle = gameObject.AddComponent<AdminPlayerControl>();
        gui = gameObject.AddComponent<AdminToolsGui>();
        gui.dono = this;
        gui.enabled = false;
    }

    void OnEnable() { SceneManager.sceneLoaded += AoCarregarCena; }
    void OnDisable() { SceneManager.sceneLoaded -= AoCarregarCena; }

    void OnDestroy()
    {
        if (fundo != null) Destroy(fundo);
        if (fundoHud != null) Destroy(fundoHud);
        if (instancia == this) instancia = null;
    }

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        carregando = false;
        pedidoCena = null;
        if (aberto) { aberto = false; controle.DefinirPainel(false); }
        PularIntroArena.pularProxima = false; // so vale para a carga pedida pelo painel
        if (Permitido) hintAte = Time.unscaledTime + 6f;
    }

    // ---------- loop ----------

    void Update()
    {
        if (!Permitido)
        {
            // deixou de ser admin (ou play-config trocou): devolve tudo e some
            if (eraAdmin || aberto || controle.GodLigado)
            {
                eraAdmin = false;
                aberto = false;
                controle.LiberarTudo();
            }
            gui.enabled = false;
            return;
        }
        if (!eraAdmin) { eraAdmin = true; hintAte = Time.unscaledTime + 8f; }

        if (AdminInput.Apertou(AdminInput.Tecla.F2))
        {
            if (aberto) pedidoFechar = true;
            else if (!EmuseuUploadModalGuard.Ativo) Abrir();
        }
        else if (aberto && AdminInput.Apertou(AdminInput.Tecla.Esc)) pedidoFechar = true;

        if (pedidoFechar)
        {
            pedidoFechar = false;
            Fechar();
        }
        if (pedidoCena != null)
        {
            Cena c = pedidoCena;
            pedidoCena = null;
            CarregarCena(c);
        }

        gui.enabled = aberto || controle.GodLigado || Time.unscaledTime < hintAte;
    }

    void Abrir()
    {
        AtualizarDisponibilidade();
        aberto = true;
        controle.DefinirPainel(true);
        EmuseuDebug.Info(Tag, "painel admin aberto");
    }

    void Fechar()
    {
        if (!aberto) return;
        aberto = false;
        controle.DefinirPainel(false);
        EmuseuDebug.Info(Tag, "painel admin fechado");
    }

    // ---------- cenas ----------

    static bool NaBuild(Cena c)
    {
        try
        {
            if (Application.CanStreamedLevelBeLoaded(c.nome)) return true;
            return !string.IsNullOrEmpty(c.caminho) && SceneUtility.GetBuildIndexByScenePath(c.caminho) >= 0;
        }
        catch (Exception) { return false; }
    }

    void AtualizarDisponibilidade()
    {
        foreach (Cena c in Cidades) c.ok = NaBuild(c);
        foreach (Cena c in Puzzles) c.ok = NaBuild(c);

        extras.Clear();
        int n = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < n; i++)
        {
            string caminho = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(caminho)) continue;
            int barra = caminho.LastIndexOf('/');
            string nome = caminho.Substring(barra + 1);
            if (nome.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) nome = nome.Substring(0, nome.Length - 6);
            if (Conhecida(nome) || Ignorada(nome)) continue;
            extras.Add(new Cena(nome, nome, caminho) { ok = true });
        }
    }

    static bool Conhecida(string nome)
    {
        foreach (Cena c in Cidades) if (c.nome == nome) return true;
        foreach (Cena c in Puzzles) if (c.nome == nome) return true;
        return false;
    }

    static bool Ignorada(string nome)
    {
        string baixo = nome.ToLowerInvariant();
        foreach (string k in NomesIgnorados) if (baixo.Contains(k)) return true;
        return false;
    }

    void CarregarCena(Cena c)
    {
        if (carregando || c == null || !c.ok) return;
        carregando = true;
        // Arena: a intro so toca na 1a vez; como o portal de volta, pula a intro (PularIntroArena existente na cena)
        if (c.nome == "CenaArena" && pularIntro) PularIntroArena.pularProxima = true;
        Fechar();
        EmuseuDebug.Info(Tag, "carregando cena " + c.nome + " (SceneManager.LoadScene, igual aos portais)");
        try { SceneManager.LoadScene(c.nome); }
        catch (Exception ex)
        {
            carregando = false;
            Debug.LogWarning("[AdminTools] falha ao carregar " + c.nome + ": " + ex.Message);
        }
    }

    // ---------- GUI (chamado pelo AdminToolsGui.OnGUI) ----------

    void CriarEstilos()
    {
        if (estBox != null) return;
        fundo = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        fundo.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.12f, 0.96f));
        fundo.Apply();
        fundoHud = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        fundoHud.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
        fundoHud.Apply();

        estBox = new GUIStyle(GUI.skin.box);
        estBox.normal.background = fundo;
        estBotao = new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = true };
        estTitulo = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        estTitulo.normal.textColor = Color.white;
        estTexto = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        estTexto.normal.textColor = new Color(0.85f, 0.88f, 0.95f);
        estHud = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        estHud.normal.textColor = Color.white;
        estHud.normal.background = fundoHud;
    }

    public void Desenhar()
    {
        if (!Permitido) return;
        CriarEstilos();
        GUI.depth = -1000;

        float esc = Mathf.Clamp(Screen.height / 800f, 1f, 2.5f);
        Matrix4x4 antiga = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(esc, esc, 1f));
        float sw = Screen.width / esc, sh = Screen.height / esc;

        if (!aberto)
        {
            DesenharHud(sw);
            GUI.matrix = antiga;
            return;
        }

        float w = Mathf.Min(600f, sw - 20f), h = Mathf.Min(640f, sh - 20f);
        var r = new Rect((sw - w) * 0.5f, (sh - h) * 0.5f, w, h);
        GUI.Box(r, GUIContent.none, estBox);
        GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 10f, w - 24f, h - 20f));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Admin Tools", estTitulo);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Fechar (F2)", estBotao, GUILayout.Width(110f), GUILayout.Height(26f))) pedidoFechar = true;
        GUILayout.EndHorizontal();

#if UNITY_EDITOR
        GUILayout.Label("Modo Editor (teste). No jogo so aparece para admin (root).", estTexto);
#else
        GUILayout.Label("Admin (root).", estTexto);
#endif

        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Space(6f);
        GUILayout.Label("God mode", estTitulo);
        DesenharGod();

        GUILayout.Space(10f);
        GUILayout.Label("Cenas (cidades)", estTitulo);
        pularIntro = GUILayout.Toggle(pularIntro, " Arena: pular a intro ao carregar", GUILayout.Height(22f));
        Grade(Cidades);

        GUILayout.Space(8f);
        GUILayout.Label("Puzzles Rio", estTitulo);
        Grade(Puzzles);

        if (extras.Count > 0)
        {
            GUILayout.Space(8f);
            GUILayout.Label("Outras cenas da build", estTitulo);
            Grade(extras.ToArray());
        }
        GUILayout.Space(6f);
        GUILayout.Label("Cena atual: " + SceneManager.GetActiveScene().name + "   |   Botao cinza = cena fora da build.", estTexto);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix = antiga;
    }

    void DesenharGod()
    {
        string rotulo = controle.GodLigado ? "God mode: LIGADO (clique para desligar)" : "God mode: desligado (clique para ligar)";
        Color bg = GUI.backgroundColor;
        if (controle.GodLigado) GUI.backgroundColor = new Color(1f, 0.65f, 0.2f);
        if (GUILayout.Button(rotulo, estBotao, GUILayout.Height(34f))) controle.DefinirGod(!controle.GodLigado);
        GUI.backgroundColor = bg;

        if (controle.GodLigado && !controle.GodAplicado)
            GUILayout.Label("Aguardando o jogador aparecer na cena...", estTexto);

        controle.GodPersistente = GUILayout.Toggle(controle.GodPersistente, " Manter god mode ao trocar de cena", GUILayout.Height(22f));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Velocidade " + controle.VelNormal.ToString("0") + " m/s", estTexto, GUILayout.Width(150f));
        controle.VelNormal = GUILayout.HorizontalSlider(controle.VelNormal, 2f, 40f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Rapido (Shift) " + controle.VelRapido.ToString("0") + " m/s", estTexto, GUILayout.Width(150f));
        controle.VelRapido = GUILayout.HorizontalSlider(controle.VelRapido, 10f, 120f);
        GUILayout.EndHorizontal();
        GUILayout.Label("WASD move (relativo a camera) | Space sobe | Ctrl ou Q desce | Shift rapido | mouse olha. Sem colisao.", estTexto);
    }

    void Grade(IList<Cena> lista)
    {
        for (int i = 0; i < lista.Count; i += 2)
        {
            GUILayout.BeginHorizontal();
            BotaoCena(lista[i]);
            if (i + 1 < lista.Count) BotaoCena(lista[i + 1]);
            else GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }

    void BotaoCena(Cena c)
    {
        bool atual = c.nome == SceneManager.GetActiveScene().name;
        string txt = c.rotulo + (!c.ok ? "\n(fora da build)" : (atual ? "\n(cena atual)" : "\n" + c.nome));
        bool enabledAntes = GUI.enabled;
        Color bg = GUI.backgroundColor;
        GUI.enabled = enabledAntes && c.ok && !carregando;
        if (atual && c.ok) GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
        if (GUILayout.Button(txt, estBotao, GUILayout.Height(44f))) pedidoCena = c;
        GUI.backgroundColor = bg;
        GUI.enabled = enabledAntes;
    }

    void DesenharHud(float sw)
    {
        if (controle.GodLigado)
        {
            string t = controle.GodAplicado ? "GOD MODE  -  WASD / Space / Ctrl-Q / Shift  -  F2 painel" : "GOD MODE  -  aguardando jogador  -  F2 painel";
            GUI.Label(new Rect(sw * 0.5f - 230f, 8f, 460f, 24f), t, estHud);
        }
        else if (Time.unscaledTime < hintAte)
        {
            GUI.Label(new Rect(8f, 8f, 210f, 22f), "Admin: F2 = ferramentas", estHud);
        }
    }
}