using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

// Catalogo de AUDIOS remotos (vozes/musicas) vindos do site (payload play-config).
// - Key de tipo audio:  setting.file.url (MP3).
// - Key de tipo texto:  setting.audio (PT) / setting.audioEn (EN) = TTS, objeto {url,...} ou string.
// - PT: audio.url da key, senao file.url da key (pacote campaign.ptbr).
// - EN (nesta ordem): audioEn da key (pacote eng, depois ptbr) -> audio da key no pacote eng -> key EN (explicita ou key + "_en";
//   file.url / audioEn / audio) no pacote eng, depois no ptbr -> FALLBACK: o resultado do PT (audio.url / file.url da key, pacote ptbr).
//   O pacote eng do site costuma vir com todo "file" nulo, entao sem o fallback nada era achado em ingles.
// - Os dois pacotes (campaign.ptbr e campaign.eng) ficam sempre no EmuseuBridge.PlayConfig; o EmuseuSettingsCatalog guarda so o ativo.
//   Aqui lemos o pacote pedido direto do PlayConfig (o do idioma ativo vem do catalogo vivo, que inclui uploads recentes).
// - Cada busca (Pedir) escreve UMA linha de log [audio] sempre ligada: achado (qual fonte/idioma) ou nao achado.
// - Baixa com UnityWebRequestMultimedia (MPEG) e guarda o AudioClip em memoria por key+idioma. O mesmo arquivo (mesma URL sem query)
//   e baixado uma vez so, mesmo que PT e EN (fallback) pecam ao mesmo tempo.
// - URLs sao assinadas e expiram: so a parte antes do "?" e guardada (para saber se o arquivo mudou). Nunca persiste URL.
// - Qualquer erro (sem key, sem url, falha de rede, mp3 invalido) -> callback com null (quem chama mantem o clip do build).
public static class RemoteAudioCatalog
{
    public const int TimeoutSegundos = 30;

    sealed class Entrada
    {
        public string urlBase;
        public AudioClip clip;
    }

    struct Pedido
    {
        public string cacheKey;
        public Action<AudioClip> callback;
    }

    // Indice key -> setting de um pacote de idioma (reconstruido quando o pacote muda).
    sealed class MapaPacote
    {
        public EmuseuLanguage fonte;
        public List<EmuseuGame> jogos;
        public int total;
        public readonly Dictionary<string, EmuseuSetting> porChave = new Dictionary<string, EmuseuSetting>(StringComparer.Ordinal);
    }

    static readonly Dictionary<string, Entrada> prontos = new Dictionary<string, Entrada>(StringComparer.Ordinal);
    static readonly Dictionary<string, AudioClip> clipsPorUrl = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
    // Pendentes por urlBase (o mesmo arquivo nao baixa duas vezes).
    static readonly Dictionary<string, List<Pedido>> pendentes = new Dictionary<string, List<Pedido>>(StringComparer.Ordinal);
    static readonly MapaPacote mapaPt = new MapaPacote();
    static readonly MapaPacote mapaEn = new MapaPacote();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar()
    {
        // Seguro com o Domain Reload do editor desligado.
        prontos.Clear();
        clipsPorUrl.Clear();
        pendentes.Clear();
        LimparMapa(mapaPt);
        LimparMapa(mapaEn);
    }

    static void LimparMapa(MapaPacote m)
    {
        m.fonte = null;
        m.jogos = null;
        m.total = 0;
        m.porChave.Clear();
    }

    public static string ChaveEn(string key, string keyEn)
    {
        if (!string.IsNullOrWhiteSpace(keyEn)) return keyEn.Trim();
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim() + "_en";
    }

    // Resolve a URL (assinada) atual no catalogo de settings. false se a key/URL nao existe no payload.
    // EN cai no PT quando nao acha o EN. Nao escreve log (o log e feito em Pedir).
    public static bool TryResolverUrl(string key, string keyEn, bool ingles, out string url)
    {
        string origem;
        return Resolver(key, keyEn, ingles, out url, out origem);
    }

    // Resolve a URL e diz de onde veio (origem = texto curto para o log).
    static bool Resolver(string key, string keyEn, bool ingles, out string url, out string origem)
    {
        url = null;
        origem = null;
        if (string.IsNullOrWhiteSpace(key)) { origem = "key vazia"; return false; }
        key = key.Trim();

        if (!ingles)
        {
            if (AcharPt(key, out url, out origem)) return true;
            origem = "nao achado em PT (audio/file da key)";
            return false;
        }

        if (AcharEn(key, keyEn, out url, out origem)) return true;

        // Fallback: sem EN -> toca o PT.
        string urlPt, origemPt;
        if (AcharPt(key, out urlPt, out origemPt))
        {
            url = urlPt;
            origem = "PT (fallback, EN nao achado): " + origemPt;
            return true;
        }
        url = null;
        origem = "nao achado (nem EN nem PT)";
        return false;
    }

