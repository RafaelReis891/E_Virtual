// MobileInput.Sensibilidade.cs
// Sensibilidade da camera do joystick direito no celular (PROVISORIO, para achar o valor bom).
// - Multiplicador 0.2..3.0 em passos de 0.1, sobre a base de 105/70 graus por segundo.
// - Valor fixo escolhido pelo Teco nos testes: SensibilidadePadrao = 0.3 (ajuste escondido).
// - Com o ajuste ESCONDIDO vale sempre SensibilidadePadrao (o valor salvo no PlayerPrefs e ignorado).
// - Com o ajuste na tela, usa o valor salvo no PlayerPrefs ("EVirtual.Mobile.SensibilidadeCamera"), ou o padrao se nao tiver.
// - Na tela: botao "<", numero e botao ">" no canto de cima a esquerda (desenhado pelo MobileTouchControls.Sensibilidade.cs).
// - Para mostrar o ajuste de novo: MostrarAjusteSensibilidadePadrao = true (ou MobileInput.MostrarAjusteSensibilidade = true por codigo).
// - Toques nesses botoes nao viram clique no mundo, nao pegam peca e nao mexem nos joysticks (ver Processar() em MobileInput.cs).
using System.Globalization;
using UnityEngine;

public static partial class MobileInput
{
    public const string PrefSensibilidade = "EVirtual.Mobile.SensibilidadeCamera";
    /// <summary>true = mostra o ajuste "&lt; 0.3 &gt;" na tela de novo. false = escondido e fixo em SensibilidadePadrao.</summary>
    public const bool MostrarAjusteSensibilidadePadrao = false;
    /// <summary>Sensibilidade usada com o ajuste escondido (e o valor inicial quando ele aparece).</summary>
    public const float SensibilidadePadrao = 0.3f;
    public const float SensibilidadeMin = 0.2f;
    public const float SensibilidadeMax = 3f;
    public const float SensibilidadePasso = 0.1f;

    /// <summary>API: false esconde o ajuste "&lt; 1.0 &gt;" da tela.</summary>
    public static bool MostrarAjusteSensibilidade = MostrarAjusteSensibilidadePadrao;

    // layout (unidades do canvas, ref 1920x1080)
    /// <summary>Distancia do canto de cima a esquerda (dentro da safe area) ate o ajuste.</summary>
    public static Vector2 MargemAjusteSensibilidade = new Vector2(40f, 40f);
    public static float LadoBotaoSensibilidade = 86f;
    public static float LarguraNumeroSensibilidade = 120f;

    /// <summary>Retangulos em pixels de tela (origem embaixo a esquerda, igual ao toque).</summary>
    public static Rect AjusteMenosPx { get; private set; }
    public static Rect AjusteNumeroPx { get; private set; }
    public static Rect AjusteMaisPx { get; private set; }
    /// <summary>Hora (unscaledTime) do ultimo toque em cada botao, para o visual piscar.</summary>
    public static float UltimoToqueMenos { get; private set; } = -10f;
    public static float UltimoToqueMais { get; private set; } = -10f;

    public static bool AjusteSensibilidadeNaTela { get { return ControlesNaTela && MostrarAjusteSensibilidade; } }

    static float? sensibilidade;

    /// <summary>Multiplicador da velocidade da camera no joystick direito. Ajuste escondido = sempre SensibilidadePadrao.</summary>
    public static float SensibilidadeCamera
    {
        get
        {
            if (!MostrarAjusteSensibilidade) return Arredondar(SensibilidadePadrao); // ignora o valor salvo antes
            if (!sensibilidade.HasValue)
            {
                float v = SensibilidadePadrao;
                try { v = PlayerPrefs.GetFloat(PrefSensibilidade, SensibilidadePadrao); } catch (System.Exception) { v = SensibilidadePadrao; }
                if (float.IsNaN(v) || float.IsInfinity(v)) v = SensibilidadePadrao;
                sensibilidade = Arredondar(v);
            }
            return sensibilidade.Value;
        }
        set
        {
            float v = Arredondar(value);
            if (sensibilidade.HasValue && Mathf.Approximately(sensibilidade.Value, v)) return;
            sensibilidade = v;
            try
            {
                PlayerPrefs.SetFloat(PrefSensibilidade, v);
                PlayerPrefs.Save();
            }
            catch (System.Exception) { }
        }
    }

    /// <summary>Texto do numero, ex.: "1.0".</summary>
    public static string SensibilidadeTexto { get { return SensibilidadeCamera.ToString("0.0", CultureInfo.InvariantCulture); } }

    /// <summary>passos = -1 (botao &lt;) ou +1 (botao &gt;).</summary>
    public static void MudarSensibilidade(int passos)
    {
        SensibilidadeCamera = SensibilidadeCamera + passos * SensibilidadePasso;
        Debug.Log("[MobileTouch] sensibilidade da camera = " + SensibilidadeTexto);
    }

    static float Arredondar(float v)
    {
        v = Mathf.Clamp(v, SensibilidadeMin, SensibilidadeMax);
        return Mathf.Round(v / SensibilidadePasso) * SensibilidadePasso;
    }

    /// <summary>Chamado no fim do CalcularLayout() (s = safe area em pixels, e = escala).</summary>
    static void CalcularLayoutSensibilidade(Rect s, float e)
    {
        float lado = LadoBotaoSensibilidade * e;
        float num = LarguraNumeroSensibilidade * e;
        float x0 = s.xMin + MargemAjusteSensibilidade.x * e;
        float y0 = s.yMax - MargemAjusteSensibilidade.y * e - lado;
        AjusteMenosPx = new Rect(x0, y0, lado, lado);
        AjusteNumeroPx = new Rect(x0 + lado, y0, num, lado);
        AjusteMaisPx = new Rect(x0 + lado + num, y0, lado, lado);
    }

    /// <summary>
    /// Toque comecou em cima do ajuste? Se sim, aplica &lt; ou &gt; e devolve true (o toque e "consumido").
    /// O numero do meio tambem consome o toque (nao faz nada), para nao virar clique no mundo.
    /// </summary>
    static bool TocouAjusteSensibilidade(Vector2 p)
    {
        if (!AjusteSensibilidadeNaTela) return false;
        float folga = 14f * Escala; // um pouco maior que o desenho, mais facil de acertar com o dedo
        Rect tudo = Rect.MinMaxRect(AjusteMenosPx.xMin - folga, AjusteMenosPx.yMin - folga, AjusteMaisPx.xMax + folga, AjusteMaisPx.yMax + folga);
        if (!tudo.Contains(p)) return false;
        if (p.x <= AjusteMenosPx.xMax)
        {
            MudarSensibilidade(-1);
            UltimoToqueMenos = Time.unscaledTime;
        }
        else if (p.x >= AjusteMaisPx.xMin)
        {
            MudarSensibilidade(+1);
            UltimoToqueMais = Time.unscaledTime;
        }
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetarSensibilidade()
    {
        sensibilidade = null;
        MostrarAjusteSensibilidade = MostrarAjusteSensibilidadePadrao;
        UltimoToqueMenos = -10f;
        UltimoToqueMais = -10f;
    }
}
