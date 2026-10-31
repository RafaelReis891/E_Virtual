using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Controle do jogador para as ferramentas de admin (usado pelo AdminToolsPanel; criado por ele, sem Inspector).
//  1) Bloqueio de input com o painel aberto (mesmo padrao do EmuseuUploadModalGuard, que nao expoe API para isso):
//     cursor livre, StarterAssetsInputs/FirstPersonController/PlayerInput/RemoteBannerClickDriver desligados.
//  2) God mode: desliga FirstPersonController (gravidade/movimento) e o CharacterController (colisao), voa com WASD
//     relativo a camera, Space sobe, Ctrl/Q desce, Shift rapido; olhar com o mouse continua (usa StarterAssetsInputs.look).
//     Ao desligar restaura tudo (enabled originais, CharacterController, Rigidbody, pitch da camera).
// Todo estado original e guardado por componente e devolvido ao liberar.
public sealed class AdminPlayerControl : MonoBehaviour
{
    const string Tag = "admin";
    const float AtrasoReativarClique = 0.6f;

#if UNITY_WEBGL && !UNITY_EDITOR
    // Mesma funcao do EmuseuUploadGuard.jslib (ja existe no projeto): solta o pointer lock do navegador.
    [DllImport("__Internal")] static extern void EmuseuUploadGuard_ReleasePointer();
#else
    static void EmuseuUploadGuard_ReleasePointer() { }
#endif

    static readonly FieldInfo CampoForcado = typeof(StarterAssets.StarterAssetsInputs)
        .GetField("cursorForcedUnlock", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo CampoPitch = typeof(StarterAssets.FirstPersonController)
        .GetField("_cinemachineTargetPitch", BindingFlags.Instance | BindingFlags.NonPublic);

    enum Tipo { SoPainel, Controlador, Inputs, Clique }

    sealed class Entrada
    {
        public Behaviour comp;
        public Tipo tipo;
        public bool original;
        public float segurarAte;
        // so para Tipo.Inputs
        public StarterAssets.StarterAssetsInputs sai;
        public bool cursorLocked, cursorInputForLook, forcado;
    }

    readonly List<Entrada> entradas = new List<Entrada>();

    // ---- estado publico ----
    public bool PainelAberto { get; private set; }
    public bool GodLigado { get; private set; }
    public bool GodPersistente = true;     // mantem o god mode ao trocar de cena
    public float VelNormal = 8f;
    public float VelRapido = 30f;
    // true enquanto o god mode esta realmente aplicado em um jogador
    public bool GodAplicado { get { return godAplicado; } }

    // ---- cursor (painel) ----
    CursorLockMode lockAnterior;
    bool visivelAnterior;

    // ---- god mode ----
    bool godAplicado;
    Transform alvo;
    CharacterController cc;
    bool ccOriginal;
    Rigidbody rb;
    bool rbKinematic;
    StarterAssets.FirstPersonController fpc;
    StarterAssets.StarterAssetsInputs sai;
    Transform alvoPitch;
    float pitch;
    float proximaBuscaGod;
    float proximaBuscaEntradas;

    void OnEnable() { SceneManager.sceneLoaded += AoCarregarCena; }
    void OnDisable() { SceneManager.sceneLoaded -= AoCarregarCena; }

    void OnDestroy()
    {
        LiberarTudo();
    }

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        // jogador da cena anterior foi destruido: esquece as referencias (nada a restaurar nelas)
        EsquecerAlvo();
        entradas.Clear();
        proximaBuscaGod = 0f;
        proximaBuscaEntradas = 0f;
        if (!GodPersistente) GodLigado = false;
    }

    // ---------- API ----------