    // PT: audio.url da key; senao file.url da key (pacote ptbr).
    static bool AcharPt(string key, out string url, out string origem)
    {
        url = null;
        origem = null;
        EmuseuSetting s;
        if (!TryGetPacote(false, key, out s)) return false;

        url = UrlDe(s.audio);
        if (CardRemoteImageCache.EhHttp(url)) { origem = "ptbr:" + key + ".audio"; return true; }
        url = s.file != null ? s.file.url : null;
        if (CardRemoteImageCache.EhHttp(url)) { origem = "ptbr:" + key + ".file"; return true; }
        url = null;
        return false;
    }

    // EN: audioEn / audio da propria key (TTS) e key EN do tipo audio, nos pacotes eng e ptbr.
    static bool AcharEn(string key, string keyEn, out string url, out string origem)
    {
        url = null;
        origem = null;
        EmuseuSetting s;

        // 1) TTS da propria key: audioEn (eng, depois ptbr); audio do pacote eng.
        if (TryGetPacote(true, key, out s))
        {
            if (Usar(UrlDe(s.audioEn), "eng:" + key + ".audioEn", out url, out origem)) return true;
            if (Usar(UrlDe(s.audio), "eng:" + key + ".audio", out url, out origem)) return true;
        }
        if (TryGetPacote(false, key, out s))
        {
            if (Usar(UrlDe(s.audioEn), "ptbr:" + key + ".audioEn", out url, out origem)) return true;
        }

        // 2) key EN do tipo audio: keyEn explicita e/ou key + "_en" (eng primeiro, depois ptbr).
        string k1 = ChaveEn(key, keyEn);
        string k2 = key + "_en";
        for (int i = 0; i < 2; i++)
        {
            string k = i == 0 ? k1 : k2;
            if (string.IsNullOrEmpty(k) || k == key || (i == 1 && k == k1)) continue;
            for (int b = 0; b < 2; b++)
            {
                bool eng = b == 0;
                string nome = (eng ? "eng:" : "ptbr:") + k;
                if (!TryGetPacote(eng, k, out s)) continue;
                if (Usar(s.file != null ? s.file.url : null, nome + ".file", out url, out origem)) return true;
                if (Usar(UrlDe(s.audioEn), nome + ".audioEn", out url, out origem)) return true;
                if (Usar(UrlDe(s.audio), nome + ".audio", out url, out origem)) return true;
            }
        }
        url = null;
        origem = null;
        return false;
    }

    static bool Usar(string candidato, string nome, out string url, out string origem)
    {
        if (CardRemoteImageCache.EhHttp(candidato)) { url = candidato; origem = nome; return true; }
        url = null;
        origem = null;
        return false;
    }

