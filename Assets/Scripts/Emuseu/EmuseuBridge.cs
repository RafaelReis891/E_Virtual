using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Scripting;

public enum SettingKind
{
    None,
    Text,
    Number,
    Boolean,
    Image,
    Audio,
    Video
}

static class SettingKindCodec
{
    public static string ToWire(SettingKind kind)
    {
        switch (kind)
        {
            case SettingKind.Text: return "text";
            case SettingKind.Number: return "number";
            case SettingKind.Boolean: return "boolean";
            case SettingKind.Image: return "image";
            case SettingKind.Audio: return "audio";
            case SettingKind.Video: return "video";
            default: return "";
        }
    }
}

[Serializable]
public struct FileSettingPayload
{
    public string key;
    public string type;
    public string phase;
}

[Serializable]
public struct TextSettingPayload
{
    public string key;
    public string type;
    public string phase;
    public string value;
}

[Serializable]
public struct NumberSettingPayload
{
    public string key;
    public string type;
    public string phase;
    public float value;
}

[Serializable]
public struct BooleanSettingPayload
{
    public string key;
    public string type;
    public string phase;
    public bool value;
}

[Serializable]
public class FileSettingMessage
{
    public int version = 1;
    public string channel = EmuseuBridge.Channel;
    public string type = "frame-upload";
    public FileSettingPayload payload;
}

[Serializable]
public class TextSettingMessage
{
    public int version = 1;
    public string channel = EmuseuBridge.Channel;
    public string type = "frame-upload";
    public TextSettingPayload payload;
}

[Serializable]
public class NumberSettingMessage
{
    public int version = 1;
    public string channel = EmuseuBridge.Channel;
    public string type = "frame-upload";
    public NumberSettingPayload payload;
}

[Serializable]
public class BooleanSettingMessage
{
    public int version = 1;
    public string channel = EmuseuBridge.Channel;
    public string type = "frame-upload";
    public BooleanSettingPayload payload;
}

// Ponte React <-> Unity do site emuseu.
// - Cria sozinha um GameObject "EmuseuBridge" (nao precisa por na cena).
// - O React chama SendMessage("EmuseuBridge", "OnPlayConfig", json).
// - Aceita o envelope completo {version, channel, type, payload} ou so o payload.
// - Unity -> React: play-config-request (pede o payload de novo, ex: URLs expiradas).
[DefaultExecutionOrder(-1000)]
public sealed class EmuseuBridge : MonoBehaviour
{
    public const string GameObjectName = "EmuseuBridge";
    public const string Channel = "unity-react";

    static EmuseuBridge _instance;
    public static EmuseuBridge Instance => _instance;

    // Disparado depois de ler um play-config (payload nunca e null aqui).
    public static event Action<EmuseuPayload> PlayConfigApplied;

    // Disparado ao enviar qualquer mensagem ao React (tipo, payloadJson). Usado pelo EmuseuUploadModalGuard.
    public static event Action<string, string> MensagemEnviada;

    // Disparado quando o React envia uma fase do upload (json cru de OnFrameUpload).
    public static event Action<string> FrameUploadRecebido;

    // Ultimo play-config lido (ou null).
    public static EmuseuPayload PlayConfig { get; private set; }

    static string _contentLanguage = "ptbr";

