using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Protege o modal de upload do React (confirmar imagem) contra o jogo.
// Quando o admin clica num banner o jogo manda frame-upload/requested ao React; o React abre o explorador de arquivos.
// Ao voltar do explorador o navegador devolve o foco ao jogo: StarterAssetsInputs.OnApplicationFocus travava o cursor de novo
// e, com o cursor travado, o RemoteBannerClickDriver mira no CENTRO da tela (onde esta o banner do tour) a cada clique,
// reenviando "requested" (reinicia o fluxo e fecha o modal). Este guard, enquanto o upload esta pendente:
//  - destrava e mostra o cursor, solta o pointer lock do navegador (jslib novo) e tira o foco do canvas;
//  - impede religar o cursor (StarterAssetsInputs.cursorLocked/cursorInputForLook = false, componente desligado,
//    cursorForcedUnlock = true a cada frame, OnApplicationFocus reforca o destravamento);
//  - desliga FirstPersonController, PlayerInput, StarterAssetsInputs e o RemoteBannerClickDriver (e o N/B do tour via Ativo);
//  - WebGLInput.captureAllKeyboardInput = false (teclado vai para o React).
// Libera (restaura o estado anterior) quando o React termina: fase completed/failed/error/cancelled/..., Changed(chave)
// do EmuseuSettingsCatalog, Esc, ou timeout de 120 s. Cria-se sozinho, sem Inspector, sem editar cena.
public sealed class EmuseuUploadModalGuard : MonoBehaviour
{
    const string Tag = "upload";
    public const float TempoMaximo = 120f;
    const float AtrasoReativarClique = 0.6f;

    static EmuseuUploadModalGuard instancia;

    // true enquanto o modal de upload esta aberto. Outros scripts (ex.: RemoteBannerTour) devem ignorar teclas.
    public static bool Ativo { get { return instancia != null && instancia.ativo; } }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void EmuseuUploadGuard_ReleasePointer();
    [DllImport("__Internal")] static extern void EmuseuUploadGuard_BlurCanvas();
#else
    static void EmuseuUploadGuard_ReleasePointer() { }
    static void EmuseuUploadGuard_BlurCanvas() { }
#endif

    static readonly FieldInfo CampoForcado = typeof(StarterAssets.StarterAssetsInputs)
        .GetField("cursorForcedUnlock", BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly string[] FasesFinais =
    {
        "completed", "complete", "done", "success", "succeeded", "failed", "failure", "error", "cancelled", "canceled",
        "cancel", "aborted", "rejected", "closed", "dismissed", "declined",
    };

    sealed class EstadoInputs
    {
        public StarterAssets.StarterAssetsInputs inputs;
        public bool enabled, cursorLocked, cursorInputForLook, forcado;
    }

    bool ativo;
    string frameId;
    float inicio;
    float religarCliqueEm = -1f;
    CursorLockMode lockAnterior;
    bool visivelAnterior;
    bool capturaAnterior;

    readonly List<EstadoInputs> estadosInputs = new List<EstadoInputs>();
    readonly List<KeyValuePair<Behaviour, bool>> desligados = new List<KeyValuePair<Behaviour, bool>>();
    readonly List<RemoteBannerClickDriver> cliques = new List<RemoteBannerClickDriver>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { instancia = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (instancia != null) return;
        var go = new GameObject("EmuseuUploadModalGuard");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<EmuseuUploadModalGuard>();
    }

    void OnEnable()
    {
        EmuseuBridge.MensagemEnviada -= AoEnviarMensagem;
        EmuseuBridge.MensagemEnviada += AoEnviarMensagem;
        EmuseuBridge.FrameUploadRecebido -= AoReceberFase;
        EmuseuBridge.FrameUploadRecebido += AoReceberFase;
        EmuseuSettingsCatalog.Changed -= AoMudarCatalogo;
        EmuseuSettingsCatalog.Changed += AoMudarCatalogo;
    }

    void OnDisable()
    {
        EmuseuBridge.MensagemEnviada -= AoEnviarMensagem;
        EmuseuBridge.FrameUploadRecebido -= AoReceberFase;
        EmuseuSettingsCatalog.Changed -= AoMudarCatalogo;
    }

    void OnDestroy()
    {
        if (ativo) Liberar("destruido");
        if (instancia == this) instancia = null;
    }

    // ---------- gatilhos ----------

    void AoEnviarMensagem(string tipo, string payloadJson)
    {
        if (tipo != "frame-upload" || string.IsNullOrEmpty(payloadJson)) return;
        try
        {
            JObject p = JToken.Parse(payloadJson) as JObject;
            if (p == null) return;
            string fase = Texto(p, "phase");
            if (!string.Equals(fase, "requested", StringComparison.OrdinalIgnoreCase)) return;
            Abrir(Texto(p, "frameId"));
        }
        catch (Exception ex) { EmuseuDebug.Warn(Tag, "mensagem frame-upload ilegivel: " + ex.Message); }
    }

    void AoReceberFase(string json)
    {
        if (!ativo || string.IsNullOrEmpty(json)) return;
        try
        {
            JObject raiz = JToken.Parse(json) as JObject;
            if (raiz == null) return;
            JObject p = raiz["payload"] as JObject ?? raiz;
            string fase = Texto(p, "phase");
            string id = Texto(p, "frameId");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(frameId) && id != frameId) return;
            if (string.IsNullOrEmpty(fase)) return;
            foreach (string f in FasesFinais)
                if (string.Equals(fase, f, StringComparison.OrdinalIgnoreCase)) { Liberar("React: fase " + fase); return; }
            EmuseuDebug.Info(Tag, "fase '" + fase + "' (modal continua protegido)");
        }
        catch (Exception ex) { EmuseuDebug.Warn(Tag, "fase ilegivel: " + ex.Message); }
    }