    // Procura a key no pacote de idioma pedido (eng=true: campaign.eng; false: campaign.ptbr), sempre a partir do PlayConfig.
    // - Idioma ativo: usa o EmuseuSettingsCatalog (vivo; inclui uploads/atualizacoes recentes), depois o pacote.
    // - Outro idioma: usa o pacote guardado no PlayConfig (o catalogo so guarda o ativo).
    // - Payload antigo (sem campaign.ptbr/eng) ou sem PlayConfig: so existe o catalogo.
    static bool TryGetPacote(bool eng, string key, out EmuseuSetting setting)
    {
        setting = null;
        if (string.IsNullOrEmpty(key)) return false;

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
                // Chave duplicada: mantem a que tem audio/arquivo; senao a ultima.
                if (m.porChave.TryGetValue(s.key, out antigo) && TemMidia(antigo) && !TemMidia(s)) continue;
                m.porChave[s.key] = s;
            }
        }
        return m;
    }

    static bool TemMidia(EmuseuSetting s)
    {
        if (s == null) return false;
        if (s.file != null && !string.IsNullOrEmpty(s.file.url)) return true;
        return !string.IsNullOrEmpty(UrlDe(s.audio)) || !string.IsNullOrEmpty(UrlDe(s.audioEn));
    }

    // Clip ja baixado (e ainda valido) para esta key/idioma, sem iniciar download.
    public static bool TryGetCached(string key, string keyEn, bool ingles, out AudioClip clip)
    {
        clip = null;
        string url;
        if (!TryResolverUrl(key, keyEn, ingles, out url)) return false;
        Entrada e;
        if (!prontos.TryGetValue(ChaveCache(key, keyEn, ingles), out e) || e == null) return false;
        if (e.clip == null) return false;
        if (e.urlBase != UrlBase(url)) return false;
        clip = e.clip;
        return true;
    }

    // Pede o audio. O callback roda na thread principal (ou na hora, se ja esta pronto / nao existe).
    // callback(null) = indisponivel: quem chama deve manter o clip do build.
    public static void Pedir(string key, string keyEn, bool ingles, Action<AudioClip> callback)
    {
        try
        {
            string url, origem;
            bool achou = Resolver(key, keyEn, ingles, out url, out origem);
            string rotulo = (key == null ? "" : key.Trim()) + " [" + (ingles ? "EN" : "PT") + "]";
            if (!achou)
            {
                EmuseuDebug.Warn("audio", "busca " + rotulo + " -> NAO ACHADO: " + origem);
                Chamar(callback, null);
                return;
            }
            string urlBase = UrlBase(url);
            EmuseuDebug.Info("audio", "busca " + rotulo + " -> achado em " + origem + " (" + NomeCurto(urlBase) + ")");

            string cacheKey = ChaveCache(key, keyEn, ingles);

            Entrada e;
            if (prontos.TryGetValue(cacheKey, out e) && e != null && e.clip != null && e.urlBase == urlBase)
            {
                Chamar(callback, e.clip);
                return;
            }

            // Mesmo arquivo ja baixado por outra key/idioma (ex: EN caindo no PT).
            AudioClip existente;
            if (clipsPorUrl.TryGetValue(urlBase, out existente) && existente != null)
            {
                prontos[cacheKey] = new Entrada { urlBase = urlBase, clip = existente };
                Chamar(callback, existente);
                return;
            }

            var pedido = new Pedido { cacheKey = cacheKey, callback = callback };
            List<Pedido> lista;
            if (pendentes.TryGetValue(urlBase, out lista))
            {
                lista.Add(pedido);
                return;
            }
            lista = new List<Pedido>();
            lista.Add(pedido);
            pendentes[urlBase] = lista;
            Baixar(urlBase, cacheKey, url);
        }
        catch (Exception ex)
        {
            EmuseuDebug.Error("audio", "Pedir falhou: " + ex.GetType().Name + ": " + ex.Message);
            Chamar(callback, null);
        }
    }

    static void Baixar(string urlBase, string cacheKey, string url)
    {
        UnityWebRequest req = null;
        try
        {
            EmuseuDebug.Info("audio", "baixando " + cacheKey + " <- " + urlBase);
            req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
            req.timeout = TimeoutSegundos;
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            UnityWebRequest r = req;
            op.completed += _ => Concluir(urlBase, cacheKey, r);
        }
        catch (Exception ex)
        {
            EmuseuDebug.Error("audio", "falha ao iniciar download " + cacheKey + ": " + ex.GetType().Name + ": " + ex.Message);
            if (req != null) { try { req.Dispose(); } catch (Exception) { } }
            Finalizar(urlBase, null);
        }
    }

    static void Concluir(string urlBase, string cacheKey, UnityWebRequest req)
    {
        AudioClip clip = null;
        try
        {
            if (req.result == UnityWebRequest.Result.Success)
            {
                clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip != null) clip.name = "Remote_" + cacheKey;
            }
            if (clip == null)
                EmuseuDebug.Warn("audio", "download falhou " + cacheKey + " (http " + req.responseCode + "): " + (req.error ?? "clip nulo"));
        }
        catch (Exception ex)
        {
            clip = null;
            EmuseuDebug.Warn("audio", "erro ao ler mp3 " + cacheKey + ": " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            try { req.Dispose(); } catch (Exception) { }
        }

        if (clip != null)
        {
            clipsPorUrl[urlBase] = clip;
            EmuseuDebug.Info("audio", "pronto " + cacheKey + " (" + clip.length.ToString("0.00") + "s)");
        }
        Finalizar(urlBase, clip);
    }

    // Entrega o resultado a todos que pediram esse arquivo (PT e EN podem ser o mesmo).
    static void Finalizar(string urlBase, AudioClip clip)
    {
        List<Pedido> lista;
        if (!pendentes.TryGetValue(urlBase, out lista)) return;
        pendentes.Remove(urlBase);
        foreach (Pedido p in lista)
        {
            if (clip != null) prontos[p.cacheKey] = new Entrada { urlBase = urlBase, clip = clip };
            Chamar(p.callback, clip);
        }
    }

    static void Chamar(Action<AudioClip> cb, AudioClip clip)
    {
        if (cb == null) return;
        try { cb(clip); }
        catch (Exception ex)
        {
            EmuseuDebug.Error("audio", "callback falhou: " + ex.GetType().Name + ": " + ex.Message);
            Debug.LogWarning("[RemoteAudioCatalog] callback falhou: " + ex.Message);
        }
    }

    static string ChaveCache(string key, string keyEn, bool ingles)
    {
        string k = key == null ? "" : key.Trim();
        return ingles ? k + "|en|" + (ChaveEn(k, keyEn) ?? "") : k + "|pt";
    }

    // URL sem a query assinada: identifica o arquivo (o nome inclui um timestamp do upload).
    static string UrlBase(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        int i = url.IndexOf('?');
        return i >= 0 ? url.Substring(0, i) : url;
    }

    // So o nome do arquivo (para o log curto).
    static string NomeCurto(string urlBase)
    {
        if (string.IsNullOrEmpty(urlBase)) return "";
        int i = urlBase.LastIndexOf('/');
        return i >= 0 && i < urlBase.Length - 1 ? urlBase.Substring(i + 1) : urlBase;
    }

    // audio/audioEn pode ser string (url) ou objeto {url,...}.
    static string UrlDe(JToken t)
    {
        if (t == null || t.Type == JTokenType.Null) return null;
        if (t.Type == JTokenType.String) return (string)t;
        JObject o = t as JObject;
        if (o == null) return null;
        JToken u = o["url"];
        if (u == null || u.Type != JTokenType.String) return null;
        return (string)u;
    }
}