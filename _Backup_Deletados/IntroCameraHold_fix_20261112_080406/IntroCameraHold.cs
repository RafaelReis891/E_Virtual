using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// Segura a camera da intro da CenaArena parada no fim do trajeto (olhando a logo do Disco_Vortice.001)
// por alguns segundos e SO DEPOIS dispara o fim da intro (o mesmo evento do Trigger_FadeinWhite:
// FadeIn -> TimedEvent 1 s -> Player_Things / FadeOut / desliga camera e trigger).
//
// Sem editar cena: cria-se sozinho no sceneLoaded da "CenaArena" (igual ao IntroArenaGarantirFim).
// Se voce colocar este componente manualmente em algum objeto da cena, o automatico nao e criado
// e valem os valores do Inspector.
//
// Fluxo:
//  1. Acha a camera da intro: "CameraView (1)" ativa; senao qualquer objeto ativo "CameraView*" com Animator IntroCamAnimation.
//  2. Espera o clip terminar (normalizedTime >= 1) ou o tempo "tempoFimOverride" (se > 0).
//  3. Congela a camera (animator.speed = 0) e, se "olharParaDisco" estiver ligado, gira suavemente para o disco.
//  4. Espera "holdSeconds" e invoca TriggerEvent._OnTriggerEnter do Trigger_FadeinWhite UMA vez.
// Aborta se a intro foi pulada (PularIntroArena), se a camera/trigger forem desligados ou se o trigger disparar sozinho.
// Logs com prefixo [IntroCameraHold].
public sealed class IntroCameraHold : MonoBehaviour
{
    const string NomeCena = "CenaArena";
    const string NomeCameraPreferida = "CameraView (1)";
    const string PrefixoCamera = "CameraView";
    const string NomeController = "IntroCamAnimation";
    const string NomeTrigger = "Trigger_FadeinWhite";

    [Header("Parada no fim da intro")]
    [Tooltip("Segundos parado olhando a logo antes do fade.")]
    public float holdSeconds = 3f;

    [Tooltip("Se > 0: segundos desde o inicio da intro em que a camera para (em vez de esperar o fim do clip). 0 = fim do clip.")]
    public float tempoFimOverride = 0f;

    [Tooltip("Gira a camera suavemente para o disco durante a parada. Desligado = mantem o enquadramento da animacao.")]
    public bool olharParaDisco = false;

    public string nomeDisco = "Disco_Vortice.001";

    [Tooltip("Velocidade do giro suave (quanto maior, mais rapido).")]
    public float velocidadeOlhar = 2f;

    [Tooltip("Seguranca: se o Animator nao informar o fim, dispara apos este tempo (s). 0 = sem limite.")]
    public float tempoMaximo = 60f;

