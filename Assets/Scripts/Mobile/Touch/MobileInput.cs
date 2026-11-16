// MobileInput.cs
// Entrada de toque para o WebGL no celular (mesma build do PC).
// - So liga no WebGL de celular/tablet (Application.isMobilePlatform), ou quando forcado:
//     * URL com ?mobile=1 (ou ?mobile=0 para desligar no celular)
//     * Editor: menu Mobile > Tools > Simular controles touch no Play
//     * Codigo/site: MobileInput.Forcar(true/false/null) ou SendMessage("MobileTouchControls", "SetMobile", "1"|"0"|"auto")
// - No PC (sem forcar) nada muda: Clique()/PosicaoClique()/ToqueLivre() devolvem o valor original.
// - Le os toques sozinho (Input legado ou Input System), sem EventSystem, com multitoque.
// API para outros scripts:
//     MobileInput.Ativo, MobileInput.Mover, MobileInput.Olhar, MobileInput.InteragirPressionado
//     MobileInput.ControlesVisiveis (esconde tudo), MobileInput.BotaoInteragirVisivel (esconde so o botao)
//     MobileInput.Clique(Input.GetMouseButtonDown(0))   -> clique original OU botao Interagir OU toque no mundo
//     MobileInput.PosicaoClique(Input.mousePosition)     -> centro da tela (mira) se veio do botao, senao o ponto tocado
//     MobileInput.ToqueLivre(Input.GetMouseButtonDown(0)) -> para arrastar: ignora toques que cairam nos controles
//     Arrastar no celular (toca para pegar, toca para soltar): ver MobileInput.Arraste.cs
//     Sensibilidade da camera (< numero >, provisorio): ver MobileInput.Sensibilidade.cs
//     Tela cheia / horizontal / aviso de girar o celular: ver MobileTela.cs (MobileInput.TelaBloqueada)
//     Botao "?" e tela de ajuda "Como jogar": ver MobileInput.Ajuda.cs e MobileAjuda.cs (MobileInput.AjudaAberta)
using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class MobileInput
{
    public const string PrefSimularEditor = "EVirtual.Mobile.SimularTouch";

    // ------------------------------------------------------------------ ativacao
    static bool? ativoCache;
    static bool? forcado;

    /// <summary>True quando os controles de toque estao ligados (celular, ?mobile=1, simulacao no Editor ou forcado).</summary>
    public static bool Ativo
    {
        get
        {
            if (!ativoCache.HasValue) ativoCache = Calcular();
            return ativoCache.Value;
        }
    }

    /// <summary>Por que esta ligado/desligado (para log).</summary>
    public static string Motivo { get; private set; } = "";

    /// <summary>Chamado quando Ativo muda (ex.: menu do Editor durante o Play).</summary>
    public static event Action<bool> AoMudarAtivo;

    /// <summary>true = liga, false = desliga, null = volta para a deteccao automatica.</summary>
    public static void Forcar(bool? valor)
    {
        forcado = valor;
        Reavaliar();
    }

    public static void Reavaliar()
    {
        bool antes = ativoCache.HasValue && ativoCache.Value;
        ativoCache = Calcular();
        if (antes != ativoCache.Value)
        {
            ResetarEstado();
            Action<bool> ev = AoMudarAtivo;
            if (ev != null) ev(ativoCache.Value);
        }
    }

    static bool Calcular()
    {
        if (forcado.HasValue) { Motivo = "forcado por codigo/SendMessage"; return forcado.Value; }
#if UNITY_EDITOR
        if (UnityEditor.EditorPrefs.GetBool(PrefSimularEditor, false)) { Motivo = "Editor: menu Simular controles touch no Play"; return true; }
#endif
        bool? url = LerParametroUrl();
        if (url.HasValue) { Motivo = "URL ?mobile=" + (url.Value ? "1" : "0"); return url.Value; }
        if (!Application.isEditor && Application.platform == RuntimePlatform.WebGLPlayer && Application.isMobilePlatform)
        {
            Motivo = "WebGL em celular/tablet";
            return true;
        }
        Motivo = "PC (sem controles de toque)";
        return false;
    }

    static bool? LerParametroUrl()
    {
        string url;
        try { url = Application.absoluteURL; } catch (Exception) { return null; }
        if (string.IsNullOrEmpty(url)) return null;
        int q = url.IndexOf('?');
        if (q < 0) return null;
        string query = url.Substring(q + 1);
        foreach (string parBruto in query.Split('&', '#'))
        {
            if (string.IsNullOrEmpty(parBruto)) continue;
            int eq = parBruto.IndexOf('=');
            string k = eq >= 0 ? parBruto.Substring(0, eq) : parBruto;
            if (!string.Equals(k.Trim(), "mobile", StringComparison.OrdinalIgnoreCase)) continue;
            string v = "1";
            if (eq >= 0)
            {
                try { v = Uri.UnescapeDataString(parBruto.Substring(eq + 1)); } catch (Exception) { v = parBruto.Substring(eq + 1); }
                v = v.Trim().ToLowerInvariant();
            }
            if (v == "" || v == "1" || v == "true" || v == "sim" || v == "on" || v == "yes") return true;
            if (v == "0" || v == "false" || v == "nao" || v == "off" || v == "no") return false;
        }
        return null;
    }

    // ------------------------------------------------------------------ visibilidade
    /// <summary>API: false esconde joysticks e botao (ex.: um painel/puzzle precisa da tela).</summary>
    public static bool ControlesVisiveis = true;
    /// <summary>API: false esconde so o botao Interagir.</summary>
    public static bool BotaoInteragirVisivel = true;
    /// <summary>Atualizado pelo MobileTouchControls: existe jogador ativo que aceita controle.</summary>
    public static bool JogadorPronto { get; internal set; }
    /// <summary>MobileTela liga quando o celular esta em pe (aviso "gire o celular"): esconde os controles e ignora toques.</summary>
    public static bool TelaBloqueada { get; internal set; }

    public static bool ControlesNaTela { get { return Ativo && ControlesVisiveis && JogadorPronto && !TelaBloqueada && !AjudaAberta; } }
    public static bool BotaoNaTela { get { return ControlesNaTela && BotaoInteragirVisivel; } }

    // ------------------------------------------------------------------ layout (unidades do canvas, ref 1920x1080)
    public static float RaioBase = 130f;
    public static float RaioKnob = 58f;
    public static float RaioBotao = 88f;
    /// <summary>Distancia do centro dos joysticks ate o canto de baixo (dentro da safe area).</summary>
    public static Vector2 MargemJoystick = new Vector2(250f, 250f);
    /// <summary>Centro do botao Interagir medido a partir do canto inferior direito (dentro da safe area).</summary>
    public static Vector2 PosicaoBotao = new Vector2(500f, 480f);

    public static readonly MobileVirtualJoystick Esquerdo = new MobileVirtualJoystick();
    public static readonly MobileVirtualJoystick Direito = new MobileVirtualJoystick();
    public static Vector2 BotaoCentro { get; private set; }
    public static float BotaoRaioPx { get; private set; }
    /// <summary>Escala tela/canvas (igual a do CanvasScaler 1920x1080, match 0.5).</summary>
    public static float Escala { get; private set; } = 1f;

    static void CalcularLayout()
    {
        float w = Mathf.Max(1f, Screen.width);
        float h = Mathf.Max(1f, Screen.height);
        Escala = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(w / 1920f, 2f), Mathf.Log(h / 1080f, 2f), 0.5f));
        Rect s = Screen.safeArea;
        if (s.width <= 1f || s.height <= 1f) s = new Rect(0f, 0f, w, h);
        float e = Escala;

        Esquerdo.Centro = new Vector2(s.xMin + MargemJoystick.x * e, s.yMin + MargemJoystick.y * e);
        Direito.Centro = new Vector2(s.xMax - MargemJoystick.x * e, s.yMin + MargemJoystick.y * e);
        Esquerdo.Raio = Direito.Raio = RaioBase * e * 0.85f;
        Esquerdo.RaioAtivacao = Direito.RaioAtivacao = RaioBase * e * 1.6f;

        BotaoCentro = new Vector2(s.xMax - PosicaoBotao.x * e, s.yMin + PosicaoBotao.y * e);
        BotaoRaioPx = RaioBotao * e;
        CalcularLayoutSensibilidade(s, e);
        CalcularLayoutAjuda(s, e);
    }

    // ------------------------------------------------------------------ leitura por frame
    struct Ponto
    {
        public int id;
        public Vector2 pos;
        public bool comecou;
        public bool terminou;
    }

    const int IdMouse = -100;
    static readonly List<Ponto> pontos = new List<Ponto>(10);
    static int frameProcessado = -1;
    static bool interagirNoFrame;
    static bool temToqueMundo;
    static Vector2 toqueMundo;
    static int dedoBotao = MobileVirtualJoystick.SemDedo;

    /// <summary>Le os toques deste frame (uma vez so por frame; pode ser chamado de qualquer lugar).</summary>
    public static void Processar()
    {
        int f = Time.frameCount;
        if (f == frameProcessado) return;
        frameProcessado = f;
        interagirNoFrame = false;
        temToqueMundo = false;

        if (!Ativo)
        {
            if (Esquerdo.Ativo) Esquerdo.Soltar();
            if (Direito.Ativo) Direito.Soltar();
            dedoBotao = MobileVirtualJoystick.SemDedo;
            return;
        }

        CalcularLayout();
        LerPontos();
        if (TelaBloqueada) pontos.Clear(); // aviso de girar o celular na frente: nenhum toque conta
        ProcessarToquesAjuda(); // tela de ajuda aberta: toques so para a ajuda (MobileInput.Ajuda.cs)

        bool naTela = ControlesNaTela;
        bool botao = BotaoNaTela;
        if (!naTela) { Esquerdo.Soltar(); Direito.Soltar(); }
        if (!botao) dedoBotao = MobileVirtualJoystick.SemDedo;

        // dedo que sumiu sem "Ended" (perda de foco etc.)
        if (Esquerdo.Ativo && !Existe(Esquerdo.Dedo)) Esquerdo.Soltar();
        if (Direito.Ativo && !Existe(Direito.Dedo)) Direito.Soltar();
        if (dedoBotao != MobileVirtualJoystick.SemDedo && !Existe(dedoBotao)) dedoBotao = MobileVirtualJoystick.SemDedo;

        float raioBotaoToque = BotaoRaioPx * 1.15f;
        for (int i = 0; i < pontos.Count; i++)
        {
            Ponto p = pontos[i];
            if (p.comecou)
            {
                if (TocouBotaoAjuda(p.pos))
                {
                    // toque no botao "?" da ajuda: nao vira clique no mundo, Interagir nem pega peca (MobileInput.Ajuda.cs)
                }
                else if (TocouAjusteSensibilidade(p.pos))
                {
                    // toque nos botoes < > da sensibilidade: nao vira clique no mundo nem pega peca
                }
                else if (botao && (p.pos - BotaoCentro).sqrMagnitude <= raioBotaoToque * raioBotaoToque)
                {
                    interagirNoFrame = true;
                    dedoBotao = p.id;
                }
                else if (naTela && !Esquerdo.Ativo && Esquerdo.Contem(p.pos)) Esquerdo.Pegar(p.id, p.pos);
                else if (naTela && !Direito.Ativo && Direito.Contem(p.pos)) Direito.Pegar(p.id, p.pos);
                else if (!temToqueMundo)
                {
                    temToqueMundo = true;
                    toqueMundo = p.pos;
                    idToqueMundo = p.id;
                }
            }
            else
            {
                if (p.id == Esquerdo.Dedo) Esquerdo.Mover(p.pos);
                else if (p.id == Direito.Dedo) Direito.Mover(p.pos);
            }

            if (p.terminou)
            {
                if (p.id == Esquerdo.Dedo) Esquerdo.Soltar();
                if (p.id == Direito.Dedo) Direito.Soltar();
                if (p.id == dedoBotao) dedoBotao = MobileVirtualJoystick.SemDedo;
            }
        }

        AtualizarArraste();
    }

    static bool Existe(int id)
    {
        for (int i = 0; i < pontos.Count; i++) if (pontos[i].id == id) return true;
        return false;
    }

    static void LerPontos()
    {
        pontos.Clear();
#if ENABLE_LEGACY_INPUT_MANAGER
        int n = 0;
        try { n = Input.touchCount; } catch (Exception) { n = 0; }
        for (int i = 0; i < n; i++)
        {
            UnityEngine.Touch t = Input.GetTouch(i);
            pontos.Add(new Ponto
            {
                id = t.fingerId,
                pos = t.position,
                comecou = t.phase == UnityEngine.TouchPhase.Began,
                terminou = t.phase == UnityEngine.TouchPhase.Ended || t.phase == UnityEngine.TouchPhase.Canceled
            });
        }
        if (n == 0 && UsarMouse())
        {
            bool down = Input.GetMouseButtonDown(0);
            bool held = Input.GetMouseButton(0);
            bool up = Input.GetMouseButtonUp(0);
            if (down || held || up)
            {
                Vector3 mp = Input.mousePosition;
                pontos.Add(new Ponto { id = IdMouse, pos = new Vector2(mp.x, mp.y), comecou = down, terminou = up });
            }
        }
#elif ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Touchscreen ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts != null)
        {
            foreach (UnityEngine.InputSystem.Controls.TouchControl tc in ts.touches)
            {
                UnityEngine.InputSystem.TouchPhase fase = tc.phase.ReadValue();
                if (fase == UnityEngine.InputSystem.TouchPhase.None) continue;
                bool fim = fase == UnityEngine.InputSystem.TouchPhase.Ended || fase == UnityEngine.InputSystem.TouchPhase.Canceled;
                if (fim && !tc.press.wasReleasedThisFrame) continue;
                pontos.Add(new Ponto
                {
                    id = tc.touchId.ReadValue(),
                    pos = tc.position.ReadValue(),
                    comecou = tc.press.wasPressedThisFrame,
                    terminou = fim
                });
            }
        }
        if (pontos.Count == 0 && UsarMouse())
        {
            UnityEngine.InputSystem.Mouse m = UnityEngine.InputSystem.Mouse.current;
            if (m != null)
            {
                bool down = m.leftButton.wasPressedThisFrame;
                bool held = m.leftButton.isPressed;
                bool up = m.leftButton.wasReleasedThisFrame;
                if (down || held || up)
                    pontos.Add(new Ponto { id = IdMouse, pos = m.position.ReadValue(), comecou = down, terminou = up });
            }
        }
#endif
    }

    /// <summary>Mouse vira "dedo" no Editor e em navegador sem tela de toque (teste com ?mobile=1 no PC).</summary>
    static bool UsarMouse()
    {
        if (Application.isEditor) return true;
#if ENABLE_LEGACY_INPUT_MANAGER
        return !Input.touchSupported;
#elif ENABLE_INPUT_SYSTEM
        return UnityEngine.InputSystem.Touchscreen.current == null;
#else
        return false;
#endif
    }

    // ------------------------------------------------------------------ API de leitura
    /// <summary>Joystick esquerdo, -1..1.</summary>
    public static Vector2 Mover { get { Processar(); return Ativo ? Esquerdo.Valor : Vector2.zero; } }
    /// <summary>Joystick direito, -1..1.</summary>
    public static Vector2 Olhar { get { Processar(); return Ativo ? Direito.Valor : Vector2.zero; } }
    /// <summary>Botao Interagir apertado NESTE frame.</summary>
    public static bool InteragirPressionado { get { Processar(); return Ativo && interagirNoFrame; } }
    /// <summary>Botao Interagir sendo segurado.</summary>
    public static bool InteragirSegurado { get { Processar(); return Ativo && dedoBotao != MobileVirtualJoystick.SemDedo; } }

    /// <summary>
    /// Troca de "Input.GetMouseButtonDown(0)". No PC devolve o original.
    /// No mobile: true se apertou Interagir ou tocou no mundo (fora dos controles).
    /// </summary>
    public static bool Clique(bool cliqueOriginal)
    {
        if (!Ativo) return cliqueOriginal;
        Processar();
        return !ArrasteUsaToque() && (interagirNoFrame || temToqueMundo);
    }

    /// <summary>
    /// Troca de "Input.mousePosition" logo depois de um Clique(). No PC devolve o original.
    /// No mobile: centro da tela (mira) se veio do botao Interagir, senao o ponto tocado.
    /// </summary>
    public static Vector3 PosicaoClique(Vector3 posicaoOriginal)
    {
        if (!Ativo) return posicaoOriginal;
        Processar();
        if (interagirNoFrame) return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        if (temToqueMundo) return new Vector3(toqueMundo.x, toqueMundo.y, 0f);
        return posicaoOriginal;
    }

    /// <summary>
    /// Para puzzles de arrastar: no mobile so deixa passar o clique se o toque comecou fora dos controles.
    /// Agora e o mesmo que Apertou(): toque no mundo ou botao Interagir pega a peca (ver MobileInput.Arraste.cs).
    /// </summary>
    public static bool ToqueLivre(bool cliqueOriginal)
    {
        if (!Ativo) return cliqueOriginal;
        Processar();
        return Apertou(cliqueOriginal);
    }

    // ------------------------------------------------------------------ reset
    public static void ResetarEstado()
    {
        Esquerdo.Soltar();
        Direito.Soltar();
        dedoBotao = MobileVirtualJoystick.SemDedo;
        interagirNoFrame = false;
        temToqueMundo = false;
        frameProcessado = -1;
        pontos.Clear();
        LimparArraste();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetarDominio()
    {
        ativoCache = null;
        forcado = null;
        AoMudarAtivo = null;
        ControlesVisiveis = true;
        BotaoInteragirVisivel = true;
        JogadorPronto = false;
        TelaBloqueada = false;
        ResetarEstado();
    }
}
