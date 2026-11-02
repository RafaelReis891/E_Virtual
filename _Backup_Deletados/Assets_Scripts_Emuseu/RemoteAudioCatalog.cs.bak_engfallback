using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

// Catalogo de AUDIOS remotos (vozes/musicas) vindos do site (payload play-config).
// - Key de tipo audio:  setting.file.url (MP3).
// - Key de tipo texto:  setting.audio (PT) / setting.audioEn (EN) = TTS, objeto {url,...} ou string.
// - EN: usa audioEn da propria key; se nao tiver, usa a key EN (explicita ou key + "_en") do tipo audio.
// - Baixa com UnityWebRequestMultimedia (MPEG) e guarda o AudioClip em memoria por key+idioma.
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

    static readonly Dictionary<string, Entrada> prontos = new Dictionary<string, Entrada>(StringComparer.Ordinal);
    static readonly Dictionary<string, List<Action<AudioClip>>> pendentes = new Dictionary<string, List<Action<AudioClip>>>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar()
    {
        // Seguro com o Domain Reload do editor desligado.
        prontos.Clear();
        pendentes.Clear();
    }

    public static string ChaveEn(string key, string keyEn)
    {
        if (!string.IsNullOrWhiteSpace(keyEn)) return keyEn.Trim();
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim() + "_en";
    }

    // Resolve a URL (assinada) atual no catalogo de settings. false se a key/URL nao existe no payload.
    public static bool TryResolverUrl(string key, string keyEn, bool ingles, out string url)
    {
        url = null;
        if (string.IsNullOrWhiteSpace(key)) return false;
        key = key.Trim();
        EmuseuSetting s;

        if (!ingles)
        {
            if (EmuseuSettingsCatalog.TryGet(key, out s) && s != null)
            {
                url = UrlDe(s.audio);
                if (!CardRemoteImageCache.EhHttp(url)) url = s.file != null ? s.file.url : null;
            }
            return CardRemoteImageCache.EhHttp(url);
        }

        // EN: 1) audioEn da propria key (TTS)
        if (EmuseuSettingsCatalog.TryGet(key, out s) && s != null) url = UrlDe(s.audioEn);
        // 2) key EN do tipo audio
        if (!CardRemoteImageCache.EhHttp(url))
        {
            string k2 = ChaveEn(key, keyEn);
            EmuseuSetting s2;
            if (k2 != null && k2 != key && EmuseuSettingsCatalog.TryGet(k2, out s2) && s2 != null)
            {
                url = s2.file != null ? s2.file.url : null;
                if (!CardRemoteImageCache.EhHttp(url)) url = UrlDe(s2.audioEn);
                if (!CardRemoteImageCache.EhHttp(url)) url = UrlDe(s2.audio);
            }
        }
        return CardRemoteImageCache.EhHttp(url);
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
            string url;
            if (!TryResolverUrl(key, keyEn, ingles, out url)) { Chamar(callback, null); return; }

            string cacheKey = ChaveCache(key, keyEn, ingles);
            string urlBase = UrlBase(url);

            Entrada e;
            if (prontos.TryGetValue(cacheKey, out e) && e != null && e.clip != null && e.urlBase == urlBase)
            {
                Chamar(callback, e.clip);
                return;
            }

            string idPendente = cacheKey + "|" + urlBase;
            List<Action<AudioClip>> lista;
            if (pendentes.TryGetValue(idPendente, out lista))
            {
                if (callback != null) lista.Add(callback);
                return;
            }
            lista = new List<Action<AudioClip>>();
            if (callback != null) lista.Add(callback);
            pendentes[idPendente] = lista;
            Baixar(idPendente, cacheKey, url, urlBase);
        }
        catch (Exception ex)
        {
            EmuseuDebug.Error("audio", "Pedir falhou: " + ex.GetType().Name + ": " + ex.Message);
            Chamar(callback, null);
        }
    }

    static void Baixar(string idPendente, string cacheKey, string url, string urlBase)
    {
        UnityWebRequest req = null;
        try
        {
            EmuseuDebug.Info("audio", "baixando " + cacheKey + " <- " + urlBase);
            req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
            req.timeout = TimeoutSegundos;
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            UnityWebRequest r = req;
            op.completed += _ => Concluir(idPendente, cacheKey, urlBase, r);
        }
        catch (Exception ex)
        {
            EmuseuDebug.Error("audio", "falha ao iniciar download " + cacheKey + ": " + ex.GetType().Name + ": " + ex.Message);
            if (req != null) { try { req.Dispose(); } catch (Exception) { } }
            Finalizar(idPendente, null);
        }
    }

    static void Concluir(string idPendente, string cacheKey, string urlBase, UnityWebRequest req)
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
            prontos[cacheKey] = new Entrada { urlBase = urlBase, clip = clip };
            EmuseuDebug.Info("audio", "pronto " + cacheKey + " (" + clip.length.ToString("0.00") + "s)");
        }
        Finalizar(idPendente, clip);
    }

    static void Finalizar(string idPendente, AudioClip clip)
    {
        List<Action<AudioClip>> lista;
        if (!pendentes.TryGetValue(idPendente, out lista)) return;
        pendentes.Remove(idPendente);
        foreach (Action<AudioClip> cb in lista) Chamar(cb, clip);
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