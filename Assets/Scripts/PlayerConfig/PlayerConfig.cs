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
    public static readonly string[] Estados = { "BH", "DF", "CE", "RS", "PE", "RJ", "BA", "SP" };

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

    // Ex: "DF_E2" -> marca DF etapa 2 e adiciona a carta
    public void RegistrarPuzzle(string etapaId, string cartaId)
    {
        if (!string.IsNullOrEmpty(etapaId))
        {
            var p = etapaId.Split('_');
            int n;
            if (p.Length == 2 && p[1].Length > 1 && int.TryParse(p[1].Substring(1), out n)) MarcarEtapa(p[0], n);
            else Debug.LogWarning("[PlayerConfig] etapaId invalido: " + etapaId);
        }
        AdicionarCarta(cartaId);
        Debug.Log("[PlayerConfig] concluido " + etapaId + (string.IsNullOrEmpty(cartaId) ? "" : " carta " + cartaId));
    }

    // Sigla pelo nome da cena ativa (CenaBrasilia -> DF)
    public static string SiglaDaCena()
    {
        string c = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower();
        if (c.Contains("belohorizonte")) return "BH";
        if (c.Contains("brasilia")) return "DF";
        if (c.Contains("fortaleza")) return "CE";
        if (c.Contains("portoalegre") || c.Contains("porto_alegre")) return "RS";
        if (c.Contains("recife")) return "PE";
        if (c.Contains("riodejaneiro") || c.StartsWith("rj_")) return "RJ";
        if (c.Contains("salvador")) return "BA";
        if (c.Contains("saopaulo")) return "SP";
        return null;
    }
    public static string EtapaDaCena(int ordem) { var s = SiglaDaCena(); return s == null ? null : s + "_E" + ordem; }

    // ---- Payload (adaptar quando soubermos o formato real) ----
    public string ParaJson() => JsonUtility.ToJson(this, true);
    public void AplicarJson(string json) { JsonUtility.FromJsonOverwrite(json, this); Garantir(); }

    void OnValidate() => Garantir();
}