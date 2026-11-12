using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Texto controlado pelo site (admin emuseu). Key = nome do GameObject (ou keyOverride, se preenchido).
// - Fica no objeto do texto (TMP_Text / UI Text / TextMesh) ou num pai: acha o texto nele mesmo ou nos filhos.
// - PT: setting.text da key (pacote ptbr). EN: textEn da key (eng, depois ptbr) -> text do pacote eng.
// - Sem payload, sem a key ou texto vazio no idioma atual: deixa o texto local (TextoIdioma PT/EN) como esta.
// - Roda depois do TextoIdioma (DefaultExecutionOrder) e reaplica no fim do frame em toda troca de idioma,
//   entao o TextoIdioma escreve o local primeiro e este script escreve o remoto por cima.
// - Varios objetos com o mesmo nome mostram o mesmo texto (no editor avisa uma vez por key repetida).
// - Nao mexe no TextoIdioma nem em cena: so adicionar o componente.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class RemoteTexto : MonoBehaviour
{
    [Tooltip("Opcional. Se preenchido, vale no lugar do nome do objeto como key do site.")]
    public string keyOverride = "";

    public bool showLogs = false;

    Component alvo;            // TMP_Text, UI.Text ou TextMesh
    TextoIdioma textoIdioma;   // TextoIdioma do objeto do texto (pode ser null)
    string localPt;            // texto local original (PT)
    bool capturou;
    bool aplicouRemoto;        // o texto na tela hoje e o remoto?
    bool pendente;

    /// <summary>Key usada no site: keyOverride (se preenchido) ou o nome do objeto, sem espacos nas pontas.</summary>
    public string Key
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(keyOverride)) return keyOverride.Trim();
            return gameObject.name.Trim();
        }
    }

    /// <summary>Componente de texto encontrado (nele mesmo ou nos filhos). Null se nao achou.</summary>
    public Component TextoAlvo { get { Achar(); return alvo; } }

    /// <summary>Texto local (PT) como esta no projeto, antes de qualquer troca. Usado pelo exportador.</summary>
    public string TextoLocalPt { get { Achar(); return alvo != null ? Ler(alvo) : ""; } }

    /// <summary>EN local (TextoIdioma.en), ou vazio.</summary>
    public string TextoLocalEn
    {
        get
        {
            Achar();
            return textoIdioma != null && textoIdioma.en != null ? textoIdioma.en : "";
        }
    }

    void Awake()
    {
        Capturar();
    }

    void OnEnable()
    {
        Capturar();
        Registrar();
        EmuseuSettingsCatalog.Changed -= AoMudarCatalogo;
        EmuseuSettingsCatalog.Changed += AoMudarCatalogo;
        EmuseuBridge.PlayConfigApplied -= AoReceberPlayConfig;
        EmuseuBridge.PlayConfigApplied += AoReceberPlayConfig;
        Idioma.OnMudou -= AoMudarIdioma;
        Idioma.OnMudou += AoMudarIdioma;
        Aplicar();
        pendente = true; // garante escrever depois do TextoIdioma.OnEnable deste frame
    }

    void Start()
    {
        Aplicar();
    }

    void OnDisable()
    {
        EmuseuSettingsCatalog.Changed -= AoMudarCatalogo;
        EmuseuBridge.PlayConfigApplied -= AoReceberPlayConfig;
        Idioma.OnMudou -= AoMudarIdioma;
        Desregistrar();
    }

    void LateUpdate()
    {
        if (!pendente) return;
        pendente = false;
        Aplicar();
    }

    void AoMudarCatalogo(string k)
    {
        if (k != null && k != Key) return;
        Aplicar();
        pendente = true;
    }

    void AoReceberPlayConfig(EmuseuPayload payload)
    {
        // O idioma pode ter mudado junto com o payload: reaplica tambem no fim do frame.
        Aplicar();
        pendente = true;
    }

    void AoMudarIdioma(bool ingles)
    {
        // O TextoIdioma pode ser chamado depois de nos (ordem de inscricao): escreve agora e de novo no LateUpdate.
        Aplicar();
        pendente = true;
    }

    /// <summary>Reaplica o texto (remoto se houver; senao o local).</summary>
    public void Aplicar()
    {
        Capturar();
        if (alvo == null) return;

        bool ingles = Idioma.Ingles;
        string remoto;
        if (Resolver(Key, ingles, out remoto))
        {
            if (Ler(alvo) != remoto) Escrever(alvo, remoto);
            aplicouRemoto = true;
            Log((ingles ? "EN" : "PT") + " remoto aplicado");
            return;
        }

        // Sem remoto: se antes mostramos o remoto, volta o local (mesma regra do TextoIdioma). Senao nao toca.
        if (aplicouRemoto)
        {
            Escrever(alvo, TextoLocal(ingles));
            aplicouRemoto = false;
            Log((ingles ? "EN" : "PT") + " sem texto remoto, voltou o local");
        }
    }

    string TextoLocal(bool ingles)
    {
        string en = textoIdioma != null ? textoIdioma.en : null;
        return ingles && !string.IsNullOrEmpty(en) ? en : localPt;
    }

    void Capturar()
    {
        if (capturou) return;
        Achar();
        if (alvo == null)
        {
            if (Application.isPlaying)
                Debug.LogWarning("[RemoteTexto] '" + Key + "': nenhum TMP_Text / Text / TextMesh neste objeto nem nos filhos.", this);
            return;
        }
        // O PT local e o texto do projeto. Se o idioma ja for EN e o TextoIdioma ja escreveu o EN,
        // o PT original nao esta mais no componente: nesse caso so sabemos o EN (fallback usa o que tiver).
        string atual = Ler(alvo);
        bool atualEhEn = Idioma.Ingles && textoIdioma != null && !string.IsNullOrEmpty(textoIdioma.en) && atual == textoIdioma.en;
        localPt = atualEhEn ? "" : atual;
        capturou = true;
    }

    void Achar()
    {
        if (alvo != null) return;
        alvo = AcharTexto(transform);
        textoIdioma = alvo != null ? alvo.GetComponent<TextoIdioma>() : null;
    }

    // Prefere o proprio objeto; depois o primeiro filho (inclusive inativo) com texto.
    static Component AcharTexto(Transform t)
    {
        Component c = t.GetComponent<TMPro.TMP_Text>();
        if (c != null) return c;
        c = t.GetComponent<UnityEngine.UI.Text>();
        if (c != null) return c;
        c = t.GetComponent<TextMesh>();
        if (c != null) return c;
        c = t.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (c != null) return c;
        c = t.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (c != null) return c;
        return t.GetComponentInChildren<TextMesh>(true);
    }

    static string Ler(Component c)
    {
        var tmp = c as TMPro.TMP_Text; if (tmp != null) return tmp.text;
        var ui = c as UnityEngine.UI.Text; if (ui != null) return ui.text;
        var tm = c as TextMesh; if (tm != null) return tm.text;
        return "";
    }

    static void Escrever(Component c, string s)
    {
        var tmp = c as TMPro.TMP_Text; if (tmp != null) { tmp.text = s; return; }
        var ui = c as UnityEngine.UI.Text; if (ui != null) { ui.text = s; return; }
        var tm = c as TextMesh; if (tm != null) tm.text = s;
    }

    void Log(string msg)
    {
        if (showLogs) Debug.Log("[RemoteTexto] " + Key + ": " + msg, this);
    }

    // ---------- Resolucao do texto (mesma ideia do RemoteAudioCatalog) ----------

    /// <summary>
    /// Texto remoto da key no idioma pedido. false = sem payload, sem a key ou texto vazio (manter o local).
    /// PT: text (pacote ptbr). EN: textEn (eng, depois ptbr) -> text do pacote eng.
    /// </summary>
    public static bool Resolver(string key, bool ingles, out string texto)
    {
        texto = null;
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (EmuseuBridge.PlayConfig == null && EmuseuSettingsCatalog.Count == 0) return false;
        key = key.Trim();
        EmuseuSetting s;

        if (!ingles)
        {
            if (TryGetPacote(false, key, out s) && Valido(TextoPt(s), out texto)) return true;
            return false;
        }

        bool temPacotes = TemPacotes();
        if (TryGetPacote(true, key, out s) && Valido(s.textEn, out texto)) return true;
        EmuseuSetting sPt;
        if (TryGetPacote(false, key, out sPt) && Valido(sPt.textEn, out texto)) return true;
        // text do pacote eng (so se existem pacotes separados; senao o "text" do catalogo e o PT).
        if (temPacotes && s != null && Valido(TextoPt(s), out texto)) return true;
        return false;
    }

    static string TextoPt(EmuseuSetting s)
    {
        if (s == null) return null;
        if (!string.IsNullOrEmpty(s.text)) return s.text;
        // Alguns payloads mandam o texto em "value".
        if (s.value != null && s.value.Type == JTokenType.String) return (string)s.value;
        return null;
    }

    static bool Valido(string candidato, out string texto)
    {
        if (!string.IsNullOrWhiteSpace(candidato))
        {
            texto = candidato.Replace("\r\n", "\n");
            return true;
        }
        texto = null;
        return false;
    }

    static bool TemPacotes()
    {
        EmuseuPayload p = EmuseuBridge.PlayConfig;
        EmuseuCampaign c = p != null ? p.campaign : null;
        return c != null && (c.ptbr != null || c.eng != null);
    }

    sealed class MapaPacote
    {
        public EmuseuLanguage fonte;
        public List<EmuseuGame> jogos;
        public int total = -1;
        public readonly Dictionary<string, EmuseuSetting> porChave = new Dictionary<string, EmuseuSetting>(StringComparer.Ordinal);
    }

    static readonly MapaPacote mapaPt = new MapaPacote();
    static readonly MapaPacote mapaEn = new MapaPacote();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar()
    {
        LimparMapa(mapaPt);
        LimparMapa(mapaEn);
        registro.Clear();
        avisadas.Clear();
    }

    static void LimparMapa(MapaPacote m)
    {
        m.fonte = null;
        m.jogos = null;
        m.total = -1;
        m.porChave.Clear();
    }

    // Idioma ativo: catalogo vivo (inclui edicoes recentes) e depois o pacote; outro idioma: pacote do PlayConfig.
    static bool TryGetPacote(bool eng, string key, out EmuseuSetting setting)
    {
        setting = null;
        EmuseuPayload p = EmuseuBridge.PlayConfig;
        EmuseuCampaign c = p != null ? p.campaign : null;
        if (c == null || (c.ptbr == null && c.eng == null))
            return EmuseuSettingsCatalog.TryGet(key, out setting) && setting != null;

        bool ativoEng = string.Equals(p.language, "eng", StringComparison.OrdinalIgnoreCase);
        if (eng == ativoEng && EmuseuSettingsCatalog.TryGet(key, out setting) && setting != null) return true;

        EmuseuLanguage pacote = eng ? c.eng : c.ptbr;
        if (pacote == null) return false;
        MapaPacote mapa = Mapa(pacote, eng ? mapaEn : mapaPt);
        return mapa.porChave.TryGetValue(key, out setting) && setting != null;
    }

    static MapaPacote Mapa(EmuseuLanguage pacote, MapaPacote m)
    {
        int total = 0;
        if (pacote.games != null)
            foreach (EmuseuGame g in pacote.games)
                if (g != null && g.settings != null) total += g.settings.Count;

        if (ReferenceEquals(m.fonte, pacote) && ReferenceEquals(m.jogos, pacote.games) && m.total == total) return m;

        m.porChave.Clear();
        m.fonte = pacote;
        m.jogos = pacote.games;
        m.total = total;
        if (pacote.games == null) return m;
        foreach (EmuseuGame g in pacote.games)
        {
            if (g == null || g.settings == null) continue;
            foreach (EmuseuSetting s in g.settings)
            {
                if (s == null || string.IsNullOrEmpty(s.key)) continue;
                EmuseuSetting antigo;
                // Chave repetida: mantem a que tem texto; senao a ultima.
                if (m.porChave.TryGetValue(s.key, out antigo) && !string.IsNullOrEmpty(TextoPt(antigo)) && string.IsNullOrEmpty(TextoPt(s))) continue;
                m.porChave[s.key] = s;
            }
        }
        return m;
    }

    // ---------- Aviso de key repetida (so no editor, uma vez por key) ----------

    static readonly Dictionary<string, RemoteTexto> registro = new Dictionary<string, RemoteTexto>(StringComparer.Ordinal);
    static readonly HashSet<string> avisadas = new HashSet<string>(StringComparer.Ordinal);
    string keyRegistrada;

    void Registrar()
    {
#if UNITY_EDITOR
        string k = Key;
        if (string.IsNullOrEmpty(k)) return;
        keyRegistrada = k;
        RemoteTexto outro;
        if (registro.TryGetValue(k, out outro) && outro != null && outro != this)
        {
            if (avisadas.Add(k))
                Debug.LogWarning("[RemoteTexto] key repetida '" + k + "': varios objetos vao mostrar o mesmo texto do site. " +
                                 "Se precisam de textos diferentes, preencha keyOverride.", this);
            return;
        }
        registro[k] = this;
#endif
    }

    void Desregistrar()
    {
#if UNITY_EDITOR
        RemoteTexto atual;
        if (!string.IsNullOrEmpty(keyRegistrada) && registro.TryGetValue(keyRegistrada, out atual) && atual == this)
            registro.Remove(keyRegistrada);
        keyRegistrada = null;
#endif
    }
}