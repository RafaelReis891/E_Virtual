using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ferramentas de ADMIN (so admin; jogador normal nunca ve nem usa):
//  - F2 abre/fecha o painel (Esc fecha). Painel aberto: cursor livre e input do jogo bloqueado (AdminPlayerControl).
//  - Interacao por polling de mouse/teclado (AdminInput), nao por eventos IMGUI: ver comentario na secao GUI.
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

        if (aberto && !pedidoFechar) ProcessarPainel();
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
        scrollY = 0f;
        arrastandoBarra = false;
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
    // O IMGUI aqui so DESENHA (evento Repaint). Cliques, roda e teclas sao lidos por polling no Update
    // (AdminInput: Mouse.current / Input legado), o mesmo caminho que o RemoteBannerClickDriver usa e que funciona no WebGL.
    // Motivo: os botoes GUI.Button/GUILayout dependem dos eventos de mouse do IMGUI (InputForUI + Input System novo no WebGL),
    // e na build o painel abria mas nao reagia ao clique. O layout e um so (Passo): o mesmo codigo desenha e testa o clique.

    const float Barra = 12f;

    static float Escala() { return Mathf.Clamp(Screen.height / 800f, 1f, 2.5f); }

    // geometria em coordenadas "virtuais" (pixels da tela / Escala())
    Rect rPainel, rFechar, rView, rTrilho, rRodape;
    float alturaConteudo;
    float scrollY;
    bool arrastandoBarra;
    Vector2 mouseGui;
    string ultimoClique = "-";
    int cliquesLidos;

    // estado do passo de layout
    bool pDesenha, pClicou;
    float pY, pLargura;
    Vector2 pPonto, pHover;

    void CalcularGeometria()
    {
        float esc = Escala();
        float sw = Screen.width / esc, sh = Screen.height / esc;
        float w = Mathf.Min(600f, sw - 20f), h = Mathf.Min(640f, sh - 20f);
        rPainel = new Rect((sw - w) * 0.5f, (sh - h) * 0.5f, w, h);
        rFechar = new Rect(rPainel.xMax - 12f - 110f, rPainel.y + 10f, 110f, 26f);
        rView = new Rect(rPainel.x + 12f, rPainel.y + 66f, w - 24f, Mathf.Max(60f, h - 66f - 46f));
        rTrilho = new Rect(rView.xMax - 8f, rView.y, 8f, rView.height);
        rRodape = new Rect(rPainel.x + 12f, rPainel.yMax - 40f, w - 24f, 34f);
    }

    // Update: mouse/roda/teclas do painel aberto.
    void ProcessarPainel()
    {
        CalcularGeometria();
        float esc = Escala();
        Vector2 px = AdminInput.PosicaoMouse();
        mouseGui = new Vector2(px.x / esc, (Screen.height - px.y) / esc);

        float maxScroll = Mathf.Max(0f, alturaConteudo - rView.height);
        float roda = AdminInput.Roda();
        if (roda != 0f && rPainel.Contains(mouseGui)) scrollY -= Mathf.Sign(roda) * 56f;

        bool pressionou = AdminInput.CliquePressionado();
        if (!AdminInput.CliqueSegurando()) arrastandoBarra = false;
        if (pressionou && maxScroll > 0f && new Rect(rTrilho.x - 8f, rTrilho.y, rTrilho.width + 8f, rTrilho.height).Contains(mouseGui)) arrastandoBarra = true;
        if (arrastandoBarra) scrollY = Mathf.Clamp01((mouseGui.y - rView.y) / rView.height) * maxScroll;
        scrollY = Mathf.Clamp(scrollY, 0f, maxScroll);

        // atalhos de teclado (reserva caso o mouse falhe): G = god mode, 1-9 = cidades
        if (AdminInput.Apertou(AdminInput.Tecla.G))
        {
            controle.DefinirGod(!controle.GodLigado);
            ultimoClique = "tecla G";
        }
        for (int i = 0; i < 9 && i < Cidades.Length; i++)
        {
            if (AdminInput.Apertou((AdminInput.Tecla)((int)AdminInput.Tecla.N1 + i)) && Cidades[i].ok && !carregando)
            {
                pedidoCena = Cidades[i];
                ultimoClique = "tecla " + (i + 1);
            }
        }

        if (pressionou && !arrastandoBarra)
        {
            if (rFechar.Contains(mouseGui))
            {
                pedidoFechar = true;
                ultimoClique = "Fechar";
            }
            else if (rView.Contains(mouseGui) && !rTrilho.Contains(mouseGui))
            {
                var pc = new Vector2(mouseGui.x - rView.x, mouseGui.y - rView.y + scrollY);
                ultimoClique = "(" + (int)mouseGui.x + "," + (int)mouseGui.y + ") sem botao";
                Passo(false, rView.width - Barra, pc, true);
            }
            else ultimoClique = "(" + (int)mouseGui.x + "," + (int)mouseGui.y + ") fora da area";
            cliquesLidos++;
            EmuseuDebug.Info(Tag, "clique no painel: " + ultimoClique + " cursor=" + Cursor.lockState + " visivel=" + Cursor.visible);
        }
    }

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

        float esc = Escala();
        Matrix4x4 antiga = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(esc, esc, 1f));
        float sw = Screen.width / esc;

        if (!aberto)
        {
            DesenharHud(sw);
            GUI.matrix = antiga;
            return;
        }

        if (Event.current.type == EventType.Repaint)
        {
            CalcularGeometria();
            GUI.Box(rPainel, GUIContent.none, estBox);
            GUI.Label(new Rect(rPainel.x + 12f, rPainel.y + 8f, 300f, 28f), "Admin Tools", estTitulo);
            DesenharBotao(rFechar, "Fechar (F2)", true, Color.white, rFechar.Contains(mouseGui));
#if UNITY_EDITOR
            string info = "Modo Editor (teste). No jogo so aparece para admin (root).";
#else
            string info = "Admin (root).";
#endif
            GUI.Label(new Rect(rPainel.x + 12f, rPainel.y + 40f, rPainel.width - 24f, 22f), info, estTexto);

            GUI.BeginGroup(rView);
            alturaConteudo = Passo(true, rView.width - Barra, Vector2.zero, false);
            GUI.EndGroup();

            if (alturaConteudo > rView.height)
            {
                GUI.DrawTexture(rTrilho, fundoHud);
                float maxScroll = alturaConteudo - rView.height;
                float th = Mathf.Max(24f, rView.height * rView.height / alturaConteudo);
                float ty = rView.y + (rView.height - th) * Mathf.Clamp01(scrollY / maxScroll);
                Color c = GUI.color;
                GUI.color = new Color(0.7f, 0.75f, 0.85f, 0.9f);
                GUI.DrawTexture(new Rect(rTrilho.x, ty, rTrilho.width, th), Texture2D.whiteTexture);
                GUI.color = c;
            }

            string diag = "Cena: " + SceneManager.GetActiveScene().name + " | botao cinza = fora da build | mouse " + (int)mouseGui.x + "," + (int)mouseGui.y +
                          " | cursor " + Cursor.lockState + (Cursor.visible ? "" : " (oculto)") + " | ultimo clique: " + ultimoClique + " (n=" + cliquesLidos + ")";
            GUI.Label(rRodape, diag, estTexto);
        }
        GUI.matrix = antiga;
    }

    void DesenharBotao(Rect d, string txt, bool hab, Color cor, bool hover)
    {
        Color bg = GUI.backgroundColor, c = GUI.color;
        GUI.backgroundColor = cor;
        if (!hab) GUI.color = new Color(1f, 1f, 1f, 0.45f);
        estBotao.Draw(d, new GUIContent(txt), hover && hab, false, false, false);
        GUI.backgroundColor = bg;
        GUI.color = c;
    }

    // ---------- layout (um so codigo para desenhar e para testar o clique) ----------

    Rect Linha(float h, float depois)
    {
        var r = new Rect(0f, pY, pLargura, h);
        pY += h + depois;
        return r;
    }

    void Rotulo(Rect r, string t, GUIStyle st)
    {
        if (!pDesenha) return;
        r.y -= scrollY;
        GUI.Label(r, t, st);
    }

    // true so no passo de clique, quando o clique caiu no botao (e habilitado)
    bool Botao(Rect r, string txt, bool hab, Color cor, string nome)
    {
        if (pDesenha)
        {
            Rect d = r;
            d.y -= scrollY;
            DesenharBotao(d, txt, hab, cor, r.Contains(pHover));
            return false;
        }
        if (!pClicou || !hab || !r.Contains(pPonto)) return false;
        pClicou = false;
        ultimoClique = nome;
        return true;
    }

    bool Marcador(Rect r, bool valor, string txt, string nome)
    {
        if (pDesenha)
        {
            Rect d = r;
            d.y -= scrollY;
            GUI.Label(d, (valor ? "[x]  " : "[   ]  ") + txt, estTexto);
            return valor;
        }
        if (pClicou && r.Contains(pPonto))
        {
            pClicou = false;
            ultimoClique = nome;
            return !valor;
        }
        return valor;
    }

    // Retorna a altura total do conteudo.
    float Passo(bool desenha, float largura, Vector2 ponto, bool clicou)
    {
        pDesenha = desenha;
        pLargura = largura;
        pPonto = ponto;
        pClicou = clicou;
        pY = 0f;
        pHover = new Vector2(mouseGui.x - rView.x, mouseGui.y - rView.y + scrollY);

        Rotulo(Linha(28f, 2f), "God mode", estTitulo);
        string rotulo = controle.GodLigado ? "God mode: LIGADO (clique para desligar)  [G]" : "God mode: desligado (clique para ligar)  [G]";
        if (Botao(Linha(34f, 4f), rotulo, true, controle.GodLigado ? new Color(1f, 0.65f, 0.2f) : Color.white, "God mode"))
            controle.DefinirGod(!controle.GodLigado);
        if (controle.GodLigado && !controle.GodAplicado)
            Rotulo(Linha(20f, 2f), "Aguardando o jogador aparecer na cena...", estTexto);
        controle.GodPersistente = Marcador(Linha(24f, 2f), controle.GodPersistente, "Manter god mode ao trocar de cena", "Manter god mode");

        Rect l = Linha(30f, 2f);
        Rotulo(new Rect(l.x, l.y, l.width - 120f, l.height), "Velocidade " + controle.VelNormal.ToString("0") + " m/s", estTexto);
        if (Botao(new Rect(l.xMax - 112f, l.y, 54f, l.height), "-", true, Color.white, "Velocidade -")) controle.VelNormal = Mathf.Max(2f, controle.VelNormal - 2f);
        if (Botao(new Rect(l.xMax - 54f, l.y, 54f, l.height), "+", true, Color.white, "Velocidade +")) controle.VelNormal = Mathf.Min(40f, controle.VelNormal + 2f);

        l = Linha(30f, 2f);
        Rotulo(new Rect(l.x, l.y, l.width - 120f, l.height), "Rapido (Shift) " + controle.VelRapido.ToString("0") + " m/s", estTexto);
        if (Botao(new Rect(l.xMax - 112f, l.y, 54f, l.height), "-", true, Color.white, "Rapido -")) controle.VelRapido = Mathf.Max(10f, controle.VelRapido - 10f);
        if (Botao(new Rect(l.xMax - 54f, l.y, 54f, l.height), "+", true, Color.white, "Rapido +")) controle.VelRapido = Mathf.Min(120f, controle.VelRapido + 10f);

        Rotulo(Linha(40f, 6f), "WASD move (relativo a camera) | Space sobe | Ctrl ou Q desce | Shift rapido | mouse olha. Sem colisao.", estTexto);

        pY += 4f;
        Rotulo(Linha(28f, 2f), "Cenas (cidades)  [teclas 1-9]", estTitulo);
        pularIntro = Marcador(Linha(24f, 4f), pularIntro, "Arena: pular a intro ao carregar", "Pular intro");
        Grade(Cidades, true);

        pY += 8f;
        Rotulo(Linha(28f, 2f), "Puzzles Rio", estTitulo);
        Grade(Puzzles, false);

        if (extras.Count > 0)
        {
            pY += 8f;
            Rotulo(Linha(28f, 2f), "Outras cenas da build", estTitulo);
            Grade(extras.ToArray(), false);
        }
        pY += 6f;
        return pY;
    }

    void Grade(IList<Cena> lista, bool numerar)
    {
        float lw = (pLargura - 6f) * 0.5f;
        for (int i = 0; i < lista.Count; i += 2)
        {
            Rect l = Linha(44f, 4f);
            BotaoCena(new Rect(0f, l.y, lw, l.height), lista[i], numerar ? i + 1 : 0);
            if (i + 1 < lista.Count) BotaoCena(new Rect(lw + 6f, l.y, lw, l.height), lista[i + 1], numerar ? i + 2 : 0);
        }
    }

    void BotaoCena(Rect r, Cena c, int numero)
    {
        bool atual = c.nome == SceneManager.GetActiveScene().name;
        string txt = (numero > 0 ? "[" + numero + "] " : "") + c.rotulo + (!c.ok ? "\n(fora da build)" : (atual ? "\n(cena atual)" : "\n" + c.nome));
        bool hab = c.ok && !carregando;
        Color cor = (atual && c.ok) ? new Color(0.5f, 0.9f, 0.5f) : Color.white;
        if (Botao(r, txt, hab, cor, c.rotulo)) pedidoCena = c;
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