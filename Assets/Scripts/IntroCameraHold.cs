using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Michsky.UI.Shift;

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
//  2. Espera a camera PARAR (detectarParada) ou o clip terminar (normalizedTime >= 1), ou o tempo "tempoFimOverride" (se > 0).
//  3. Congela a camera (animator.speed = 0) e, se "olharParaDisco" estiver ligado, gira suavemente para o disco.
//  4. Espera "holdSeconds", liga o Trigger_FadeinWhite (se estiver inativo na cena) e invoca o _OnTriggerEnter UMA vez
//     (FadeIn + TimedEvent 1 s). Se em timer+0,5 s a camera da intro ainda estiver ligada, executa o fim direto
//     (timerAction do TimedEvent, ou por nome: Player_Things, FadeOut, StartAnfitrian_1, desliga FadeIn/trigger/camera).
// Aborta se a intro foi pulada (PularIntroArena), se a camera da intro for desligada ou se o trigger disparar sozinho.
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

    [Header("Detectar a camera parada (fim real do trajeto)")]
    [Tooltip("Ligado = o fim do trajeto e quando a camera PARA de se mover (o clip fica ~4 s quase parado no final). "
             + "Desligado = espera o clip terminar (normalizedTime >= 1).")]
    public bool detectarParada = true;

    [Tooltip("Abaixo desta velocidade (m/s) a camera conta como parada.")]
    public float velocidadeParada = 0.15f;

    [Tooltip("Abaixo deste giro (graus/s) a camera conta como parada.")]
    public float giroParada = 2f;

    [Tooltip("Por quantos segundos seguidos ela precisa ficar parada para confirmar.")]
    public float tempoConfirmaParada = 0.3f;

    [Tooltip("So procura a parada depois deste tempo de intro (s), para nao confundir com o comeco.")]
    public float tempoMinimoParada = 15f;

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
    GameObject triggerGo;     // pode estar INATIVO na cena (normal) - nao e motivo para abortar
    TriggerEvent triggerEvent;
    TimedEvent timedFim;      // o TimedEvent (1 s) que faz Player_Things / FadeOut / desliga camera...
    Transform disco;
    Scene cena;

    bool congelado;
    Quaternion rotCongelada;

    void Start()
    {
        cena = gameObject.scene;
        if (Cancelada(cena)) { Encerrar("intro pulada"); return; }
        if (atual != null && atual != this) { Encerrar("ja existe outra instancia"); return; }
        atual = this;

        cameraGo = AcharCamera(cena, out animator);
        if (cameraGo == null) { Encerrar("nenhuma camera de intro ativa (CameraView com Animator " + NomeController + ")"); return; }
        cameraCol = cameraGo.GetComponent<Collider>();

        triggerGo = Achar(cena, NomeTrigger);
        if (triggerGo != null)
        {
            triggerEvent = triggerGo.GetComponent<TriggerEvent>();
            timedFim = AcharTimedFim(triggerGo);
            if (triggerEvent != null)
            {
                if (triggerEvent._OnTriggerEnter == null) triggerEvent._OnTriggerEnter = new ColliderUnityEvent();
                triggerEvent._OnTriggerEnter.AddListener(AoTriggerEnter);
            }
        }

        GameObject d = Achar(cena, nomeDisco);
        disco = d != null ? d.transform : null;

        Debug.Log("[IntroCameraHold] ativo (" + (criadoAutomatico ? "automatico, valores padrao do script" : "componente da cena em " + gameObject.name) + "): camera=" + cameraGo.name
                  + ", holdSeconds=" + holdSeconds.ToString("0.00") + "s, tempoFimOverride=" + tempoFimOverride.ToString("0.00")
                  + "s, fim=" + (tempoFimOverride > 0 ? tempoFimOverride.ToString("0.0") + "s" : (detectarParada ? "camera parada (<" + velocidadeParada + " m/s por " + tempoConfirmaParada + "s, depois de " + tempoMinimoParada + "s) ou fim do clip" : "fim do clip"))
                  + ", olharParaDisco=" + olharParaDisco + (disco == null ? " (disco nao encontrado)" : "")
                  + ", trigger=" + (triggerGo == null ? "NAO ENCONTRADO (usa fallback)" : (triggerGo.activeSelf ? "ativo" : "inativo (sera ligado no fim)"))
                  + ", TriggerEvent=" + (triggerEvent != null) + ", TimedEvent fim=" + (timedFim != null ? timedFim.timer.ToString("0.0") + "s" : "nao achado"));
        StartCoroutine(Rodar());
    }

    // O TimedEvent do trigger com mais chamadas no timerAction (o de 1 s: Player_Things, FadeOut, etc.)
    static TimedEvent AcharTimedFim(GameObject go)
    {
        TimedEvent melhor = null;
        int maior = 0;
        foreach (TimedEvent te in go.GetComponents<TimedEvent>())
        {
            int n = te.timerAction != null ? te.timerAction.GetPersistentEventCount() : 0;
            if (n > maior) { maior = n; melhor = te; }
        }
        return melhor;
    }

    IEnumerator Rodar()
    {
        float inicio = Time.time;
        Vector3 posAnt = cameraGo.transform.position;
        Quaternion rotAnt = cameraGo.transform.rotation;
        float paradaDesde = -1f;
        string motivoFim = "fim do clip";

        // 1. espera o fim do trajeto
        while (true)
        {
            if (!Valido()) yield break;
            if (disparou) { Encerrar("trigger disparou sozinho - nada a segurar"); yield break; }

            float t = Time.time - inicio;
            float dt = Time.deltaTime;
            Vector3 pos = cameraGo.transform.position;
            Quaternion rot = cameraGo.transform.rotation;
            float vel = dt > 0f ? Vector3.Distance(pos, posAnt) / dt : 0f;
            float giro = dt > 0f ? Quaternion.Angle(rot, rotAnt) / dt : 0f;
            posAnt = pos; rotAnt = rot;

            if (tempoFimOverride > 0f)
            {
                if (t >= tempoFimOverride) { motivoFim = "tempoFimOverride"; break; }
            }
            else if (detectarParada && t >= tempoMinimoParada && dt > 0f && vel < velocidadeParada && giro < giroParada)
            {
                if (paradaDesde < 0f) paradaDesde = t;
                if (t - paradaDesde >= tempoConfirmaParada) { motivoFim = "camera parou (desde t=" + paradaDesde.ToString("0.00") + "s)"; break; }
            }
            else
            {
                if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                {
                    AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
                    if (!animator.IsInTransition(0) && st.normalizedTime >= 1f) { motivoFim = "fim do clip"; break; }
                }
                else break; // sem animator: nao ha o que esperar
            }
            if (tempoMaximo > 0f && t >= tempoMaximo) { motivoFim = "tempoMaximo"; Debug.LogWarning("[IntroCameraHold] tempoMaximo atingido (" + tempoMaximo + "s)"); break; }
            if (detectarParada && paradaDesde >= 0f && (vel >= velocidadeParada || giro >= giroParada)) paradaDesde = -1f;
            yield return null;
        }

        // 2. congela
        if (animator != null) animator.speed = 0f;
        rotCongelada = cameraGo.transform.rotation;
        congelado = true;
        Debug.Log("[IntroCameraHold] fim do trajeto em t=" + (Time.time - inicio).ToString("0.00") + "s (" + motivoFim + ") - segurando " + holdSeconds.ToString("0.00") + "s");

        // 3. segura
        float fimHold = Time.time + Mathf.Max(0f, holdSeconds);
        while (Time.time < fimHold)
        {
            if (!Valido()) yield break;
            if (disparou) { Encerrar("trigger disparou sozinho durante a parada"); yield break; }
            yield return null;
        }
        if (!Valido() || disparou) yield break;
        Debug.Log("[IntroCameraHold] fade disparado em t=" + (Time.time - inicio).ToString("0.00") + "s");

        // 4. dispara o fim da intro: primeiro pelos eventos existentes do Trigger_FadeinWhite
        float espera = DispararEventos();

        // 5. confere: se a cadeia do TimedEvent nao rodou (camera ainda ligada), faz direto
        float limite = Time.time + espera + 0.5f;
        while (Time.time < limite)
        {
            if (abortado) yield break;
            if (cameraGo == null || !cameraGo.activeInHierarchy) { Encerrar("concluido pela cadeia do " + NomeTrigger); yield break; }
            yield return null;
        }
        if (abortado) yield break;
        Debug.LogWarning("[IntroCameraHold] a cadeia do TimedEvent nao rodou em " + espera.ToString("0.0") + "s - executando o fim direto (fallback)");
        FimDireto();
        Encerrar("concluido pelo fallback");
    }

    // Liga o trigger (se estiver inativo) e invoca o _OnTriggerEnter, igual a fisica faria.
    // Retorna quantos segundos esperar pela cadeia (timer do TimedEvent).
    float DispararEventos()
    {
        nosDisparamos = true;
        disparou = true;
        float timer = timedFim != null ? Mathf.Max(0f, timedFim.timer) : 1f;

        if (triggerGo != null && !triggerGo.activeSelf)
        {
            Debug.Log("[IntroCameraHold] " + NomeTrigger + " estava inativo - ligando para rodar os eventos");
            triggerGo.SetActive(true);
        }

        if (triggerEvent != null && triggerGo != null && triggerGo.activeInHierarchy)
        {
            if (triggerEvent.playOnce)
            {
                FieldInfo f = typeof(TriggerEvent).GetField("hasTriggered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) f.SetValue(triggerEvent, true);
            }
            Debug.Log("[IntroCameraHold] disparando fim da intro (" + NomeTrigger + "._OnTriggerEnter)");
            try { triggerEvent._OnTriggerEnter.Invoke(cameraCol); }
            catch (System.Exception e) { Debug.LogException(e); }

            // garante o TimedEvent de 1 s contando (caso o evento nao o tenha ligado)
            if (timedFim != null && !timedFim.enabled)
            {
                Debug.Log("[IntroCameraHold] ligando o TimedEvent de " + timer.ToString("0.0") + "s");
                timedFim.enabled = true; // OnEnable -> conta o timer
            }
        }
        else
        {
            Debug.LogWarning("[IntroCameraHold] sem TriggerEvent utilizavel - ligando FadeIn direto");
        }

        // o FadeIn tem que aparecer de qualquer jeito
        GameObject fadeIn = Achar(cena, "FadeIn");
        if (fadeIn != null && !fadeIn.activeSelf) fadeIn.SetActive(true);
        return timer;
    }

    // Mesmo resultado do timerAction do TimedEvent de 1 s
    void FimDireto()
    {
        if (timedFim != null && timedFim.timerAction != null && timedFim.timerAction.GetPersistentEventCount() > 0)
        {
            try { timedFim.timerAction.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }
        }
        else
        {
            Ligar("Player_Things", true);
            Ligar(NomeTrigger, false);
            Ligar("FadeIn", false);
            Ligar("FadeOut", true);
            Ligar("StartAnfitrian_1", true);
        }
        if (cameraGo != null && cameraGo.activeSelf) cameraGo.SetActive(false);
    }

    void Ligar(string nome, bool ativo)
    {
        GameObject go = Achar(cena, nome);
        if (go != null) go.SetActive(ativo);
        else Debug.LogWarning("[IntroCameraHold] nao achei " + nome);
    }

    void LateUpdate()
    {
        // roda depois do Animator: mantem a pose congelada / gira para o disco
        if (!congelado || cameraGo == null || !cameraGo.activeInHierarchy) return;
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

    // Trigger inativo NAO e motivo para abortar (ele fica desligado na cena ate o fim da intro).
    bool Valido()
    {
        if (abortado) return false;
        if (Cancelada(cena)) { Encerrar("intro pulada"); return false; }
        if (cameraGo == null || !cameraGo.activeInHierarchy) { Encerrar("camera da intro desligada"); return false; }
        return true;
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