    public void DefinirPainel(bool aberto)
    {
        if (aberto == PainelAberto) return;
        PainelAberto = aberto;
        if (aberto)
        {
            lockAnterior = Cursor.lockState;
            visivelAnterior = Cursor.visible;
            Descobrir();
            AplicarCursorLivre();
        }
        else
        {
            foreach (Entrada e in entradas)
                if (e.tipo == Tipo.Clique) e.segurarAte = Time.unscaledTime + AtrasoReativarClique; // o clique que fechou nao pode virar upload
            Cursor.visible = visivelAnterior;
            Cursor.lockState = lockAnterior; // no WebGL o navegador so trava no proximo clique no jogo
            ProcessarEntradas();
        }
    }

    public void DefinirGod(bool ligado)
    {
        if (ligado == GodLigado) return;
        GodLigado = ligado;
        if (ligado)
        {
            Descobrir();
            TentarAplicarGod();
        }
        else
        {
            RestaurarGod();
            ProcessarEntradas();
        }
    }

    // Desliga painel e god mode, restaurando tudo (ex.: usuario deixou de ser admin).
    public void LiberarTudo()
    {
        GodLigado = false;
        RestaurarGod();
        if (PainelAberto)
        {
            PainelAberto = false;
            Cursor.visible = visivelAnterior;
            Cursor.lockState = lockAnterior;
        }
        for (int i = entradas.Count - 1; i >= 0; i--) Restaurar(entradas[i]);
        entradas.Clear();
    }

    // ---------- descoberta de componentes ----------