    void AoMudarCatalogo(string key)
    {
        if (!ativo || string.IsNullOrEmpty(key)) return;
        if (string.IsNullOrEmpty(frameId) || key == frameId) Liberar("imagem enviada (" + key + ")");
    }

    // ---------- estado ----------

    void Abrir(string id)
    {
        if (ativo)
        {
            inicio = Time.unscaledTime; // novo pedido: renova o prazo
            frameId = id;
            return;
        }
        ativo = true;
        frameId = id;
        inicio = Time.unscaledTime;
        religarCliqueEm = -1f;
        lockAnterior = Cursor.lockState;
        visivelAnterior = Cursor.visible;

        estadosInputs.Clear();
        desligados.Clear();

        foreach (StarterAssets.StarterAssetsInputs si in FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Include))
        {
            if (si == null) continue;
            var e = new EstadoInputs
            {
                inputs = si,
                enabled = si.enabled,
                cursorLocked = si.cursorLocked,
                cursorInputForLook = si.cursorInputForLook,
                forcado = LerForcado(si),
            };
            estadosInputs.Add(e);
            si.cursorLocked = false;
            si.cursorInputForLook = false;
            si.move = Vector2.zero; si.look = Vector2.zero; si.jump = false; si.sprint = false;
            EscreverForcado(si, true);
            si.enabled = false;
        }

        foreach (StarterAssets.FirstPersonController fpc in FindObjectsByType<StarterAssets.FirstPersonController>(FindObjectsInactive.Include))
            Desligar(fpc);
#if ENABLE_INPUT_SYSTEM
        foreach (PlayerInput pi in FindObjectsByType<PlayerInput>(FindObjectsInactive.Include))
            Desligar(pi);
#endif
        foreach (RemoteBannerClickDriver cd in FindObjectsByType<RemoteBannerClickDriver>(FindObjectsInactive.Include))
        {
            if (cd == null) continue;
            if (!cliques.Contains(cd)) cliques.Add(cd);
            cd.enabled = false;
        }