    /// <summary>True enquanto o site mandou pause. O olhar do mouse não gira a câmera nesse estado.</summary>
    public static bool Paused { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void EmuseuBridge_SendToReact(string json);
#else
    // Editor / outras plataformas: so mostra no Console.
    static void EmuseuBridge_SendToReact(string json)
    {
        Debug.Log("[EmuseuBridge] -> React (stub do editor): " + json);
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CriarSeNaoExistir()
    {
        if (_instance != null) return;
        var existente = FindFirstObjectByType<EmuseuBridge>();
        if (existente != null) { _instance = existente; return; }
        var go = new GameObject(GameObjectName);
        go.AddComponent<EmuseuBridge>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            EmuseuDebug.Warn("bridge", "segundo EmuseuBridge encontrado e destruido");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        // O nome tem que ser exatamente este: o React usa SendMessage("EmuseuBridge", ...).
        if (gameObject.name != GameObjectName) gameObject.name = GameObjectName;
        DontDestroyOnLoad(gameObject);
        EmuseuDebug.BridgeCriado();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        // Espera uns frames e pede o play-config (caso o React ainda nao tenha enviado).
        StartCoroutine(PedirPlayConfigDepoisDeAlgunsFrames());
    }

    IEnumerator PedirPlayConfigDepoisDeAlgunsFrames()
    {
        yield return null;
        yield return null;
        RequestPlayConfig();
    }

    // Pede ao React um play-config novo (ex: URLs assinadas expiraram).
    public void RequestPlayConfig()
    {
        Send("play-config-request", "{}");
    }

    // Entrada chamada pelo React via SendMessage.
    // [Preserve]: no WebGL (IL2CPP) metodos so chamados por SendMessage seriam cortados.
    [Preserve]
    public void OnPlayConfig(string json)
    {
        Debug.Log("[EmuseuBridge] OnPlayConfig len=" + (json == null ? 0 : json.Length));
        EmuseuDebug.PlayConfigRecebido(json);
        if (string.IsNullOrEmpty(json)) return;

        var payload = ParsePayload(json);
        EmuseuDebug.ParseResultado(payload);
        if (payload == null)
        {
            Debug.LogWarning("[EmuseuBridge] OnPlayConfig: payload invalido, ignorado");
            return;
        }

        if (!string.IsNullOrEmpty(payload.language))
            UseContentLanguage(payload.language);
        ApplyActiveLanguage(payload);
        PlayConfig = payload;
        int albums = payload.campaign != null && payload.campaign.albums != null ? payload.campaign.albums.Count : 0;
        Debug.Log("[EmuseuBridge] play-config aplicado. albuns=" + albums);

        EmuseuDebug.Info("bridge", "ouvintes de PlayConfigApplied=" + (PlayConfigApplied != null ? PlayConfigApplied.GetInvocationList().Length : 0));
        try { PlayConfigApplied?.Invoke(payload); }
        catch (Exception ex) { EmuseuDebug.Error("bridge", "listener de PlayConfigApplied falhou: " + ex.GetType().Name + ": " + ex.Message); Debug.LogWarning("[EmuseuBridge] listener de PlayConfigApplied falhou: " + ex.Message); }
    }

    // Play e pause chegam no mesmo metodo. paused true para a cena; false volta a rodar.
    [Preserve]
    public void OnGamePaused(string json)
    {
        bool paused = ReadPaused(json);
        Paused = paused;
        StarterAssets.StarterAssetsInputs.GamePaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        AplicarMouse(paused);
        EmuseuDebug.Info("bridge", "OnGamePaused paused=" + paused);
    }

    // Volume do jogo apenas. Nao pausa o audio e nao mexe na luneta nem na anfitria.
    [Preserve]
    public void OnGameVolume(string json)
    {
        float volume;
        if (!ReadVolume(json, out volume)) return;
        AudioListener.volume = volume;
        EmuseuDebug.Info("bridge", "OnGameVolume volume=" + volume.ToString("0.00"));
    }

    // Play prende o cursor e devolve o olhar. Pause solta o cursor e zera o olhar.
    // O modal de upload já segurou o mouse: um play não pisa nele.
    static void AplicarMouse(bool paused)
    {
        if (!paused && EmuseuUploadModalGuard.Ativo) return;

        foreach (StarterAssets.StarterAssetsInputs si in FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Include))
        {
            if (si == null) continue;
            si.look = Vector2.zero;
            if (!paused) continue;
            si.move = Vector2.zero;
            si.jump = false;
            si.sprint = false;
            si.cursorLocked = false;
            si.cursorInputForLook = false;
            si.UnlockCursor(true);
        }

        if (paused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        foreach (StarterAssets.StarterAssetsInputs si in FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Include))
        {
            if (si == null) continue;
            si.cursorLocked = true;
            si.cursorInputForLook = true;
            si.UnlockCursor(false);
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Troca o idioma ativo. eng e en viram eng; qualquer outro valor vira ptbr.
    // Ingles vazio permanece vazio: nao copia o portugues por cima.
    [Preserve]
    public void OnContentLanguage(string json)
    {
        string language = ReadLanguage(json);
        if (string.IsNullOrEmpty(language)) return;
        UseContentLanguage(language);
        if (PlayConfig == null) return;
        ApplyActiveLanguage(PlayConfig);
        try { PlayConfigApplied?.Invoke(PlayConfig); }
        catch (Exception ex) { EmuseuDebug.Error("bridge", "listener de PlayConfigApplied falhou: " + ex.Message); }
    }

    public static void UseContentLanguage(string language)
    {
        _contentLanguage = language == "eng" || language == "en" ? "eng" : "ptbr";
    }

    // Projeta o pacote do idioma ativo em campaign.games e campaign.albums.
    // Sem ptbr/eng, o payload antigo (albuns direto na campanha) permanece.
    public static void ApplyActiveLanguage(EmuseuPayload payload)
    {
        if (payload == null) return;
        payload.language = _contentLanguage;
        EmuseuCampaign campaign = payload.campaign;
        if (campaign == null) return;
        if (campaign.ptbr == null && campaign.eng == null) return;

        EmuseuLanguage bundle = _contentLanguage == "eng" ? campaign.eng : campaign.ptbr;
        campaign.games = bundle != null && bundle.games != null ? bundle.games : new List<EmuseuGame>();
        campaign.albums = bundle != null && bundle.albums != null ? bundle.albums : new List<EmuseuAlbum>();
        if (bundle != null && bundle.sponsors != null) payload.sponsors = bundle.sponsors;
    }

    public void RequestTextSetting(string key, string value)
    {
        var kind = SettingKind.Text;
        if (!CanSendSetting(key, kind)) return;
        var payload = new TextSettingPayload
        {
            key = key,
            type = SettingKindCodec.ToWire(kind),
            phase = "requested",
            value = value ?? "",
        };
        SendObject(new TextSettingMessage { payload = payload });
    }

    public void RequestNumberSetting(string key, float value)
    {
        var kind = SettingKind.Number;
        if (!CanSendSetting(key, kind)) return;
        var payload = new NumberSettingPayload
        {
            key = key,
            type = SettingKindCodec.ToWire(kind),
            phase = "requested",
            value = value,
        };
        SendObject(new NumberSettingMessage { payload = payload });
    }

    public void RequestBooleanSetting(string key, bool value)
    {
        var kind = SettingKind.Boolean;
        if (!CanSendSetting(key, kind)) return;
        var payload = new BooleanSettingPayload
        {
            key = key,
            type = SettingKindCodec.ToWire(kind),
            phase = "requested",
            value = value,
        };
        SendObject(new BooleanSettingMessage { payload = payload });
    }

    public void RequestFileSetting(string key, SettingKind kind)
    {
        if (kind != SettingKind.Image && kind != SettingKind.Audio && kind != SettingKind.Video)
        {
            Debug.LogWarning("[EmuseuBridge] RequestFileSetting tipo invalido " + kind + " key=" + key);
            return;
        }
        if (!CanSendSetting(key, kind)) return;
        var payload = new FileSettingPayload
        {
            key = key,
            type = SettingKindCodec.ToWire(kind),
            phase = "requested",
        };
        SendObject(new FileSettingMessage { payload = payload });
    }

    static bool CanSendSetting(string key, SettingKind kind)
    {
        if (UsuarioRoot()) return true;
        Debug.Log("[EmuseuBridge] RequestSetting ignorado (sem role root) key=" + key + " type=" + SettingKindCodec.ToWire(kind));
        return false;
    }

    static bool UsuarioRoot()
    {
        var roles = PlayConfig != null && PlayConfig.user != null ? PlayConfig.user.roles : null;
        if (roles == null) return false;
        foreach (string role in roles)
            if (string.Equals(role, "root", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static void SendObject(object message)
    {
        string envelope = JsonUtility.ToJson(message);
        string payloadJson = "{}";
        try
        {
            var root = JObject.Parse(envelope);
            if (root["payload"] != null) payloadJson = root["payload"].ToString(Formatting.None);
        }
        catch (Exception) { }
        EmuseuDebug.Enviado("frame-upload", envelope.Length);
        try { MensagemEnviada?.Invoke("frame-upload", payloadJson); }
        catch (Exception ex) { EmuseuDebug.Error("bridge", "listener de MensagemEnviada falhou: " + ex.Message); }
        EmuseuBridge_SendToReact(envelope);
    }

    static bool ReadPaused(string json)
    {
        if (string.IsNullOrEmpty(json)) return false;
        try
        {
            var raiz = JToken.Parse(json) as JObject;
            if (raiz == null) return false;
            JToken valor = raiz["payload"] is JObject interno ? interno["paused"] : raiz["paused"];
            if (valor == null || valor.Type == JTokenType.Null) return false;
            if (valor.Type == JTokenType.Boolean) return (bool)valor;
            bool parsed;
            return bool.TryParse(valor.ToString(), out parsed) && parsed;
        }
        catch (Exception) { return false; }
    }

    static bool ReadVolume(string json, out float volume)
    {
        volume = 1f;
        if (string.IsNullOrEmpty(json)) return false;
        try
        {
            var raiz = JToken.Parse(json) as JObject;
            if (raiz == null) return false;
            JToken valor = raiz["payload"] is JObject interno ? interno["volume"] : raiz["volume"];
            if (valor == null || valor.Type == JTokenType.Null) return false;
            float parsed;
            if (!float.TryParse(valor.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed))
                return false;
            volume = Mathf.Clamp01(parsed);
            return true;
        }
        catch (Exception) { return false; }
    }

    static string ReadLanguage(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var raiz = JToken.Parse(json) as JObject;
            if (raiz == null) return null;
            JToken valor = raiz["payload"] is JObject interno ? interno["language"] : raiz["language"];
            if (valor == null || valor.Type == JTokenType.Null) return null;
            return valor.ToString();
        }
        catch (Exception) { return null; }
    }

    // Entrada chamada pelo React via SendMessage: fases do upload (picking/uploading/completed/failed...).
    [Preserve]
    public void OnFrameUpload(string json)
    {
        EmuseuDebug.Info("banner", "OnFrameUpload len=" + (json == null ? 0 : json.Length));
        if (string.IsNullOrEmpty(json)) return;
        EmuseuSettingsCatalog.HandleFrameUpload(json);
        try { FrameUploadRecebido?.Invoke(json); }
        catch (Exception ex) { EmuseuDebug.Error("bridge", "listener de FrameUploadRecebido falhou: " + ex.Message); }
    }

    // Envia ao React qualquer mensagem do canal unity-react (ex: "frame-upload"). payloadJson = so o objeto payload.
    public static void SendToReact(string type, string payloadJson)
    {
        Send(type, payloadJson);
    }

    // Le o JSON (envelope completo ou payload puro) com Newtonsoft. Retorna null se nao der.
    public static EmuseuPayload ParsePayload(string json)
    {
        try
        {
            var raiz = JToken.Parse(json) as JObject;
            if (raiz == null) { EmuseuDebug.ParseErro("raiz do json nao e um objeto"); return null; }

            JObject alvo = raiz;
            // Envelope: tem "payload" objeto e nao tem "campaign" direto.
            if (raiz["campaign"] == null && raiz["payload"] is JObject interno)
            {
                string tipo = raiz.Value<string>("type");
                if (!string.IsNullOrEmpty(tipo) && tipo != "play-config")
                {
                    EmuseuDebug.ParseErro("tipo inesperado: " + tipo);
                    Debug.LogWarning("[EmuseuBridge] tipo inesperado: " + tipo);
                    return null;
                }
                alvo = interno;
            }
            EmuseuDebug.Info("parse", alvo == raiz ? "formato: payload puro (sem envelope)" : "formato: envelope com payload");

            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Error = (s, e) =>
                {
                    // Campo com tipo estranho: avisa e segue em frente.
                    EmuseuDebug.ParseAviso(e.ErrorContext.Path, e.ErrorContext.Error.Message);
                    Debug.LogWarning("[EmuseuBridge] json: " + e.ErrorContext.Path + " : " + e.ErrorContext.Error.Message);
                    e.ErrorContext.Handled = true;
                }
            });
            return alvo.ToObject<EmuseuPayload>(serializer);
        }
        catch (Exception ex)
        {
            EmuseuDebug.ParseErro(ex.GetType().Name + ": " + ex.Message);
            Debug.LogWarning("[EmuseuBridge] falha ao ler json: " + ex.Message);
            return null;
        }
    }

    // Monta o envelope {version, channel, type, payload} e manda para o React.
    static void Send(string type, string payloadJson)
    {
        string envelope = "{\"version\":1,\"channel\":\"" + Channel + "\",\"type\":\"" + type +
                          "\",\"payload\":" + (string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson) + "}";
        EmuseuDebug.Enviado(type, envelope.Length);
        try { MensagemEnviada?.Invoke(type, payloadJson); }
        catch (Exception ex) { EmuseuDebug.Error("bridge", "listener de MensagemEnviada falhou: " + ex.Message); }
        EmuseuBridge_SendToReact(envelope);
    }
}