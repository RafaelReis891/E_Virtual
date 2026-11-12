using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Cinematica v2 (so nas cenas listadas em Cenas, ex.: CenaArena_TESTECINE_v2).
//
// Sequencia:
//  1. A animacao da camera da intro toca (por padrao "CameraView (1)" com Animator; tambem aceita PlayableDirector/Timeline
//     ou Animation). O PlayerCapsule fica DESLIGADO na cena.
//  2. Fim da animacao:
//     - se o fluxo normal da cena desligar a camera da intro (IntroCameraHold -> Trigger_FadeinWhite -> fade branco),
//       o player e ligado no mesmo frame (o corte fica escondido pelo fade);
//     - se a animacao terminou e a camera continua ligada depois de "esperaFluxoNormal" s, este script faz a troca:
//       liga o player e desliga a camera da intro.
//     Seguranca: "tempoMaximoAnimacao" s depois do inicio a troca e feita de qualquer jeito.
//  3. Liga o PlayerCapsule (e os pais desligados, ex.: Player_Things). Se ele estiver longe do HUB, leva para
//     "Cine_SpawnHub" (se existir) ou para o destino do teleporte (Teletransporter.localTarget).
//  4. HUB: espera "esperaNoHub", da uma volta completa em velocidade constante (entrada/saida suaves),
//     termina mirando o portal do Rio (Trigger_Teleport_RJ, que gira junto com os portais) e anda ate ele.
//     O TriggerEvent do portal faz o fade e carrega a CenaRiodeJaneiro normalmente.
//
// Controle do jogador travado e cursor escondido durante a sequencia.
// Teclas: F6 = reinicia a volta do HUB (se a animacao ainda estiver tocando, pula direto para o player). F7 = parar.
// Cria-se sozinho (DontDestroyOnLoad), sem Inspector, sem editar cena/prefab e sem mexer nos scripts originais.
// Nao roda nas cenas do CinematicaAutoPiloto (v1) e vice-versa.
// Logs com prefixo [CineAutoV2].
[DefaultExecutionOrder(-50)] // roda antes do FirstPersonController (move/rotacao aplicados no mesmo frame)
public sealed class CinematicaAutoPilotoV2 : MonoBehaviour
{
    // nomes das cenas (sem .unity) onde esta cinematica existe
    public static readonly string[] Cenas = { "CenaArena_TESTECINE_v2" };

    [Header("Animacao da intro")]
    [Tooltip("Nome do objeto com a animacao (Animator, PlayableDirector ou Animation). Vazio = detecta sozinho.")]
    public string nomeObjetoAnimacao = "CameraView (1)";
    [Tooltip("Depois que a animacao termina, espera este tempo (s) para o fluxo normal da cena (fade branco) desligar a camera. "
             + "Se nao desligar, este script faz a troca direto.")]
    public float esperaFluxoNormal = 3f;
    [Tooltip("Desligado = troca direto no fim da animacao (corte seco, sem esperar o fade da cena).")]
    public bool usarFadeDaCena = true;
    [Tooltip("Seguranca: segundos depois do inicio da cena em que a troca e feita de qualquer jeito. 0 = sem limite.")]
    public float tempoMaximoAnimacao = 90f;

    [Header("Player")]
    public string nomePlayer = "PlayerCapsule";
    [Tooltip("Ao ligar, vira o player para a mesma direcao (e inclinacao) em que a camera da animacao terminou.")]
    public bool alinharComCameraDaAnimacao = true;
    [Tooltip("Se o player estiver longe do HUB ao ligar, leva para Cine_SpawnHub / destino do teleporte.")]
    public bool levarParaHubSeLonge = true;
    [Tooltip("Diferenca de altura (m) acima da qual o player e considerado fora do HUB.")]
    public float alturaToleranciaHub = 6f;
    public string nomeSpawnHubOpcional = "Cine_SpawnHub";
    [Tooltip("Esconde e trava o cursor do mouse nesta cena (exceto com o painel de admin aberto).")]
    public bool esconderCursorNaCena = true;

