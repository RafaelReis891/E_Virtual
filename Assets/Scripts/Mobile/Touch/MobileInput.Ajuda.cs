// MobileInput.Ajuda.cs
// Parte de entrada da tela de ajuda do celular (botao "?" e tela "Como jogar"). O desenho fica em MobileAjuda.cs.
// - Toque no botao "?" e "consumido": nao vira clique no mundo, nao aperta Interagir e nao pega/solta peca.
// - Com a ajuda aberta (AjudaAberta = true): os controles somem (ControlesNaTela = false) e nenhum toque chega
//   no jogo; o primeiro dedo vai so para a ajuda (ToqueNaAjuda), para rolar o texto e tocar em Fechar/X.
// - Posicao/tamanho do botao em unidades do canvas (ref 1920x1080), medidos do canto inferior direito (safe area),
//   igual ao botao Interagir. O MobileAjuda copia os valores do Inspector para ca a cada frame.
// - No PC (MobileInput.Ativo == false) nada disso roda.
using UnityEngine;

public static partial class MobileInput
{
    /// <summary>True enquanto a tela de ajuda esta aberta (controles escondidos, toques so para a ajuda).</summary>
    public static bool AjudaAberta { get; internal set; }
    /// <summary>MobileAjuda liga quando existe e MostrarBotaoAjuda = true.</summary>
    public static bool BotaoAjudaLigado { get; internal set; }

    /// <summary>Centro do botao "?" medido do canto inferior direito (unidades do canvas 1920x1080).</summary>
    public static Vector2 PosicaoBotaoAjuda = new Vector2(100f, 490f);
    /// <summary>Raio do botao "?" (unidades do canvas 1920x1080).</summary>
    public static float RaioBotaoAjuda = 52f;

    public static Vector2 BotaoAjudaCentro { get; private set; }
    public static float BotaoAjudaRaioPx { get; private set; }
    public static float UltimoToqueAjuda { get; private set; } = -10f;

    public static bool BotaoAjudaNaTela { get { return ControlesNaTela && BotaoAjudaLigado; } }

    /// <summary>True no frame em que o botao "?" foi tocado.</summary>
    public static bool TocouBotaoAjudaNoFrame { get { Processar(); return Ativo && frameTocouAjuda == Time.frameCount; } }

    /// <summary>Um dedo dentro da tela de ajuda (pixels de tela, origem embaixo a esquerda).</summary>
    public struct ToqueAjuda
    {
        public bool ativo;     // tem dedo na tela neste frame
        public bool comecou;   // encostou neste frame
        public bool terminou;  // saiu neste frame
        public Vector2 pos;
    }

    /// <summary>Dedo que esta mexendo na ajuda neste frame (so quando AjudaAberta).</summary>
    public static ToqueAjuda ToqueNaAjuda { get { Processar(); return toqueAjuda; } }

    static int frameTocouAjuda = -1;
    static ToqueAjuda toqueAjuda;
    static int dedoAjuda = MobileVirtualJoystick.SemDedo;

    /// <summary>Chamado no fim do CalcularLayout() (s = safe area em pixels, e = escala).</summary>
    static void CalcularLayoutAjuda(Rect s, float e)
    {
        BotaoAjudaCentro = new Vector2(s.xMax - PosicaoBotaoAjuda.x * e, s.yMin + PosicaoBotaoAjuda.y * e);
        BotaoAjudaRaioPx = Mathf.Max(1f, RaioBotaoAjuda * e);
    }

    /// <summary>Toque comecou no botao "?"? Se sim, marca o pedido e devolve true (toque consumido).</summary>
    static bool TocouBotaoAjuda(Vector2 p)
    {
        if (!BotaoAjudaNaTela) return false;
        float r = BotaoAjudaRaioPx * 1.2f; // um pouco maior que o desenho, mais facil de acertar
        if ((p - BotaoAjudaCentro).sqrMagnitude > r * r) return false;
        frameTocouAjuda = Time.frameCount;
        UltimoToqueAjuda = Time.unscaledTime;
        return true;
    }

    /// <summary>
    /// Chamado no Processar() logo depois de ler os toques. Com a ajuda aberta, guarda o dedo da ajuda e
    /// apaga todos os toques (nada chega nos joysticks, no Interagir, no Clique() nem no arraste).
    /// </summary>
    static void ProcessarToquesAjuda()
    {
        if (!AjudaAberta)
        {
            toqueAjuda = default(ToqueAjuda);
            dedoAjuda = MobileVirtualJoystick.SemDedo;
            return;
        }

        ToqueAjuda t = default(ToqueAjuda);
        bool achou = false;
        if (dedoAjuda != MobileVirtualJoystick.SemDedo)
        {
            for (int i = 0; i < pontos.Count; i++)
            {
                if (pontos[i].id != dedoAjuda) continue;
                t.ativo = true;
                t.comecou = pontos[i].comecou;
                t.terminou = pontos[i].terminou;
                t.pos = pontos[i].pos;
                achou = true;
                break;
            }
            if (!achou) dedoAjuda = MobileVirtualJoystick.SemDedo;
        }
        if (!achou)
        {
            // so um dedo que ENCOSTOU com a ajuda aberta conta (o dedo que tocou no "?" nao vira toque na ajuda)
            for (int i = 0; i < pontos.Count; i++)
            {
                if (!pontos[i].comecou) continue;
                dedoAjuda = pontos[i].id;
                t.ativo = true;
                t.comecou = true;
                t.terminou = pontos[i].terminou;
                t.pos = pontos[i].pos;
                break;
            }
        }
        if (t.terminou) dedoAjuda = MobileVirtualJoystick.SemDedo;
        toqueAjuda = t;
        pontos.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetarAjuda()
    {
        AjudaAberta = false;
        BotaoAjudaLigado = false;
        frameTocouAjuda = -1;
        toqueAjuda = default(ToqueAjuda);
        dedoAjuda = MobileVirtualJoystick.SemDedo;
        UltimoToqueAjuda = -10f;
    }
}
