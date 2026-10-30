using System;
using System.Collections.Generic;
using UnityEngine;

public enum IdiomaJogador { PT, EN }

[Serializable]
public class EstadoProgresso
{
    public string estado;                       // ex: "RJ"
    public bool[] etapas = new bool[5];         // E1..E5 concluidas
    public int Concluidas() { int n = 0; foreach (var b in etapas) if (b) n++; return n; }
}

// Config do jogador. Hoje e editada a mao (Tools > Player Config).
// Quando o payload do site chegar, basta chamar AplicarJson(...) ou preencher os campos.
[CreateAssetMenu(fileName = "PlayerConfig", menuName = "E_Virtual/Player Config")]
public class PlayerConfig : ScriptableObject
{
    public static readonly string[] Estados = { "RJ", "SP", "BH", "SSA", "DF", "REC", "FOR", "POA" };

    public IdiomaJogador idioma = IdiomaJogador.PT;
    public List<string> cartas = new List<string>();     // ids que o jogador ja tem, ex: "F11"
    public List<EstadoProgresso> estados = new List<EstadoProgresso>();

    static PlayerConfig _inst;
    public static PlayerConfig Instancia
    {
        get { if (_inst == null) _inst = Resources.Load<PlayerConfig>("PlayerConfig"); return _inst; }
    }

    public bool Ingles => idioma == IdiomaJogador.EN;
    public bool TemCarta(string id) => cartas.Contains(id);
    public void AdicionarCarta(string id) { if (!string.IsNullOrEmpty(id) && !cartas.Contains(id)) cartas.Add(id); }

    public EstadoProgresso Estado(string sigla)
    {
        Garantir();
        return estados.Find(e => e.estado == sigla);
    }
    public bool EtapaConcluida(string sigla, int etapa) { var e = Estado(sigla); return e != null && etapa >= 1 && etapa <= 5 && e.etapas[etapa - 1]; }
    public void MarcarEtapa(string sigla, int etapa, bool v = true) { var e = Estado(sigla); if (e != null && etapa >= 1 && etapa <= 5) e.etapas[etapa - 1] = v; }

    public void Garantir()
    {
        foreach (var s in Estados)
            if (!estados.Exists(e => e.estado == s)) estados.Add(new EstadoProgresso { estado = s });
        foreach (var e in estados) if (e.etapas == null || e.etapas.Length != 5) Array.Resize(ref e.etapas, 5);
    }

    // ---- Payload (adaptar quando soubermos o formato real) ----
    public string ParaJson() => JsonUtility.ToJson(this, true);
    public void AplicarJson(string json) { JsonUtility.FromJsonOverwrite(json, this); Garantir(); }

    void OnValidate() => Garantir();
}