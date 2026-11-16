// MobileTouchControls.Rotulos.cs
// Letras no centro dos controles de toque: "M" no joystick esquerdo (andar), "C" no direito (camera), "I" no botao Interagir.
// - Nos joysticks a letra fica no knob (a bolinha que mexe), entao acompanha o dedo.
// - No botao Interagir a letra substitui o pontinho do centro.
// - Trocar as letras: mude os valores abaixo (pode ser mais de uma letra, ex.: "MOVER"). Tambem da para trocar por codigo
//   durante o jogo (MobileTouchControls.RotuloCamera = "CAM"); o texto atualiza sozinho. Texto vazio esconde a letra.
// - As letras nao bloqueiam toque (raycastTarget = false).
using UnityEngine;
using UnityEngine.UI;

public partial class MobileTouchControls
{
    public static string RotuloMover = "M";
    public static string RotuloCamera = "C";
    public static string RotuloInteragir = "I";
    /// <summary>Cor das letras (branco semitransparente).</summary>
    public static Color CorRotulo = new Color(1f, 1f, 1f, 0.7f);
    /// <summary>Quanto da bolinha a letra ocupa (0..1). Mais letras ficam menores sozinhas (best fit).</summary>
    public static float TamanhoRotulo = 0.6f;

    Text txtRotuloMover, txtRotuloCamera, txtRotuloInteragir;

    void ConstruirRotulos()
    {
        txtRotuloMover = CriarRotulo("Rotulo_Mover", knobE, RotuloMover);
        txtRotuloCamera = CriarRotulo("Rotulo_Camera", knobD, RotuloCamera);
        txtRotuloInteragir = CriarRotulo("Rotulo_Interagir", botao, RotuloInteragir);
        AtualizarRotulos();
    }

    static Text CriarRotulo(string nome, RectTransform pai, string texto)
    {
        if (pai == null) return null;
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var rt = (RectTransform)go.transform;
        Esticar(rt, Mathf.Clamp01(TamanhoRotulo));
        var t = go.AddComponent<Text>();
        Font f = FonteUI();
        if (f != null) t.font = f;
        t.text = texto ?? "";
        t.alignment = TextAnchor.MiddleCenter;
        t.color = CorRotulo;
        t.fontStyle = FontStyle.Bold;
        t.resizeTextForBestFit = true; // tamanho acompanha o tamanho da bolinha
        t.resizeTextMinSize = 8;
        t.resizeTextMaxSize = 300;
        t.fontSize = 300;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    void AtualizarRotulos()
    {
        AplicarRotulo(txtRotuloMover, RotuloMover);
        AplicarRotulo(txtRotuloCamera, RotuloCamera);
        AplicarRotulo(txtRotuloInteragir, RotuloInteragir);
        // o pontinho do botao Interagir so aparece se o rotulo estiver vazio
        if (botaoIcone != null)
        {
            bool mostrarPonto = string.IsNullOrEmpty(RotuloInteragir);
            if (botaoIcone.gameObject.activeSelf != mostrarPonto) botaoIcone.gameObject.SetActive(mostrarPonto);
        }
    }

    static void AplicarRotulo(Text t, string texto)
    {
        if (t == null) return;
        string v = texto ?? "";
        if (t.text != v) t.text = v;
        if (t.color != CorRotulo) t.color = CorRotulo;
        bool ativo = v.Length > 0;
        if (t.enabled != ativo) t.enabled = ativo;
    }
}