    [Header("HUB - volta completa")]
    [Tooltip("Segundos depois de ligar o player antes de comecar a girar.")]
    public float esperaNoHub = 1.5f;
    [Tooltip("Segundos para uma volta de 360 graus na velocidade constante.")]
    public float segundosPorVolta = 12f;
    [Tooltip("Segundos de aceleracao no inicio e de desaceleracao no fim do giro.")]
    public float tempoSuavizar = 2f;
    [Tooltip("Quantas voltas completas antes de parar mirando o Rio (1 = 360 graus + o que faltar ate o Rio).")]
    public int voltas = 1;
    [Tooltip("Ligado = gira para o lado que termina mais perto do Rio. Desligado = usa sentidoHorario.")]
    public bool escolherLadoMaisCurto = true;
    public bool sentidoHorario = true;
    [Tooltip("Ajusta a inclinacao da camera (olhar para cima/baixo) durante o giro.")]
    public bool ajustarPitch = true;
    [Tooltip("Inclinacao da camera no HUB (graus). 0 = horizonte; negativo = olha para cima.")]
    public float pitchNoHub = 0f;
    [Tooltip("Segundos parado mirando o Rio antes de andar.")]
    public float esperaAntesDeAndar = 0.6f;

    [Header("Caminhada ate o portal")]
    [Tooltip("Velocidade de caminhada (m/s). O jogo normal usa 6.")]
    public float velocidadeCaminhada = 3f;
    public float tempoAceleracao = 1.2f;
    [Tooltip("Suavidade para virar na direcao do portal enquanto anda (s).")]
    public float suavidadeDirecao = 0.6f;
    public float tempoMaxPortal = 30f;
    public string nomeTriggerPortalRio = "Trigger_Teleport_RJ";
    public string nomePortalOpcional = "Cine_PortalRio";

    enum Fase { Desligado, AguardandoAnimacao, EsperaHub, Girando, Mirando, IndoAoPortal, Fim }

    static CinematicaAutoPilotoV2 instancia;
    static readonly FieldInfo CampoPitch = typeof(StarterAssets.FirstPersonController)
        .GetField("_cinemachineTargetPitch", BindingFlags.Instance | BindingFlags.NonPublic);

    bool cenaAtiva;
    Fase fase = Fase.Desligado;
    float tFase, tCena;

    // animacao
    GameObject animObj;
    PlayableDirector diretor;
    Animator animador;
    Animation animacao;
    bool animComecou, animTerminou;
    float tFimAnim = -1f;
    Vector3 camPos; Quaternion camRot; bool temCam;

    // player
    StarterAssets.FirstPersonController fpc;
    StarterAssets.StarterAssetsInputs sai;
    GameObject playerGo;
    AdminPlayerControl admin;
    Transform portalOpcional, triggerRio, alvoTeleporte;
    Collider colRio;
    bool travado;
    float moveSpeedOriginal;
    bool analogOriginal, lookOriginal;

    float yawVel, tempoPerto;
    float yaw0, dir, extra0, rumoAcum, rumoAnterior, rumoAcum0, duracao, suave, pitch0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { instancia = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (instancia != null) return;
        var go = new GameObject("CinematicaAutoPilotoV2");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<CinematicaAutoPilotoV2>();
    }

