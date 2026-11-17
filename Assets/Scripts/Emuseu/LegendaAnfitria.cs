// LegendaAnfitria.cs
// Legendas da Anfitria (PC e celular, mesma build WebGL).
// - Cria-se sozinha (RuntimeInitializeOnLoadMethod): um objeto "LegendaAnfitria" (DontDestroyOnLoad) com Canvas proprio.
//   Nao precisa de cena, prefab nem Inspector.
// - A cada 0,1 s procura uma fala tocando: RemoteVoiceClip (key do site) com o AudioSource do mesmo objeto isPlaying.
// - Texto: RemoteTexto.Resolver(key, ingles) no idioma do audio (EN: key e depois keyEn). Sem texto EN usa o PT; sem nada, nao mostra.
// - Limpeza: <break time="Ns" /> e [pausa] viram pontos de corte; tags como [excited] e qualquer <...> somem.
//   Trechos longos (> ~90 caracteres) sao quebrados em frase / virgula / palavra.
// - Tempo: cada trecho dura proporcional ao numero de letras, dentro do clip.length (pausas do <break> somadas ao trecho anterior),
//   seguindo audioSource.time. Some quando o audio para.
// - Opcoes: LegendaAnfitria.Ativo (PlayerPrefs "EVirtual.Legendas.Ativo", padrao ligado) e LegendaAnfitria.TamanhoFonte
//   (PlayerPrefs "EVirtual.Legendas.Tamanho"). O site pode mandar:
//     SendMessage("LegendaAnfitria", "SetAtivo", "1" | "0")   e   SendMessage("LegendaAnfitria", "SetTamanho", "44")
// - Celular: fica acima da area dos controles M / C / I; some com a ajuda aberta (MobileInput.AjudaAberta). Respeita a safe area.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public sealed class LegendaAnfitria : MonoBehaviour
{
    // ------------------------------------------------------------------ opcoes
    public const string PrefAtivo = "EVirtual.Legendas.Ativo";
    public const string PrefTamanho = "EVirtual.Legendas.Tamanho";
    public const float TamanhoPadrao = 40f;

    /// <summary>Liga/desliga as legendas (lembra no PlayerPrefs).</summary>
    public static bool Ativo
    {
        get { CarregarPrefs(); return ativo; }
        set
        {
            CarregarPrefs();
            if (ativo == value) return;
            ativo = value;
            PlayerPrefs.SetInt(PrefAtivo, value ? 1 : 0);
            PlayerPrefs.Save();
            if (Instancia != null) Instancia.Esconder();
        }
    }

    /// <summary>Tamanho da letra em unidades do canvas 1920x1080 (24 a 80). Lembra no PlayerPrefs.</summary>
    public static float TamanhoFonte
    {
        get { CarregarPrefs(); return tamanho; }
        set
        {
            CarregarPrefs();
            float v = Mathf.Clamp(value, 24f, 80f);
            if (Mathf.Approximately(v, tamanho)) return;
            tamanho = v;
            PlayerPrefs.SetFloat(PrefTamanho, v);
            PlayerPrefs.Save();
            if (Instancia != null) Instancia.ultimoTrecho = null; // remonta
        }
    }

    public static void Alternar() { Ativo = !Ativo; }

    /// <summary>Distancia da borda de baixo (unidades do canvas). No celular fica acima dos controles M / C / I.</summary>
    public static float MargemInferiorPC = 70f;
    public static float MargemInferiorCelular = 600f;
    /// <summary>Largura maxima da caixa (unidades do canvas).</summary>
    public static float LarguraMaxima = 1400f;
    /// <summary>Letras por trecho antes de quebrar.</summary>
    public static int MaxCaracteres = 90;
    public static Color CorFundo = new Color(0f, 0f, 0f, 0.62f);
    public static Color CorTexto = new Color(1f, 1f, 1f, 0.97f);
    public static bool LogNoConsole = false;

    public static LegendaAnfitria Instancia { get; private set; }

    static bool prefsCarregadas, ativo = true;
    static float tamanho = TamanhoPadrao;

    static void CarregarPrefs()
    {
        if (prefsCarregadas) return;
        prefsCarregadas = true;
        ativo = PlayerPrefs.GetInt(PrefAtivo, 1) != 0;
        tamanho = Mathf.Clamp(PlayerPrefs.GetFloat(PrefTamanho, TamanhoPadrao), 24f, 80f);
    }

    // ------------------------------------------------------------------ criacao
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetarDominio()
    {
        Instancia = null;
        prefsCarregadas = false;
        cacheTexto.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Criar()
    {
        if (Instancia != null) return;
        var go = new GameObject("LegendaAnfitria");
        DontDestroyOnLoad(go);
        go.AddComponent<LegendaAnfitria>();
    }

    // Mensagens do site (SendMessage). Strings para funcionar com react-unity-webgl.
    [UnityEngine.Scripting.Preserve]
    public void SetAtivo(string v)
    {
        v = (v ?? "").Trim().ToLowerInvariant();
        Ativo = !(v == "0" || v == "false" || v == "off" || v == "nao" || v == "não");
    }

    [UnityEngine.Scripting.Preserve]
    public void SetTamanho(string v)
    {
        float f;
        if (float.TryParse((v ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out f))
            TamanhoFonte = f;
    }

    // ------------------------------------------------------------------ estado
    struct Voz
    {
        public RemoteVoiceClip remoto;
        public CharacterAppearDissolve personagem;
        public AudioSource fonte;
    }

    sealed class Trecho
    {
        public string texto;
        public float inicio, fim;
    }

    readonly List<Voz> vozes = new List<Voz>();
    float proximaBusca, proximaChecagem;

    // fala atual
    AudioSource fonteAtual;
    AudioClip clipAtual;
    string textoFonte;          // texto bruto que gerou os trechos
    bool inglesAtual;
    List<Trecho> trechos;
    float relogio, ultimoTempoAudio;
    string ultimoTrecho;

    // UI
    Canvas canvas;
    RectTransform raiz, caixa;
    Image fundo;
    TextMeshProUGUI txt;
    Rect safeAplicada;
    Vector2 telaAplicada;

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        CarregarPrefs();
        ConstruirUI();
        SceneManager.sceneLoaded += AoCarregarCena;
        Buscar();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        if (Instancia == this) Instancia = null;
    }

    void AoCarregarCena(Scene s, LoadSceneMode m)
    {
        vozes.Clear();
        Esconder();
        proximaBusca = 0f; // busca no proximo Update (objetos ja acordaram)
    }

    void Buscar()
    {
        vozes.Clear();
        foreach (RemoteVoiceClip r in FindObjectsByType<RemoteVoiceClip>(FindObjectsInactive.Include))
        {
            if (r == null) continue;
            vozes.Add(new Voz
            {
                remoto = r,
                personagem = r.GetComponent<CharacterAppearDissolve>(),
                fonte = r.GetComponent<AudioSource>()
            });
        }
        proximaBusca = Time.unscaledTime + 2f; // a Anfitria pode aparecer depois (instanciada / ativada)
    }

    void Update()
    {
        float agora = Time.unscaledTime;
        if (agora >= proximaBusca) Buscar();

        // relogio proprio (caso audioSource.time nao ande no navegador)
        if (fonteAtual != null && fonteAtual.isPlaying) relogio += Time.unscaledDeltaTime * Mathf.Abs(fonteAtual.pitch);

        AtualizarSafeArea();

        if (agora < proximaChecagem) return;
        proximaChecagem = agora + 0.1f;
        Checar();
    }

    void Checar()
    {
        if (!Ativo || MobileInput.AjudaAberta) { Esconder(); return; }

        Voz voz;
        if (!AcharVozTocando(out voz)) { Esconder(); return; }

        AudioSource src = voz.fonte;
        AudioClip clip = src.clip;
        bool ingles = IdiomaDoAudio(voz, clip);
        string bruto = TextoDaKey(voz.remoto, ingles);
        if (string.IsNullOrWhiteSpace(bruto)) { Esconder(); return; }

        float t = src.time;
        bool novaFala = src != fonteAtual || clip != clipAtual;
        if (!novaFala && t + 0.25f < ultimoTempoAudio && t < 0.5f) novaFala = true; // tocou de novo do inicio
        if (novaFala || bruto != textoFonte || ingles != inglesAtual)
        {
            if (novaFala) relogio = t;
            fonteAtual = src;
            clipAtual = clip;
            textoFonte = bruto;
            inglesAtual = ingles;
            trechos = MontarTrechos(bruto, clip != null ? clip.length : 0f);
            if (LogNoConsole) Debug.Log("[LegendaAnfitria] " + voz.remoto.key + " (" + (ingles ? "EN" : "PT") + "): " + trechos.Count + " trechos");
        }
        ultimoTempoAudio = t;

        if (t < 0.001f && relogio > 0.3f) t = relogio; // navegador sem time: usa o relogio proprio
        else relogio = t;

        Mostrar(TrechoNoTempo(t));
    }

    bool AcharVozTocando(out Voz escolhida)
    {
        escolhida = default(Voz);
        bool achou = false;
        float menorTempo = float.MaxValue;
        for (int i = vozes.Count - 1; i >= 0; i--)
        {
            Voz v = vozes[i];
            if (v.remoto == null) { vozes.RemoveAt(i); continue; }
            if (v.fonte == null) { v.fonte = v.remoto.GetComponent<AudioSource>(); vozes[i] = v; }
            if (v.fonte == null || !v.fonte.isPlaying || v.fonte.clip == null) continue;
            if (!v.remoto.isActiveAndEnabled && !v.fonte.isActiveAndEnabled) continue;
            // mais de uma tocando: a que comecou por ultimo
            float tt = v.fonte.time;
            if (!achou || tt < menorTempo) { escolhida = v; menorTempo = tt; achou = true; }
        }
        return achou;
    }

    static bool IdiomaDoAudio(Voz v, AudioClip clip)
    {
        // Se o clipe e um OGG do build, sabemos o idioma. Senao (audio remoto) segue o idioma do jogo.
        if (v.personagem != null && clip != null)
        {
            if (clip == v.personagem.voiceClipEng) return true;
            if (clip == v.personagem.voiceClip) return false;
        }
        return Idioma.Ingles;
    }

    static string TextoDaKey(RemoteVoiceClip r, bool ingles)
    {
        if (r == null || string.IsNullOrWhiteSpace(r.key)) return null;
        string s;
        if (ingles)
        {
            if (RemoteTexto.Resolver(r.key, true, out s)) return s;
            if (!string.IsNullOrWhiteSpace(r.keyEn) && RemoteTexto.Resolver(r.keyEn, true, out s)) return s;
            if (!string.IsNullOrWhiteSpace(r.keyEn) && RemoteTexto.Resolver(r.keyEn, false, out s)) return s;
        }
        if (RemoteTexto.Resolver(r.key, false, out s)) return s; // fallback PT
        return null;
    }

    string TrechoNoTempo(float t)
    {
        if (trechos == null || trechos.Count == 0) return null;
        for (int i = 0; i < trechos.Count; i++)
            if (t < trechos[i].fim) return trechos[i].texto;
        return trechos[trechos.Count - 1].texto; // audio um pouco mais longo: fica no ultimo ate parar
    }

    // ------------------------------------------------------------------ texto -> trechos
    static readonly Regex reCorte = new Regex("<\\s*break\\b[^>]*?time\\s*=\\s*[\"']?\\s*([0-9]+(?:[.,][0-9]+)?)\\s*(ms|s)?\\s*[\"']?[^>]*>|\\[\\s*pausa\\s*\\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex reTag = new Regex("\\[[A-Za-zÀ-ÿ\\s\\-]{2,30}\\]", RegexOptions.CultureInvariant);
    static readonly Regex reAngulo = new Regex("<[^>]*>", RegexOptions.CultureInvariant);
    static readonly Regex reEspacos = new Regex("\\s+", RegexOptions.CultureInvariant);
    static readonly Regex reEspacoPontuacao = new Regex("\\s+([,.;:!?…])", RegexOptions.CultureInvariant);
    static readonly Regex rePontuacaoInicio = new Regex("^[,.;:!?…\"”')\\]]+", RegexOptions.CultureInvariant);

    static readonly Dictionary<string, List<Trecho>> cacheTexto = new Dictionary<string, List<Trecho>>(StringComparer.Ordinal);

    sealed class Peca { public string texto; public float pausaDepois; }

    // Divide o texto do site em trechos com tempo.
    static List<Trecho> MontarTrechos(string bruto, float duracao)
    {
        string chave = duracao.ToString("0.000", CultureInfo.InvariantCulture) + "|" + bruto;
        List<Trecho> pronto;
        if (cacheTexto.TryGetValue(chave, out pronto)) return pronto;

        // 1) corta nos <break> / [pausa]
        var pecas = new List<Peca>();
        int pos = 0;
        foreach (Match m in reCorte.Matches(bruto))
        {
            pecas.Add(new Peca { texto = bruto.Substring(pos, m.Index - pos) });
            float seg = 0f;
            if (m.Groups[1].Success)
            {
                float.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out seg);
                if (m.Groups[2].Success && m.Groups[2].Value.ToLowerInvariant() == "ms") seg /= 1000f;
            }
            pecas[pecas.Count - 1].pausaDepois = Mathf.Max(0f, seg);
            pos = m.Index + m.Length;
        }
        pecas.Add(new Peca { texto = bruto.Substring(pos) });

        // 2) limpa cada peca
        foreach (Peca p in pecas) p.texto = Limpar(p.texto);

        // 3) pontuacao que comecou uma peca volta para a anterior
        for (int i = 1; i < pecas.Count; i++)
        {
            Match m = rePontuacaoInicio.Match(pecas[i].texto);
            if (!m.Success) continue;
            int ant = i - 1;
            while (ant >= 0 && pecas[ant].texto.Length == 0) ant--;
            if (ant < 0) continue;
            pecas[ant].texto = pecas[ant].texto + m.Value;
            pecas[i].texto = pecas[i].texto.Substring(m.Length).Trim();
        }

        // 4) quebra os longos e junta tudo
        var lista = new List<Trecho>();
        var pausas = new List<float>();
        float pausaPendente = 0f;
        foreach (Peca p in pecas)
        {
            if (p.texto.Length == 0)
            {
                // peca vazia (ex.: break no inicio): a pausa vai antes do proximo trecho
                if (lista.Count > 0) pausas[pausas.Count - 1] += p.pausaDepois; else pausaPendente += p.pausaDepois;
                continue;
            }
            List<string> partes = Quebrar(p.texto, Mathf.Max(30, MaxCaracteres));
            for (int k = 0; k < partes.Count; k++)
            {
                lista.Add(new Trecho { texto = partes[k] });
                pausas.Add(k == partes.Count - 1 ? p.pausaDepois : 0f);
            }
        }

        // 5) tempos proporcionais ao numero de letras
        if (lista.Count > 0)
        {
            float totalPausas = pausaPendente;
            foreach (float pz in pausas) totalPausas += pz;
            int totalLetras = 0;
            foreach (Trecho tr in lista) totalLetras += Mathf.Max(1, tr.texto.Length);

            float dur = duracao > 0.1f ? duracao : totalLetras / 14f; // sem clipe: ~14 letras/s
            float escalaPausa = 1f;
            if (totalPausas > dur * 0.6f) escalaPausa = totalPausas > 0f ? (dur * 0.6f) / totalPausas : 0f; // pausa absurda: limita
            float fala = Mathf.Max(0.1f, dur - totalPausas * escalaPausa);

            float t = pausaPendente * escalaPausa;
            for (int i = 0; i < lista.Count; i++)
            {
                lista[i].inicio = t;
                t += fala * Mathf.Max(1, lista[i].texto.Length) / totalLetras;
                t += pausas[i] * escalaPausa; // durante a pausa o trecho continua na tela
                lista[i].fim = t;
            }
            lista[lista.Count - 1].fim = Mathf.Max(lista[lista.Count - 1].fim, dur);
        }

        if (cacheTexto.Count > 200) cacheTexto.Clear();
        cacheTexto[chave] = lista;
        return lista;
    }

    /// <summary>Tira tags de voz ([excited], [laughs]...), qualquer &lt;...&gt; e espacos repetidos.</summary>
    public static string Limpar(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        s = reTag.Replace(s, " ");
        s = reAngulo.Replace(s, " ");
        s = reEspacos.Replace(s, " ");
        s = reEspacoPontuacao.Replace(s, "$1");
        return s.Trim();
    }

    static List<string> Quebrar(string s, int max)
    {
        var saida = new List<string>();
        if (s.Length <= max) { saida.Add(s); return saida; }

        // frases
        var frases = DividirApos(s, ".!?…");
        var blocos = new List<string>();
        foreach (string f in frases)
        {
            if (f.Length <= max) { blocos.Add(f); continue; }
            // frase longa: virgulas / ponto e virgula / dois pontos
            foreach (string v in Juntar(DividirApos(f, ",;:"), max))
            {
                if (v.Length <= max) { blocos.Add(v); continue; }
                blocos.AddRange(PorPalavras(v, max));
            }
        }
        saida.AddRange(Juntar(blocos, max));
        return saida;
    }

    // Divide depois dos caracteres dados quando seguidos de espaco. Mantem a pontuacao no pedaco.
    static List<string> DividirApos(string s, string chars)
    {
        var r = new List<string>();
        int ini = 0;
        for (int i = 0; i < s.Length - 1; i++)
        {
            if (chars.IndexOf(s[i]) < 0 || s[i + 1] != ' ') continue;
            string p = s.Substring(ini, i + 1 - ini).Trim();
            if (p.Length > 0) r.Add(p);
            ini = i + 1;
        }
        string resto = s.Substring(ini).Trim();
        if (resto.Length > 0) r.Add(resto);
        return r;
    }

    // Junta pedacos vizinhos enquanto couberem em max.
    static List<string> Juntar(List<string> partes, int max)
    {
        var r = new List<string>();
        var sb = new StringBuilder();
        foreach (string p in partes)
        {
            if (sb.Length > 0 && sb.Length + 1 + p.Length > max)
            {
                r.Add(sb.ToString());
                sb.Length = 0;
            }
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(p);
        }
        if (sb.Length > 0) r.Add(sb.ToString());
        return r;
    }

    static List<string> PorPalavras(string s, int max)
    {
        var r = new List<string>();
        var sb = new StringBuilder();
        foreach (string w in s.Split(' '))
        {
            if (w.Length == 0) continue;
            if (sb.Length > 0 && sb.Length + 1 + w.Length > max)
            {
                r.Add(sb.ToString());
                sb.Length = 0;
            }
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(w);
        }
        if (sb.Length > 0) r.Add(sb.ToString());
        return r;
    }

    // ------------------------------------------------------------------ UI
    void ConstruirUI()
    {
        var go = new GameObject("LegendaAnfitriaCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 940; // acima dos controles de toque (900), abaixo da ajuda (950) e do aviso de girar (1000)
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        // sem GraphicRaycaster: a legenda nunca bloqueia clique/toque

        var r = new GameObject("SafeArea", typeof(RectTransform));
        r.transform.SetParent(go.transform, false);
        raiz = (RectTransform)r.transform;
        raiz.anchorMin = Vector2.zero; raiz.anchorMax = Vector2.one;
        raiz.offsetMin = raiz.offsetMax = Vector2.zero;

        var c = new GameObject("Caixa", typeof(RectTransform));
        c.transform.SetParent(raiz, false);
        caixa = (RectTransform)c.transform;
        caixa.anchorMin = caixa.anchorMax = new Vector2(0.5f, 0f);
        caixa.pivot = new Vector2(0.5f, 0f);
        fundo = c.AddComponent<Image>();
        fundo.sprite = CriarSpriteArredondado(64, 18);
        fundo.type = Image.Type.Sliced;
        fundo.color = CorFundo;
        fundo.raycastTarget = false;

        var t = new GameObject("Texto", typeof(RectTransform));
        t.transform.SetParent(caixa, false);
        var rt = (RectTransform)t.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(28f, 14f); rt.offsetMax = new Vector2(-28f, -14f);
        txt = t.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = AcharFonte();
        if (f != null) txt.font = f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.textWrappingMode = TextWrappingModes.Normal;
        txt.richText = false; // texto do site e mostrado como esta
        txt.raycastTarget = false;
        txt.color = CorTexto;
        txt.outlineWidth = 0f;

        canvas.enabled = false;
    }

    void Mostrar(string trecho)
    {
        if (string.IsNullOrEmpty(trecho)) { Esconder(); return; }
        if (!canvas.enabled) canvas.enabled = true;

        bool celular = MobileInput.Ativo;
        float margem = celular ? MargemInferiorCelular : MargemInferiorPC;
        float larguraTela = raiz.rect.width > 1f ? raiz.rect.width : 1920f;
        float largMax = Mathf.Min(LarguraMaxima, larguraTela - 80f);
        if (caixa.anchoredPosition.y != margem) caixa.anchoredPosition = new Vector2(0f, margem);

        if (trecho == ultimoTrecho && Mathf.Approximately(txt.fontSize, TamanhoFonte)) return;
        ultimoTrecho = trecho;
        txt.fontSize = TamanhoFonte;
        txt.text = trecho;
        Vector2 pref = txt.GetPreferredValues(trecho, largMax - 56f, 0f);
        float w = Mathf.Min(largMax, Mathf.Ceil(pref.x) + 56f + 2f);
        float h = Mathf.Ceil(pref.y) + 28f;
        caixa.sizeDelta = new Vector2(w, h);
    }

    void Esconder()
    {
        if (canvas != null && canvas.enabled) canvas.enabled = false;
        ultimoTrecho = null;
        fonteAtual = null;
        clipAtual = null;
        textoFonte = null;
        trechos = null;
    }

    void AtualizarSafeArea()
    {
        if (raiz == null) return;
        Rect sa = Screen.safeArea;
        Vector2 tela = new Vector2(Screen.width, Screen.height);
        if (sa == safeAplicada && tela == telaAplicada) return;
        safeAplicada = sa;
        telaAplicada = tela;
        if (tela.x <= 0f || tela.y <= 0f) return;
        raiz.anchorMin = new Vector2(sa.xMin / tela.x, sa.yMin / tela.y);
        raiz.anchorMax = new Vector2(sa.xMax / tela.x, sa.yMax / tela.y);
        raiz.offsetMin = raiz.offsetMax = Vector2.zero;
        ultimoTrecho = null; // largura pode ter mudado
    }

    static TMP_FontAsset AcharFonte()
    {
        TMP_FontAsset melhor = null;
        int nota = 0;
        try
        {
            foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (f == null) continue;
                string n = f.name;
                int v = 0;
                if (n == "Barlow-Regular SDF") v = 4;
                else if (n.StartsWith("Barlow-Regular")) v = 3;
                else if (n.StartsWith("Barlow") && !n.Contains("Condensed") && !n.Contains("Italic")) v = 2;
                if (v > nota) { nota = v; melhor = f; }
            }
        }
        catch (Exception) { melhor = null; }
        return melhor; // null = fonte padrao do TextMeshPro
    }

    static Sprite CriarSpriteArredondado(int tam, int raio)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[tam * tam];
        for (int y = 0; y < tam; y++)
            for (int x = 0; x < tam; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, raio, tam - raio);
                float cy = Mathf.Clamp(y + 0.5f, raio, tam - raio);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(raio - d + 0.5f);
                px[y * tam + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(raio, raio, raio, raio));
    }
}
