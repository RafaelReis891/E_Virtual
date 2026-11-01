using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Catalogo estatico dos settings do site (campaign.games[].settings[]): chave -> setting.
// - Le sozinho cada play-config (assina EmuseuBridge.PlayConfigApplied).
// - Changed(key): setting dessa chave mudou (key == null: catalogo inteiro recarregado).
// - IsAdmin: user.roles contem "root" (pode pedir upload de quadro).
// - HandleFrameUpload: fases do upload vindas do React (chamado por EmuseuBridge.OnFrameUpload).
public static class EmuseuSettingsCatalog
{
    public const string RoleRoot = "root";

    public static event Action<string> Changed;

    static readonly Dictionary<string, EmuseuSetting> porChave = new Dictionary<string, EmuseuSetting>(StringComparer.Ordinal);
    static bool admin;
    static bool inscrito;

    public static bool IsAdmin { get { Garantir(); return admin; } }
    public static int Count { get { Garantir(); return porChave.Count; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar()
    {
        porChave.Clear();
        admin = false;
        inscrito = false;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar() { Garantir(); }

    static void Garantir()
    {
        if (inscrito) return;
        inscrito = true;
        EmuseuBridge.PlayConfigApplied -= AoReceberPlayConfig;
        EmuseuBridge.PlayConfigApplied += AoReceberPlayConfig;
        if (EmuseuBridge.PlayConfig != null) Carregar(EmuseuBridge.PlayConfig, false);
    }

    static void AoReceberPlayConfig(EmuseuPayload payload)
    {
        Carregar(payload, true);
    }

    // Reconstroi o catalogo a partir de um payload (publico para testes).
    public static void Carregar(EmuseuPayload payload, bool avisar)
    {
        porChave.Clear();
        admin = false;
        if (payload == null) return;

        if (payload.user != null && payload.user.roles != null)
        {
            foreach (string r in payload.user.roles)
                if (string.Equals(r, RoleRoot, StringComparison.OrdinalIgnoreCase)) { admin = true; break; }
        }

        int total = 0, imagens = 0;
        if (payload.campaign != null && payload.campaign.games != null)
        {
            foreach (EmuseuGame g in payload.campaign.games)
            {
                if (g == null || g.settings == null) continue;
                foreach (EmuseuSetting s in g.settings)
                {
                    if (s == null || string.IsNullOrEmpty(s.key)) continue;
                    EmuseuSetting antigo;
                    if (porChave.TryGetValue(s.key, out antigo))
                    {
                        EmuseuDebug.Warn("banner", "chave duplicada no payload: " + s.key + " (jogo " + g.id + ")");
                        // Mantem o que tem url; senao o ultimo.
                        if (TemUrl(antigo) && !TemUrl(s)) continue;
                    }
                    porChave[s.key] = s;
                    total++;
                    if (EhImagem(s)) imagens++;
                }
            }
        }
        EmuseuDebug.Info("banner", "catalogo de settings: " + porChave.Count + " chaves (" + imagens + " imagens), admin=" + admin);
        if (avisar) Disparar(null);
    }

    public static bool TryGet(string key, out EmuseuSetting setting)
    {
        Garantir();
        setting = null;
        return !string.IsNullOrEmpty(key) && porChave.TryGetValue(key, out setting);
    }

    // true se a chave existe, e do tipo image e tem url.
    public static bool TryGetImageUrl(string key, out string url)
    {
        url = null;
        EmuseuSetting s;
        if (!TryGet(key, out s) || !EhImagem(s) || !TemUrl(s)) return false;
        url = s.file.url;
        return true;
    }

    public static IEnumerable<string> Chaves { get { Garantir(); return porChave.Keys; } }

    // Atualiza (ou cria) a imagem de uma chave, ex: depois de upload completed, e avisa os ouvintes.
    public static void SetImageUrl(string key, string url)
    {
        Garantir();
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(url)) return;
        EmuseuSetting s;
        if (!porChave.TryGetValue(key, out s) || s == null)
        {
            s = new EmuseuSetting { key = key };
            porChave[key] = s;
        }
        s.type = "image";
        if (s.file == null) s.file = new EmuseuFile();
        s.file.url = url;
        EmuseuDebug.Info("banner", "catalogo: imagem de '" + key + "' atualizada");
        Disparar(key);
    }

    // JSON do React: envelope {type:'frame-upload', payload:{frameId, phase, progress?, url?}} ou so o payload.
    public static void HandleFrameUpload(string json)
    {
        JObject raiz;
        try { raiz = JToken.Parse(json) as JObject; }
        catch (Exception ex) { EmuseuDebug.Error("banner", "OnFrameUpload json invalido: " + ex.Message); return; }
        if (raiz == null) { EmuseuDebug.Error("banner", "OnFrameUpload: raiz nao e objeto"); return; }

        JObject p = raiz["payload"] as JObject ?? raiz;
        string frameId = Texto(p, "frameId");
        string phase = Texto(p, "phase");
        string url = Texto(p, "url");
        string progress = Texto(p, "progress");
        string erro = Texto(p, "error");

        EmuseuDebug.Info("banner", "frame-upload frameId=" + frameId + " phase=" + phase +
                         (string.IsNullOrEmpty(progress) ? "" : " progress=" + progress) +
                         (string.IsNullOrEmpty(erro) ? "" : " erro=" + erro));
        if (string.IsNullOrEmpty(frameId)) return;

        if (string.Equals(phase, "completed", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(url)) { EmuseuDebug.Warn("banner", "completed sem url para " + frameId); return; }
            SetImageUrl(frameId, url);
        }
    }

    static string Texto(JObject o, string campo)
    {
        JToken t = o[campo];
        if (t == null || t.Type == JTokenType.Null) return null;
        return t.Type == JTokenType.String ? (string)t : t.ToString();
    }

    static bool EhImagem(EmuseuSetting s)
    {
        return s != null && string.Equals(s.type, "image", StringComparison.OrdinalIgnoreCase);
    }

    static bool TemUrl(EmuseuSetting s)
    {
        return s != null && s.file != null && !string.IsNullOrEmpty(s.file.url);
    }

    static void Disparar(string key)
    {
        Action<string> h = Changed;
        if (h == null) return;
        foreach (Delegate d in h.GetInvocationList())
        {
            try { ((Action<string>)d)(key); }
            catch (Exception ex)
            {
                EmuseuDebug.Error("banner", "listener de Changed falhou: " + ex.GetType().Name + ": " + ex.Message);
                Debug.LogWarning("[EmuseuSettingsCatalog] listener falhou: " + ex.Message);
            }
        }
    }
}