    void Descobrir()
    {
        if (!PainelAberto && !GodLigado) return;

        foreach (StarterAssets.FirstPersonController f in FindObjectsByType<StarterAssets.FirstPersonController>(FindObjectsInactive.Include))
            Adicionar(f, Tipo.Controlador);

        if (PainelAberto)
        {
            foreach (StarterAssets.StarterAssetsInputs s in FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Include))
                Adicionar(s, Tipo.Inputs);
            // PlayerInput NAO e mais desligado: StarterAssetsInputs/FirstPersonController desligados ja bloqueiam o jogador, e mexer no
            // PlayerInput (ativar/desativar action maps) pode interferir nas actions de UI do Input System (InputForUI/IMGUI).
            foreach (RemoteBannerClickDriver cd in FindObjectsByType<RemoteBannerClickDriver>(FindObjectsInactive.Include))
                Adicionar(cd, Tipo.Clique);
        }
    }

    void Adicionar(Behaviour b, Tipo tipo)
    {
        if (b == null) return;
        foreach (Entrada x in entradas) if (x.comp == b) return;

        var e = new Entrada { comp = b, tipo = tipo, original = b.enabled };
        if (tipo == Tipo.Inputs)
        {
            e.sai = b as StarterAssets.StarterAssetsInputs;
            if (e.sai == null) return;
            e.cursorLocked = e.sai.cursorLocked;
            e.cursorInputForLook = e.sai.cursorInputForLook;
            e.forcado = LerForcado(e.sai);
        }
        entradas.Add(e);
        AplicarBloqueio(e);
    }

    static void AplicarBloqueio(Entrada e)
    {
        if (e.comp == null) return;
        if (e.tipo == Tipo.Inputs && e.sai != null)
        {
            e.sai.cursorLocked = false;
            e.sai.cursorInputForLook = false;
            e.sai.move = Vector2.zero; e.sai.look = Vector2.zero; e.sai.jump = false; e.sai.sprint = false;
            EscreverForcado(e.sai, true);
        }
        if (e.comp.enabled) e.comp.enabled = false;
    }

    static void Restaurar(Entrada e)
    {
        if (e.comp == null) return;
        if (e.tipo == Tipo.Inputs && e.sai != null)
        {
            e.sai.cursorLocked = e.cursorLocked;
            e.sai.cursorInputForLook = e.cursorInputForLook;
            EscreverForcado(e.sai, e.forcado);
        }
        e.comp.enabled = e.original;
    }

    bool Precisa(Entrada e)
    {
        if (PainelAberto) return true;
        if (e.tipo == Tipo.Controlador && GodLigado) return true;
        return Time.unscaledTime < e.segurarAte;
    }

    void ProcessarEntradas()
    {
        for (int i = entradas.Count - 1; i >= 0; i--)
        {
            Entrada e = entradas[i];
            if (e.comp == null) { entradas.RemoveAt(i); continue; }
            if (Precisa(e))
            {
                // reforco: qualquer coisa que tente religar (ex.: StarterAssetsInputs travando o cursor) e desfeita
                if (e.tipo == Tipo.Inputs && e.sai != null && PainelAberto)
                {
                    e.sai.cursorLocked = false;
                    e.sai.cursorInputForLook = false;
                    e.sai.move = Vector2.zero; e.sai.look = Vector2.zero; e.sai.jump = false; e.sai.sprint = false; // PlayerInput segue ativo e continua enviando OnMove/OnLook
                    EscreverForcado(e.sai, true);
                }
                if (e.comp.enabled) e.comp.enabled = false;
            }
            else
            {
                Restaurar(e);
                entradas.RemoveAt(i);
            }
        }
    }

    // ---------- loop ----------

    void Update()
    {
        if (!PainelAberto && !GodLigado && entradas.Count == 0 && !godAplicado) return;

        if ((PainelAberto || GodLigado) && Time.unscaledTime >= proximaBuscaEntradas)
        {
            proximaBuscaEntradas = Time.unscaledTime + 0.5f;
            Descobrir();
            if (PainelAberto) EmuseuUploadGuard_ReleasePointer(); // o navegador pode manter o pointer lock mesmo com Cursor.lockState = None
        }
        ProcessarEntradas();

        if (PainelAberto)
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible) AplicarCursorLivre();
            return;
        }

        if (!GodLigado) return;

        if (!godAplicado)
        {
            if (Time.unscaledTime >= proximaBuscaGod)
            {
                proximaBuscaGod = Time.unscaledTime + 0.25f;
                TentarAplicarGod();
            }
            return;
        }
        Voar();
    }

    void LateUpdate()
    {
        // algo que rode depois do Update (ou no mesmo frame) nao pode religar o cursor com o painel aberto
        if (PainelAberto && (Cursor.lockState != CursorLockMode.None || !Cursor.visible)) AplicarCursorLivre();
    }

    void OnApplicationFocus(bool temFoco)
    {
        // volta do navegador/explorador de arquivos: o StarterAssetsInputs trava o cursor de novo; com painel aberto reforca o destravamento
        if (PainelAberto && temFoco) AplicarCursorLivre();
    }

    void AplicarCursorLivre()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EmuseuUploadGuard_ReleasePointer();
    }

    // ---------- god mode ----------

    void EsquecerAlvo()
    {
        godAplicado = false;
        alvo = null; cc = null; rb = null; fpc = null; sai = null; alvoPitch = null;
    }

    void TentarAplicarGod()
    {
        if (godAplicado) return;

        // jogador: FirstPersonController ativo; senao CharacterController dentro de "Player_Things"; senao tag Player
        StarterAssets.FirstPersonController f = null;
        foreach (StarterAssets.FirstPersonController c in FindObjectsByType<StarterAssets.FirstPersonController>(FindObjectsInactive.Exclude))
            if (c != null && c.gameObject.activeInHierarchy) { f = c; break; }

        CharacterController controlador = null;
        if (f != null) controlador = f.GetComponent<CharacterController>();
        if (controlador == null)
        {
            GameObject pt = GameObject.Find("Player_Things");
            if (pt != null) controlador = pt.GetComponentInChildren<CharacterController>();
        }
        if (controlador == null)
        {
            try
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) controlador = p.GetComponentInChildren<CharacterController>();
            }
            catch (Exception) { }
        }
        if (controlador == null || !controlador.gameObject.activeInHierarchy) return; // ainda sem jogador (intro/carregando): tenta de novo

        fpc = f != null ? f : controlador.GetComponent<StarterAssets.FirstPersonController>();
        sai = controlador.GetComponent<StarterAssets.StarterAssetsInputs>();
        cc = controlador;
        alvo = controlador.transform;
        ccOriginal = cc.enabled;
        cc.enabled = false;

        rb = alvo.GetComponent<Rigidbody>();
        if (rb != null) { rbKinematic = rb.isKinematic; rb.isKinematic = true; }

        alvoPitch = (fpc != null && fpc.CinemachineCameraTarget != null) ? fpc.CinemachineCameraTarget.transform : null;
        pitch = LerPitch();

        godAplicado = true;
        Descobrir(); // garante o FirstPersonController desligado (gravidade/movimento)
        ProcessarEntradas();
        EmuseuDebug.Info(Tag, "god mode ligado em " + alvo.name + " (fpc=" + (fpc != null) + ", cc=" + ccOriginal + ")");
    }

    float LerPitch()
    {
        try
        {
            if (fpc != null && CampoPitch != null) return (float)CampoPitch.GetValue(fpc);
        }
        catch (Exception) { }
        if (alvoPitch != null)
        {
            float x = alvoPitch.localEulerAngles.x;
            return x > 180f ? x - 360f : x;
        }
        return 0f;
    }

    void RestaurarGod()
    {
        if (!godAplicado) { EsquecerAlvo(); return; }

        // devolve o pitch ao controlador (o olhar continua de onde parou)
        try { if (fpc != null && CampoPitch != null) CampoPitch.SetValue(fpc, pitch); }
        catch (Exception) { }

        if (alvo != null)
        {
            if (rb != null) rb.isKinematic = rbKinematic;
            if (cc != null)
            {
                Physics.SyncTransforms(); // a posicao mudou com o CharacterController desligado
                cc.enabled = ccOriginal;
            }
        }
        EmuseuDebug.Info(Tag, "god mode desligado: componentes restaurados");
        EsquecerAlvo();
    }

    void Voar()
    {
        if (alvo == null || cc == null) { EsquecerAlvo(); return; } // jogador destruido (troca de cena): religa na proxima cena se persistente
        if (EmuseuUploadModalGuard.Ativo) return; // modal de upload: teclado e do React

        // o RemoteBannerTour (N/B) religa o CharacterController depois de teleportar; mantem desligado
        if (cc.enabled) cc.enabled = false;

        // olhar com o mouse: mesma conta do FirstPersonController.CameraRotation
        if (fpc != null && sai != null && sai.cursorInputForLook && sai.look.sqrMagnitude >= 0.01f)
        {
            pitch = Mathf.Clamp(pitch + sai.look.y * fpc.RotationSpeed, fpc.BottomClamp, fpc.TopClamp);
            if (alvoPitch != null) alvoPitch.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            alvo.Rotate(Vector3.up * (sai.look.x * fpc.RotationSpeed));
        }

        Camera cam = Camera.main;
        Transform refer = cam != null ? cam.transform : alvo;

        float h = (AdminInput.Segurando(AdminInput.Tecla.D) ? 1f : 0f) - (AdminInput.Segurando(AdminInput.Tecla.A) ? 1f : 0f);
        float v = (AdminInput.Segurando(AdminInput.Tecla.W) ? 1f : 0f) - (AdminInput.Segurando(AdminInput.Tecla.S) ? 1f : 0f);
        float u = (AdminInput.Segurando(AdminInput.Tecla.Space) ? 1f : 0f)
                  - ((AdminInput.Segurando(AdminInput.Tecla.Ctrl) || AdminInput.Segurando(AdminInput.Tecla.Q)) ? 1f : 0f);

        Vector3 dir = refer.forward * v + refer.right * h + Vector3.up * u;
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        float vel = AdminInput.Segurando(AdminInput.Tecla.Shift) ? VelRapido : VelNormal;
        alvo.position += dir * (vel * Mathf.Min(Time.unscaledDeltaTime, 0.1f));
    }

    // ---------- utilidades ----------

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
}