    // ---------- instalacao automatica ----------
    static bool assinado;
    static Scene cenaCancelada;         // cena em que a intro foi pulada
    static bool temCancelada;
    static IntroCameraHold atual;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { assinado = false; temCancelada = false; cenaCancelada = default(Scene); atual = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Assinar()
    {
        if (assinado) return;
        assinado = true;
        SceneManager.sceneLoaded += AoCarregarCena;
        SceneManager.sceneUnloaded += AoDescarregarCena;
    }

    static void AoDescarregarCena(Scene cena)
    {
        if (temCancelada && cena == cenaCancelada) { temCancelada = false; cenaCancelada = default(Scene); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void VerificarCenaAtiva()
    {
        Scene s = SceneManager.GetActiveScene();
        if (s.IsValid() && s.isLoaded) AoCarregarCena(s, LoadSceneMode.Single);
    }

    static bool Cancelada(Scene cena) { return temCancelada && cena == cenaCancelada; }

    /// <summary>Chamado pelo PularIntroArena: cancela a parada nesta cena.</summary>
    public static void Cancelar(Scene cena)
    {
        cenaCancelada = cena;
        temCancelada = true;
        if (atual != null && atual.gameObject.scene == cena)
        {
            Debug.Log("[IntroCameraHold] cancelado (intro pulada)");
            atual.abortado = true;
            if (atual.criadoAutomatico) Destroy(atual.gameObject); else Destroy(atual);
            atual = null;
        }
    }

    static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (cena.name != NomeCena) return;
        if (Cancelada(cena)) { Debug.Log("[IntroCameraHold] intro pulada nesta cena - nada a fazer"); return; }

        // ja existe (manual na cena ou criado antes)
        foreach (GameObject root in cena.GetRootGameObjects())
            if (root.GetComponentInChildren<IntroCameraHold>(true) != null) return;

        var go = new GameObject("IntroCameraHold");
        SceneManager.MoveGameObjectToScene(go, cena);
        var comp = go.AddComponent<IntroCameraHold>();
        comp.criadoAutomatico = true;
    }

    // ---------- instancia ----------
    bool criadoAutomatico;
    bool abortado;
    bool disparou;            // _OnTriggerEnter ja foi invocado (por nos ou pela fisica)
    bool nosDisparamos;

    Animator animator;
    GameObject cameraGo;
    Collider cameraCol;
    TriggerEvent triggerEvent;
    Transform disco;

    bool congelado;
    Quaternion rotCongelada;

    void Start()
    {
        if (Cancelada(gameObject.scene)) { Encerrar("intro pulada"); return; }
        if (atual != null && atual != this) { Encerrar("ja existe outra instancia"); return; }
        atual = this;

        Scene cena = gameObject.scene;
        cameraGo = AcharCamera(cena, out animator);
        if (cameraGo == null) { Encerrar("nenhuma camera de intro ativa (CameraView com Animator " + NomeController + ")"); return; }

        GameObject trigGo = Achar(cena, NomeTrigger);
        if (trigGo == null || !trigGo.activeInHierarchy) { Encerrar(NomeTrigger + " ausente ou inativo"); return; }
        triggerEvent = trigGo.GetComponent<TriggerEvent>();
        if (triggerEvent == null) { Encerrar(NomeTrigger + " sem TriggerEvent"); return; }

        cameraCol = cameraGo.GetComponent<Collider>();
        GameObject d = Achar(cena, nomeDisco);
        disco = d != null ? d.transform : null;

        if (triggerEvent._OnTriggerEnter == null) triggerEvent._OnTriggerEnter = new ColliderUnityEvent();
        triggerEvent._OnTriggerEnter.AddListener(AoTriggerEnter);

        Debug.Log("[IntroCameraHold] ativo: camera=" + cameraGo.name + ", hold=" + holdSeconds.ToString("0.0") + "s"
                  + ", fim=" + (tempoFimOverride > 0 ? tempoFimOverride.ToString("0.0") + "s" : "fim do clip")
                  + ", olharParaDisco=" + olharParaDisco + (disco == null ? " (disco nao encontrado)" : ""));
        StartCoroutine(Rodar());
    }

    IEnumerator Rodar()
    {
        float inicio = Time.time;

        // 1. espera o fim do trajeto
        while (true)
        {
            if (!Valido()) yield break;
            if (disparou) { Encerrar("trigger disparou sozinho - nada a segurar"); yield break; }

            float t = Time.time - inicio;
            if (tempoFimOverride > 0f)
            {
                if (t >= tempoFimOverride) break;
            }
            else
            {
                if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                {
                    AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
                    if (!animator.IsInTransition(0) && st.normalizedTime >= 1f) break;
                }
                else break; // sem animator: nao ha o que esperar
            }
            if (tempoMaximo > 0f && t >= tempoMaximo) { Debug.LogWarning("[IntroCameraHold] tempoMaximo atingido (" + tempoMaximo + "s)"); break; }
            yield return null;
        }

        // 2. congela
        if (animator != null) animator.speed = 0f;
        rotCongelada = cameraGo.transform.rotation;
        congelado = true;
        Debug.Log("[IntroCameraHold] fim do trajeto (t=" + (Time.time - inicio).ToString("0.00") + "s) - segurando " + holdSeconds.ToString("0.0") + "s");

        // 3. segura
        float fimHold = Time.time + Mathf.Max(0f, holdSeconds);
        while (Time.time < fimHold)
        {
            if (!Valido()) yield break;
            if (disparou) { Encerrar("trigger disparou sozinho durante a parada"); yield break; }
            yield return null;
        }

        // 4. dispara o fim da intro (mesmo efeito do OnTriggerEnter)
        if (!Valido() || disparou) yield break;
        Disparar();
    }

    void LateUpdate()
    {
        // roda depois do Animator: mantem a pose congelada / gira para o disco
        if (!congelado || cameraGo == null) return;
        if (olharParaDisco && disco != null)
        {
            Vector3 dir = disco.position - cameraGo.transform.position;
            if (dir.sqrMagnitude > 1e-6f)
            {
                Quaternion alvo = Quaternion.LookRotation(dir, cameraGo.transform.up);
                rotCongelada = Quaternion.Slerp(rotCongelada, alvo, 1f - Mathf.Exp(-velocidadeOlhar * Time.deltaTime));
            }
        }
        cameraGo.transform.rotation = rotCongelada;
    }

    bool Valido()
    {
        if (abortado) return false;
        if (Cancelada(gameObject.scene)) { Encerrar("intro pulada"); return false; }
        if (cameraGo == null || !cameraGo.activeInHierarchy) { Encerrar("camera da intro desligada"); return false; }
        if (triggerEvent == null || !triggerEvent.gameObject.activeInHierarchy) { Encerrar(NomeTrigger + " desligado"); return false; }
        return true;
    }

    void Disparar()
    {
        nosDisparamos = true;
        // respeita playOnce: marca o campo privado para a fisica nao repetir
        if (triggerEvent.playOnce)
        {
            FieldInfo f = typeof(TriggerEvent).GetField("hasTriggered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(triggerEvent, true);
        }
        Debug.Log("[IntroCameraHold] disparando fim da intro (" + NomeTrigger + "._OnTriggerEnter)");
        triggerEvent._OnTriggerEnter.Invoke(cameraCol);
        Encerrar("concluido");
    }

    void AoTriggerEnter(Collider c)
    {
        if (disparou) return;
        disparou = true;
        if (!nosDisparamos) Debug.Log("[IntroCameraHold] " + NomeTrigger + " disparou pela fisica");
    }

    void Encerrar(string motivo)
    {
        if (abortado) return;
        abortado = true;
        congelado = false;
        Debug.Log("[IntroCameraHold] encerrando: " + motivo);
        if (atual == this) atual = null;
        if (criadoAutomatico) Destroy(gameObject); else enabled = false;
    }

    void OnDestroy()
    {
        if (triggerEvent != null && triggerEvent._OnTriggerEnter != null) triggerEvent._OnTriggerEnter.RemoveListener(AoTriggerEnter);
        if (atual == this) atual = null;
    }

    // ---------- busca ----------
    public static bool EhCameraIntro(GameObject go)
    {
        if (go == null || !go.name.StartsWith(PrefixoCamera)) return false;
        Animator a = go.GetComponent<Animator>();
        return a != null && a.runtimeAnimatorController != null && a.runtimeAnimatorController.name == NomeController;
    }

    static GameObject AcharCamera(Scene cena, out Animator anim)
    {
        anim = null;
        GameObject pref = Achar(cena, NomeCameraPreferida);
        if (pref != null && pref.activeInHierarchy)
        {
            anim = pref.GetComponent<Animator>();
            return pref;
        }
        foreach (GameObject root in cena.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.gameObject.activeInHierarchy && EhCameraIntro(t.gameObject))
                {
                    anim = t.GetComponent<Animator>();
                    return t.gameObject;
                }
        return null;
    }

    static GameObject Achar(Scene cena, string nome)
    {
        foreach (GameObject root in cena.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t.gameObject;
        return null;
    }
}