    void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;
        SceneManager.sceneLoaded += AoCarregarCena;
        Aplicar(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        Destravar();
        if (instancia == this) instancia = null;
    }

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (modo == LoadSceneMode.Single) Aplicar(cena);
    }

    static bool EhCena(string nome)
    {
        for (int i = 0; i < Cenas.Length; i++)
            if (string.Equals(Cenas[i], nome, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    void Aplicar(Scene cena)
    {
        travado = false;
        fpc = null; sai = null; playerGo = null; admin = null;
        portalOpcional = triggerRio = alvoTeleporte = null; colRio = null;
        animObj = null; diretor = null; animador = null; animacao = null;
        animComecou = animTerminou = false; tFimAnim = -1f; temCam = false;
        tCena = 0f;

        if (fase == Fase.IndoAoPortal) Debug.Log("[CineAutoV2] nova cena carregada (" + cena.name + ") - sequencia concluida");
        cenaAtiva = EhCena(cena.name);
        if (!cenaAtiva) { MudarFase(Fase.Desligado, null); return; }

        BuscarPlayer();
        BuscarAnimacao();
        Debug.Log("[CineAutoV2] ativo em " + cena.name + " | animacao: " + DescreverAnimacao()
            + " | player: " + (playerGo != null ? playerGo.name + (playerGo.activeInHierarchy ? " (ja ligado)" : " (desligado)") : "NAO ACHADO")
            + " | F6 reinicia a volta do HUB, F7 para");
        MudarFase(Fase.AguardandoAnimacao, "aguardando o fim da animacao");
    }

    // ---------------- ciclo ----------------
    void Update()
    {
        if (!cenaAtiva) return;
        tFase += Time.deltaTime;
        tCena += Time.deltaTime;

        if (admin == null) admin = FindAnyObjectByType<AdminPlayerControl>();
        bool painelAberto = admin != null && admin.PainelAberto;

        if (esconderCursorNaCena && !painelAberto && !StarterAssets.StarterAssetsInputs.GamePaused)
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            if (Cursor.visible) Cursor.visible = false;
        }

        if (TeclaF7() && fase != Fase.Desligado) { Parar("F7"); return; }
        if (TeclaF6()) ComecarPorTecla();

        if (fase == Fase.Desligado || fase == Fase.Fim) return;
        if (painelAberto || StarterAssets.StarterAssetsInputs.GamePaused) return;

        switch (fase)
        {
            case Fase.AguardandoAnimacao: AtualizarAnimacao(); break;
            case Fase.EsperaHub: if (!PlayerOk()) return; Travar(); AtualizarEsperaHub(); break;
            case Fase.Girando: if (!PlayerOk()) return; Travar(); AtualizarGirando(); break;
            case Fase.Mirando: if (!PlayerOk()) return; Travar(); AtualizarMirando(); break;
            case Fase.IndoAoPortal: if (!PlayerOk()) return; Travar(); AtualizarIndoAoPortal(); break;
        }
    }

    // ---------------- fim da animacao ----------------
    void AtualizarAnimacao()
    {
        if (animObj == null) BuscarAnimacao();

        // guarda a ultima pose da camera da animacao (para alinhar o player)
        if (animObj != null && animObj.activeInHierarchy)
        {
            Transform ct = animObj.transform;
            Camera cam = animObj.GetComponentInChildren<Camera>();
            if (cam != null) ct = cam.transform;
            camPos = ct.position; camRot = ct.rotation; temCam = true;
        }

        // 1) o fluxo normal da cena desligou a camera da intro -> troca agora (o fade esconde o corte)
        if (animComecou && (animObj == null || !animObj.activeInHierarchy))
        {
            Trocar("camera da animacao desligada pela cena");
            return;
        }
        if (animObj != null && animObj.activeInHierarchy) animComecou = true;

        // 2) a animacao terminou
        if (!animTerminou && animComecou && AnimacaoTerminou())
        {
            animTerminou = true;
            tFimAnim = tCena;
            Debug.Log("[CineAutoV2] animacao terminou em " + tCena.ToString("0.0") + "s"
                + (usarFadeDaCena ? " - aguardando ate " + esperaFluxoNormal.ToString("0.0") + "s pelo fade da cena" : " - troca direta"));
            if (!usarFadeDaCena) { Trocar("fim da animacao (troca direta)"); return; }
        }
        if (animTerminou && tCena - tFimAnim >= esperaFluxoNormal) { Trocar("fim da animacao + " + esperaFluxoNormal.ToString("0.0") + "s sem o fade da cena"); return; }

        // 3) seguranca
        if (tempoMaximoAnimacao > 0f && tCena >= tempoMaximoAnimacao) { Trocar("tempo maximo da animacao (" + tempoMaximoAnimacao + "s)"); return; }

        // sem animacao tocando (nao achada, ou camera ja desligada no inicio - ex.: intro pulada pelo PularIntroArena)
        if (!animComecou && tCena > 1.5f)
        {
            if (animObj == null) Trocar("nenhuma animacao encontrada");
            else if (!animObj.activeInHierarchy) Trocar("camera da animacao desligada desde o inicio (intro pulada?)");
        }
    }

    bool AnimacaoTerminou()
    {
        if (diretor != null)
        {
            if (diretor.state == PlayState.Playing && diretor.duration > 0 && diretor.time >= diretor.duration - 0.02) return true;
            if (diretor.state != PlayState.Playing && diretor.time > 0) return true;
            if (diretor.state != PlayState.Playing && tCena > 1f && diretor.playOnAwake) return true;
            return false;
        }
        if (animador != null && animador.isActiveAndEnabled && animador.runtimeAnimatorController != null)
        {
            if (animador.IsInTransition(0)) return false;
            AnimatorStateInfo st = animador.GetCurrentAnimatorStateInfo(0);
            if (st.loop) return false;
            return st.normalizedTime >= 1f || animador.speed == 0f && tCena > 1f; // speed 0 = IntroCameraHold congelou no fim
        }
        if (animacao != null) return !animacao.isPlaying && tCena > 0.5f;
        return false;
    }

    void Trocar(string motivo)
    {
        Debug.Log("[CineAutoV2] troca para o player: " + motivo);
        BuscarPlayer();
        if (playerGo == null) { Parar("nao achei o " + nomePlayer); return; }

        // liga os pais desligados (ex.: Player_Things) e o proprio player
        Transform t = playerGo.transform.parent;
        while (t != null)
        {
            if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); Debug.Log("[CineAutoV2] ligando " + t.name); }
            t = t.parent;
        }
        if (!playerGo.activeSelf) playerGo.SetActive(true);

        // a camera da intro sai de cena (se a cena ja nao desligou)
        if (animObj != null && animObj.activeSelf) { animObj.SetActive(false); Debug.Log("[CineAutoV2] desligando a camera da animacao " + animObj.name); }

        BuscarPlayer();
        if (fpc == null || sai == null) { Parar("player sem FirstPersonController/StarterAssetsInputs"); return; }

        LevarParaHubSePreciso();
        if (alinharComCameraDaAnimacao && temCam)
        {
            Vector3 f = camRot * Vector3.forward;
            if (f.x * f.x + f.z * f.z > 1e-4f) AplicarYaw(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, camRot.eulerAngles.x), fpc.BottomClamp, fpc.TopClamp);
            AplicarPitch(pitch);
        }
        MudarFase(Fase.EsperaHub, "player ligado em " + fpc.transform.position.ToString("F2") + " - volta no HUB em " + esperaNoHub.ToString("0.0") + "s");
    }

    void LevarParaHubSePreciso()
    {
        Vector3 p = fpc.transform.position;
        GameObject spawn = Achar(nomeSpawnHubOpcional);
        Transform destino = spawn != null ? spawn.transform : alvoTeleporte;
        if (destino == null) { Debug.Log("[CineAutoV2] sem destino do HUB para conferir (fica onde esta: " + p.ToString("F2") + ")"); return; }

        float dy = Mathf.Abs(p.y - destino.position.y);
        bool forcar = spawn != null; // se Teco criou o spawn, usa sempre
        if (!forcar && (!levarParaHubSeLonge || dy <= alturaToleranciaHub))
        {
            Debug.Log("[CineAutoV2] player ja esta no HUB (" + p.ToString("F2") + ", destino do teleporte " + destino.position.ToString("F2") + ")");
            return;
        }
        CharacterController cc = fpc.GetComponent<CharacterController>();
        bool ccLigado = cc != null && cc.enabled;
        if (ccLigado) cc.enabled = false;
        fpc.transform.position = destino.position;
        if (ccLigado) cc.enabled = true;
        Debug.Log("[CineAutoV2] player levado de " + p.ToString("F2") + " para " + destino.name + " " + destino.position.ToString("F2"));
    }

    // ---------------- HUB ----------------
    void AtualizarEsperaHub()
    {
        sai.move = Vector2.zero;
        if (tFase < esperaNoHub) return;
        if (!fpc.Grounded && tFase < esperaNoHub + 4f) return; // espera pousar
        if (!AcharPortalRio()) { Parar("nao achei o portal do Rio (" + nomeTriggerPortalRio + " / " + nomePortalOpcional + ")"); return; }
        ComecarGiro();
    }

    void ComecarGiro()
    {
        yaw0 = fpc.transform.eulerAngles.y;
        float rumo = RumoPara(PosicaoPortalRio());
        float delta = Mathf.DeltaAngle(yaw0, rumo);
        dir = sentidoHorario ? 1f : -1f;
        if (escolherLadoMaisCurto && Mathf.Abs(delta) > 0.01f) dir = delta > 0f ? 1f : -1f;
        extra0 = Mathf.Repeat(dir * (rumo - yaw0), 360f);
        rumoAnterior = rumo;
        rumoAcum = rumoAcum0 = rumo;

        float total = 360f * Mathf.Max(0, voltas) + extra0;
        float vmax = 360f / Mathf.Max(0.5f, segundosPorVolta);
        suave = Mathf.Clamp(tempoSuavizar, 0f, total / vmax);
        duracao = Mathf.Max(0.1f, total / vmax + suave);

        pitch0 = LerPitch();
        MudarFase(Fase.Girando, "girando " + total.ToString("0") + " graus para a " + (dir > 0 ? "direita" : "esquerda")
            + " em " + duracao.ToString("0.0") + "s (" + vmax.ToString("0.0") + " graus/s constante, suavizar " + suave.ToString("0.0") + "s)");
    }

    void AtualizarGirando()
    {
        sai.move = Vector2.zero;
        float rumo = RumoPara(PosicaoPortalRio()); // o portal gira junto com os outros: acompanha ao vivo
        rumoAcum += Mathf.DeltaAngle(rumoAnterior, rumo);
        rumoAnterior = rumo;
        float extra = extra0 + dir * (rumoAcum - rumoAcum0);
        float total = 360f * Mathf.Max(0, voltas) + extra;

        float t = Mathf.Min(tFase, duracao);
        float s = Perfil(t) / Mathf.Max(0.0001f, duracao - suave);
        AplicarYaw(yaw0 + dir * total * s);

        if (ajustarPitch)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tFase / Mathf.Max(0.5f, suave * 1.5f)));
            AplicarPitch(Mathf.Lerp(pitch0, pitchNoHub, k));
        }

        if (tFase >= duracao) MudarFase(Fase.Mirando, "volta completa - mirando o Rio");
    }

    void AtualizarMirando()
    {
        sai.move = Vector2.zero;
        VirarPara(PosicaoPortalRio(), 0.25f);
        if (tFase >= esperaAntesDeAndar) MudarFase(Fase.IndoAoPortal, "andando ate o portal do Rio");
    }

    void AtualizarIndoAoPortal()
    {
        Vector3 alvo = PosicaoPortalRio();
        float erro = VirarPara(alvo, suavidadeDirecao);
        float mag = Rampa(tFase, tempoAceleracao) * Mathf.Clamp01(1f - Mathf.Abs(erro) / 75f);
        sai.move = new Vector2(0f, Mathf.Max(0.05f, mag));

        float dist = DistXZ(fpc.transform.position, alvo);
        tempoPerto = dist < 0.5f ? tempoPerto + Time.deltaTime : 0f;
        if (tempoPerto > 2f) { Concluir("chegou no portal mas a cena nao trocou (trigger nao disparou?)"); return; }
        if (tFase > tempoMaxPortal) Concluir("tempo maximo indo ao portal");
    }

    // ---------------- comandos ----------------
    void ComecarPorTecla()
    {
        BuscarPlayer();
        if (playerGo == null || !playerGo.activeInHierarchy || fase == Fase.AguardandoAnimacao)
        {
            Trocar("F6 - pulando a animacao");
            return;
        }
        Destravar();
        MudarFase(Fase.EsperaHub, "F6 - reiniciando a volta do HUB");
    }

    void Parar(string motivo)
    {
        Destravar();
        MudarFase(Fase.Desligado, "parado: " + motivo + " (controle devolvido; F6 recomeca a volta do HUB)");
    }

    void Concluir(string motivo)
    {
        Destravar();
        MudarFase(Fase.Fim, motivo);
    }

    void MudarFase(Fase nova, string log)
    {
        fase = nova;
        tFase = 0f;
        yawVel = 0f;
        tempoPerto = 0f;
        if (!string.IsNullOrEmpty(log)) Debug.Log("[CineAutoV2] " + log);
    }

    // ---------------- travar/destravar ----------------
    void Travar()
    {
        if (sai == null || fpc == null) return;
        if (!travado)
        {
            travado = true;
            moveSpeedOriginal = fpc.MoveSpeed;
            analogOriginal = sai.analogMovement;
            lookOriginal = sai.cursorInputForLook;
            sai.analogMovement = true;
        }
        sai.cursorInputForLook = false; // OnLook do PlayerInput passa a mandar zero
        sai.look = Vector2.zero;
        sai.jump = false;
        sai.sprint = false;
        sai.move = Vector2.zero; // cada fase escreve o move dela logo depois
        fpc.MoveSpeed = velocidadeCaminhada;
    }

    void Destravar()
    {
        if (!travado) return;
        travado = false;
        if (fpc != null) fpc.MoveSpeed = moveSpeedOriginal;
        if (sai != null)
        {
            sai.move = Vector2.zero;
            sai.look = Vector2.zero;
            sai.analogMovement = analogOriginal;
            sai.cursorInputForLook = lookOriginal;
        }
    }

    // ---------------- busca ----------------
    void BuscarPlayer()
    {
        StarterAssets.FirstPersonController melhor = null;
        foreach (StarterAssets.FirstPersonController c in FindObjectsByType<StarterAssets.FirstPersonController>(FindObjectsInactive.Include))
        {
            if (c.gameObject.scene != SceneManager.GetActiveScene()) continue;
            if (melhor == null || c.name == nomePlayer) melhor = c;
            if (c.name == nomePlayer) break;
        }
        fpc = melhor;
        playerGo = fpc != null ? fpc.gameObject : Achar(nomePlayer);
        sai = playerGo != null ? playerGo.GetComponent<StarterAssets.StarterAssetsInputs>() : null;

        GameObject go = Achar(nomePortalOpcional);
        portalOpcional = go != null ? go.transform : null;
        Teletransporter tp = FindAnyObjectByType<Teletransporter>(FindObjectsInactive.Include);
        alvoTeleporte = tp != null ? tp.localTarget : null;
    }

    void BuscarAnimacao()
    {
        animObj = null; diretor = null; animador = null; animacao = null;
        if (!string.IsNullOrEmpty(nomeObjetoAnimacao)) animObj = Achar(nomeObjetoAnimacao);

        if (animObj == null)
        {
            // auto: 1) PlayableDirector ativo  2) camera ativa com Animator  3) camera ativa com Animation
            foreach (PlayableDirector d in FindObjectsByType<PlayableDirector>(FindObjectsInactive.Exclude))
                if (d.gameObject.scene == SceneManager.GetActiveScene()) { animObj = d.gameObject; break; }
            if (animObj == null)
                foreach (Camera c in FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                {
                    if (c.gameObject.scene != SceneManager.GetActiveScene()) continue;
                    if (playerGo != null && c.transform.IsChildOf(playerGo.transform.root)) continue; // camera do player
                    if (c.GetComponentInParent<Animator>() != null || c.GetComponentInParent<Animation>() != null) { animObj = c.gameObject; break; }
                }
        }
        if (animObj == null) return;
        diretor = animObj.GetComponentInParent<PlayableDirector>();
        animador = animObj.GetComponentInParent<Animator>();
        animacao = animObj.GetComponentInParent<Animation>();
    }

    string DescreverAnimacao()
    {
        if (animObj == null) return "NAO ACHADA (troca por tempo)";
        string tipo = diretor != null ? "PlayableDirector (" + diretor.duration.ToString("0.0") + "s)"
            : animador != null ? "Animator" + (animador.runtimeAnimatorController != null ? " " + animador.runtimeAnimatorController.name : "")
            : animacao != null ? "Animation" : "sem componente de animacao (espera a camera desligar)";
        if (animador != null && animador.runtimeAnimatorController != null)
        {
            var clips = animador.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0 && clips[0] != null) tipo += ", clip " + clips[0].name + " " + clips[0].length.ToString("0.0") + "s";
        }
        return animObj.name + " - " + tipo;
    }

    bool AcharPortalRio()
    {
        if (portalOpcional != null) return true;
        if (triggerRio == null)
        {
            GameObject go = Achar(nomeTriggerPortalRio);
            if (go != null) { triggerRio = go.transform; colRio = go.GetComponent<Collider>(); }
        }
        return triggerRio != null;
    }

    bool PlayerOk()
    {
        if (fpc == null || sai == null) BuscarPlayer();
        return fpc != null && sai != null && fpc.isActiveAndEnabled && sai.isActiveAndEnabled;
    }

    Vector3 PosicaoPortalRio()
    {
        if (portalOpcional != null) return portalOpcional.position;
        if (colRio != null && colRio.enabled && colRio.gameObject.activeInHierarchy) return colRio.bounds.center;
        if (triggerRio != null) return triggerRio.position;
        return fpc.transform.position + fpc.transform.forward;
    }

    GameObject Achar(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t.gameObject;
        return null;
    }

    // ---------------- rotacao ----------------
    float RumoPara(Vector3 alvo)
    {
        Vector3 d = alvo - fpc.transform.position;
        if (d.x * d.x + d.z * d.z < 1e-6f) return fpc.transform.eulerAngles.y;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    float VirarPara(Vector3 alvo, float tempo)
    {
        float atual = fpc.transform.eulerAngles.y;
        float rumo = RumoPara(alvo);
        float erro = Mathf.DeltaAngle(atual, rumo);
        AplicarYaw(Mathf.SmoothDampAngle(atual, rumo, ref yawVel, Mathf.Max(0.01f, tempo)));
        return erro;
    }

    void AplicarYaw(float yaw)
    {
        fpc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    float LerPitch()
    {
        if (CampoPitch != null) return (float)CampoPitch.GetValue(fpc);
        if (fpc.CinemachineCameraTarget != null) return Mathf.DeltaAngle(0f, fpc.CinemachineCameraTarget.transform.localEulerAngles.x);
        return 0f;
    }

    void AplicarPitch(float pitch)
    {
        if (CampoPitch != null) CampoPitch.SetValue(fpc, pitch);
        if (fpc.CinemachineCameraTarget != null) fpc.CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // velocidade normalizada: sobe em "suave" s (cosseno), constante, desce em "suave" s. Perfil(duracao) = duracao - suave.
    float Perfil(float t)
    {
        float e = suave;
        if (e <= 0.0001f) return t;
        if (t <= e) return Rampa01(t, e);
        if (t <= duracao - e) return e * 0.5f + (t - e);
        return (duracao - e) - Rampa01(duracao - t, e);
    }

    static float Rampa01(float u, float e)
    {
        return u * 0.5f - e / (2f * Mathf.PI) * Mathf.Sin(Mathf.PI * u / e);
    }

    static float Rampa(float t, float tempo)
    {
        if (tempo <= 0.0001f) return 1f;
        float x = Mathf.Clamp01(t / tempo);
        return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * x);
    }

    static float DistXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // ---------------- teclas ----------------
    static bool TeclaF6()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.f6Key.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F6)) return true;
#endif
        return false;
    }

    static bool TeclaF7()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.f7Key.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F7)) return true;
#endif
        return false;
    }
}