        AplicarDestravado();
        SetarCapturaTeclado(false, true);
        EmuseuDebug.Info(Tag, "modal de upload aberto (" + id + "): cursor destravado, jogo sem input (max " + TempoMaximo.ToString("0") + "s, Esc libera)");
    }

    void Desligar(Behaviour b)
    {
        if (b == null) return;
        desligados.Add(new KeyValuePair<Behaviour, bool>(b, b.enabled));
        b.enabled = false;
    }

    void Liberar(string motivo)
    {
        if (!ativo) return;
        ativo = false;
        EmuseuDebug.Info(Tag, "modal de upload fechado (" + (frameId ?? "?") + "): " + motivo + "; restaurando estado");

        foreach (EstadoInputs e in estadosInputs)
        {
            if (e.inputs == null) continue;
            e.inputs.cursorLocked = e.cursorLocked;
            e.inputs.cursorInputForLook = e.cursorInputForLook;
            EscreverForcado(e.inputs, e.forcado);
            e.inputs.enabled = e.enabled;
        }
        foreach (var kv in desligados)
            if (kv.Key != null) kv.Key.enabled = kv.Value;

        Cursor.visible = visivelAnterior;
        Cursor.lockState = lockAnterior; // no WebGL o navegador so trava no proximo clique no jogo
        SetarCapturaTeclado(capturaAnterior, false);

        estadosInputs.Clear();
        desligados.Clear();
        religarCliqueEm = Time.unscaledTime + AtrasoReativarClique; // o clique que fechou o modal nao pode virar novo pedido
        frameId = null;
    }

    void AplicarDestravado()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EmuseuUploadGuard_ReleasePointer();
        EmuseuUploadGuard_BlurCanvas();
    }

    // ---------- loop ----------

    void Update()
    {
        if (!ativo)
        {
            if (religarCliqueEm >= 0f && Time.unscaledTime >= religarCliqueEm)
            {
                religarCliqueEm = -1f;
                foreach (RemoteBannerClickDriver cd in cliques) if (cd != null) cd.enabled = true;
                cliques.Clear();
            }
            return;
        }

        if (Time.unscaledTime - inicio > TempoMaximo) { Liberar("timeout de " + TempoMaximo.ToString("0") + "s"); return; }
        if (EscPressionado()) { Liberar("Esc"); return; }

        // reforco: qualquer coisa que tente travar de novo (ex.: StarterAssetsInputs.Update com botao direito) e desfeita.
        foreach (EstadoInputs e in estadosInputs)
        {
            if (e.inputs == null) continue;
            if (e.inputs.enabled) e.inputs.enabled = false;
            e.inputs.cursorLocked = false;
            e.inputs.cursorInputForLook = false;
            EscreverForcado(e.inputs, true);
        }
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EmuseuUploadGuard_ReleasePointer();
        }
    }

    // Volta do explorador de arquivos: o jogo recebe o foco de novo; reforca o destravamento (e ignora o relock do StarterAssets).
    void OnApplicationFocus(bool temFoco)
    {
        if (!ativo || !temFoco) return;
        AplicarDestravado();
        EmuseuDebug.Info(Tag, "foco voltou ao jogo com modal aberto: cursor mantido livre");
    }

    // ---------- utilidades ----------

    static bool EscPressionado()
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            Keyboard k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) return true;
        }
        catch (Exception) { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try { if (Input.GetKeyDown(KeyCode.Escape)) return true; }
        catch (Exception) { }
#endif
        return false;
    }

    static bool LerForcado(StarterAssets.StarterAssetsInputs si)
    {
        try { return CampoForcado != null && (bool)CampoForcado.GetValue(si); }
        catch (Exception) { return false; }
    }

    static void EscreverForcado(StarterAssets.StarterAssetsInputs si, bool v)
    {
        try { if (CampoForcado != null) CampoForcado.SetValue(si, v); }
        catch (Exception) { }
    }

    void SetarCapturaTeclado(bool valor, bool guardar)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            if (guardar) capturaAnterior = WebGLInput.captureAllKeyboardInput;
            WebGLInput.captureAllKeyboardInput = valor;
        }
        catch (Exception ex) { EmuseuDebug.Warn(Tag, "WebGLInput.captureAllKeyboardInput: " + ex.Message); }
#else
        if (guardar) capturaAnterior = true;
#endif
    }

    static string Texto(JObject o, string campo)
    {
        JToken t = o[campo];
        if (t == null || t.Type == JTokenType.Null) return null;
        return t.Type == JTokenType.String ? (string)t : t.ToString();
    }
}