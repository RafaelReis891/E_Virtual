// MobileVirtualJoystick.cs
// Joystick virtual (so a logica, sem UI). Tudo em pixels de tela.
// Usado pelo MobileInput (leitura dos toques) e desenhado pelo MobileTouchControls.
using UnityEngine;

public class MobileVirtualJoystick
{
    public const int SemDedo = int.MinValue;

    /// <summary>Centro da base, em pixels de tela.</summary>
    public Vector2 Centro;
    /// <summary>Curso maximo do knob, em pixels.</summary>
    public float Raio = 100f;
    /// <summary>Raio da area que "pega" o toque, em pixels (maior que a base, pra facilitar).</summary>
    public float RaioAtivacao = 160f;
    /// <summary>Zona morta (0..1).</summary>
    public float ZonaMorta = 0.12f;

    public int Dedo { get; private set; } = SemDedo;
    public bool Ativo { get { return Dedo != SemDedo; } }
    /// <summary>Valor -1..1 em X e Y, ja descontada a zona morta.</summary>
    public Vector2 Valor { get; private set; }
    /// <summary>Deslocamento do knob em pixels (para o visual).</summary>
    public Vector2 OffsetKnob { get; private set; }

    public bool Contem(Vector2 p)
    {
        return (p - Centro).sqrMagnitude <= RaioAtivacao * RaioAtivacao;
    }

    public void Pegar(int dedo, Vector2 p)
    {
        Dedo = dedo;
        Mover(p);
    }

    public void Mover(Vector2 p)
    {
        if (Raio <= 0.0001f) { OffsetKnob = Vector2.zero; Valor = Vector2.zero; return; }
        Vector2 d = Vector2.ClampMagnitude(p - Centro, Raio);
        OffsetKnob = d;
        Vector2 v = d / Raio;
        float m = v.magnitude;
        if (m < ZonaMorta || m < 0.0001f) { Valor = Vector2.zero; return; }
        Valor = (v / m) * Mathf.Clamp01((m - ZonaMorta) / (1f - ZonaMorta));
    }

    public void Soltar()
    {
        Dedo = SemDedo;
        OffsetKnob = Vector2.zero;
        Valor = Vector2.zero;
    }
}
