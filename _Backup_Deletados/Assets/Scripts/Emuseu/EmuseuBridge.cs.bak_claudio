using System;
using System.Collections;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Scripting;

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

        PlayConfig = payload;
        int albums = payload.campaign != null && payload.campaign.albums != null ? payload.campaign.albums.Count : 0;
        Debug.Log("[EmuseuBridge] play-config aplicado. albuns=" + albums);

        EmuseuDebug.Info("bridge", "ouvintes de PlayConfigApplied=" + (PlayConfigApplied != null ? PlayConfigApplied.GetInvocationList().Length : 0));
        try { PlayConfigApplied?.Invoke(payload); }
        catch (Exception ex) { EmuseuDebug.Error("bridge", "listener de PlayConfigApplied falhou: " + ex.GetType().Name + ": " + ex.Message); Debug.LogWarning("[EmuseuBridge] listener de PlayConfigApplied falhou: " + ex.Message); }
    }

    // Entrada chamada pelo React via SendMessage: fases do upload de quadro (picking/uploading/completed/failed...).
    // 'completed' com url troca o banner na hora (ver EmuseuSettingsCatalog.HandleFrameUpload).
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