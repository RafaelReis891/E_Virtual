using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Piloto automatico do player para gravar a cinematica institucional (so nas cenas listadas em Cenas).
//
// Sequencia:
//  1. Espera o fim da intro (PlayerCapsule ativo e camera da intro desligada) + "esperaInicial".
//  2. Anda sozinho ate o centro do campo (feixe azul: AntiGravityTrigger) -> o feixe levanta o player
//     e o Teleport.cs leva para o HUB (Teletransporter.localTarget), como no jogo normal.
//  3. No HUB: espera "esperaNoHub", da uma volta completa em velocidade constante (com entrada/saida suaves)
//     e termina ja mirando o portal do Rio (Trigger_Teleport_RJ, que gira junto com os portais).
//  4. Anda ate o portal do Rio; o TriggerEvent do portal faz o fade e carrega a CenaRiodeJaneiro normalmente.
//
// Durante a sequencia o controle do jogador fica travado (teclado/mouse nao mexem) e o cursor fica escondido.
// Teclas: F6 = iniciar/reiniciar (no campo comeca andando ate o centro; no HUB comeca pela volta). F7 = parar.
//
// Cria-se sozinho (DontDestroyOnLoad), sem Inspector, sem editar cena/prefab e sem mexer nos scripts originais.
// Ajustes: mude os valores padrao abaixo, ou com o jogo rodando selecione "CinematicaAutoPiloto" (DontDestroyOnLoad)
// no Hierarchy e mexa no Inspector (vale so ate parar o Play).
// Objetos vazios OPCIONAIS (se existirem na cena, tem prioridade sobre a deteccao automatica):
//  - "Cine_Centro"    : ponto no chao onde o player para no centro do campo.
//  - "Cine_PortalRio" : ponto para onde o player mira/anda no HUB (atencao: os portais giram; um vazio parado nao gira junto).
// Logs com prefixo [CineAuto].
[DefaultExecutionOrder(-50)] // roda antes do FirstPersonController (move/rotacao aplicados no mesmo frame)
public sealed class CinematicaAutoPiloto : MonoBehaviour
{
    // nomes das cenas (sem .unity) onde o piloto automatico existe
    public static readonly string[] Cenas = { "CenaArena_TESTECINE" };

    [Header("Inicio")]
    [Tooltip("Comeca sozinho quando a intro termina. Desligado = so com F6.")]
    public bool autoIniciar = true;
    [Tooltip("Segundos depois que o player aparece (fim da intro) ate comecar a andar.")]
    public float esperaInicial = 2.5f;
    [Tooltip("Esconde e trava o cursor do mouse nesta cena (exceto com o painel de admin aberto).")]
    public bool esconderCursorNaCena = true;

    [Header("Caminhada")]
    [Tooltip("Velocidade de caminhada durante a cinematica (m/s). O jogo normal usa 6.")]
    public float velocidadeCaminhada = 3f;
    [Tooltip("Segundos para sair do parado ate a velocidade cheia.")]
    public float tempoAceleracao = 1.2f;
    [Tooltip("Distancia (m) em que comeca a frear ao chegar no centro.")]
    public float distanciaFreio = 2.5f;
    [Tooltip("Distancia (m) do centro em que considera que chegou.")]
    public float toleranciaCentro = 0.3f;
    [Tooltip("Suavidade para virar na direcao do alvo enquanto anda (s). Maior = vira mais devagar.")]
    public float suavidadeDirecao = 0.6f;

    [Header("HUB - volta completa")]
    [Tooltip("Segundos parado no HUB antes de comecar a girar.")]
    public float esperaNoHub = 1.5f;
    [Tooltip("Segundos para uma volta de 360 graus na velocidade constante.")]
    public float segundosPorVolta = 12f;
    [Tooltip("Segundos de aceleracao no inicio e de desaceleracao no fim do giro (entrada/saida suaves).")]
    public float tempoSuavizar = 2f;
    [Tooltip("Quantas voltas completas antes de parar mirando o Rio (1 = 360 graus + o que faltar ate o Rio).")]
    public int voltas = 1;
    [Tooltip("Ligado = gira para o lado que termina mais perto do Rio. Desligado = usa sentidoHorario.")]
    public bool escolherLadoMaisCurto = true;
    [Tooltip("Sentido do giro quando escolherLadoMaisCurto esta desligado (true = para a direita).")]
    public bool sentidoHorario = true;
    [Tooltip("Nivela a inclinacao da camera (olhar para cima/baixo) durante o giro.")]
    public bool ajustarPitch = true;
    [Tooltip("Inclinacao da camera no HUB (graus). 0 = horizonte; negativo = olha para cima.")]
    public float pitchNoHub = 0f;
    [Tooltip("Segundos parado mirando o Rio antes de andar ate o portal.")]
    public float esperaAntesDeAndar = 0.6f;

    [Header("Seguranca")]
    public float tempoMaxCentro = 40f;
    public float tempoMaxTeleporte = 15f;
    public float tempoMaxPortal = 30f;

    [Header("Nomes")]
    public string nomePlayer = "PlayerCapsule";
    public string nomeCentroOpcional = "Cine_Centro";
    public string nomePortalOpcional = "Cine_PortalRio";
    public string nomeTriggerPortalRio = "Trigger_Teleport_RJ";
    public string nomeCameraIntro = "CameraView (1)";

    enum Fase { Desligado, AguardandoPlayer, EsperaInicial, IndoAoCentro, AguardandoTeleporte, EsperaHub, Girando, Mirando, IndoAoPortal, Fim }

    static CinematicaAutoPiloto instancia;
    static readonly FieldInfo CampoPitch = typeof(StarterAssets.FirstPersonController)
        .GetField("_cinemachineTargetPitch", BindingFlags.Instance | BindingFlags.NonPublic);

    bool cenaAtiva;
    Fase fase = Fase.Desligado;
    float tFase;

    // referencias
    StarterAssets.FirstPersonController fpc;
    StarterAssets.StarterAssetsInputs sai;
    AdminPlayerControl admin;
    Transform centroOpcional, portalOpcional, triggerRio, alvoTeleporte;
    Collider colCentro, colRio;

    // estado salvo para devolver ao terminar
    bool travado;
    float moveSpeedOriginal;
    bool analogOriginal, lookOriginal;

    // movimento
    float yawVel;
    Vector3 posAnterior;
    bool temPosAnterior;
    float tempoPerto;

    // giro
    float yaw0, dir, extra0, rumoAcum, rumoAnterior, rumoAcum0, duracao, suave, pitch0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { instancia = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (instancia != null) return;
        var go = new GameObject("CinematicaAutoPiloto");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<CinematicaAutoPiloto>();
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

    static bool EhCenaCinematica(string nome)
    {
        for (int i = 0; i < Cenas.Length; i++)
            if (string.Equals(Cenas[i], nome, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    void Aplicar(Scene cena)
    {
        // objetos da cena anterior ja foram destruidos: nao restaura nada neles
        travado = false;
        fpc = null; sai = null; admin = null;
        centroOpcional = portalOpcional = triggerRio = alvoTeleporte = null;
        colCentro = colRio = null;

        if (fase == Fase.IndoAoPortal) Debug.Log("[CineAuto] nova cena carregada (" + cena.name + ") - sequencia concluida");
        cenaAtiva = EhCenaCinematica(cena.name);
        if (!cenaAtiva) { MudarFase(Fase.Desligado, null); return; }

        Debug.Log("[CineAuto] ativo em " + cena.name + " (F6 inicia/reinicia, F7 para; autoIniciar=" + autoIniciar + ")");
        MudarFase(autoIniciar ? Fase.AguardandoPlayer : Fase.Desligado, autoIniciar ? "aguardando o fim da intro" : "aguardando F6");
    }

    // ---------------- ciclo ----------------
    void Update()
    {
        if (!cenaAtiva) return;
        tFase += Time.deltaTime;

        if (fpc == null) Buscar();
        if (admin == null) admin = FindAnyObjectByType<AdminPlayerControl>();
        bool painelAberto = admin != null && admin.PainelAberto;

        if (esconderCursorNaCena && !painelAberto && !StarterAssets.StarterAssetsInputs.GamePaused)
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            if (Cursor.visible) Cursor.visible = false;
        }

        if (TeclaF7() && fase != Fase.Desligado) { Parar("F7"); return; }
        if (TeclaF6()) { ComecarPorTecla(); }

        if (fase == Fase.Desligado || fase == Fase.Fim) return;

        // pausa do site / painel de admin / god mode: nao mexe em nada ate voltar
        if (painelAberto || StarterAssets.StarterAssetsInputs.GamePaused) return;

        switch (fase)
        {
            case Fase.AguardandoPlayer: AtualizarAguardandoPlayer(); break;
            case Fase.EsperaInicial: if (!PlayerOk()) return; Travar(); if (tFase >= esperaInicial) MudarFase(Fase.IndoAoCentro, "andando ate o centro do campo"); break;
            case Fase.IndoAoCentro: if (!PlayerOk()) return; Travar(); AtualizarIndoAoCentro(); break;
            case Fase.AguardandoTeleporte: if (!PlayerOk()) return; Travar(); AtualizarAguardandoTeleporte(); break;
            case Fase.EsperaHub: if (!PlayerOk()) return; Travar(); AtualizarEsperaHub(); break;
            case Fase.Girando: if (!PlayerOk()) return; Travar(); AtualizarGirando(); break;
            case Fase.Mirando: if (!PlayerOk()) return; Travar(); AtualizarMirando(); break;
            case Fase.IndoAoPortal: if (!PlayerOk()) return; Travar(); AtualizarIndoAoPortal(); break;
        }
    }

    void AtualizarAguardandoPlayer()
    {
        if (!PlayerOk()) return;
        GameObject camIntro = Achar(nomeCameraIntro);
        if (camIntro != null && camIntro.activeInHierarchy) return;
        MudarFase(Fase.EsperaInicial, "fim da intro detectado - comeca em " + esperaInicial.ToString("0.0") + "s");
    }

    void AtualizarIndoAoCentro()
    {
        Vector3 alvo;
        if (!PosicaoCentro(out alvo)) { Parar("nao achei o centro (AntiGravityTrigger/Teletransporter/Cine_Centro)"); return; }

        float dist = DistXZ(fpc.transform.position, alvo);
        if (fpc.IsAntiGravityActive || dist <= toleranciaCentro)
        {
            sai.move = Vector2.zero;
            MudarFase(Fase.AguardandoTeleporte, fpc.IsAntiGravityActive ? "feixe ativou - subindo" : "chegou no centro - aguardando o feixe/teleporte");
            return;
        }
        float erro = VirarPara(alvo, suavidadeDirecao);
        float mag = Rampa(tFase, tempoAceleracao) * Mathf.Clamp(dist / Mathf.Max(0.01f, distanciaFreio), 0.2f, 1f) * Mathf.Clamp01(1f - Mathf.Abs(erro) / 60f);
        sai.move = new Vector2(0f, mag);

        if (tFase > tempoMaxCentro) Parar("tempo maximo indo ao centro (algo bloqueando o caminho?)");
    }

    void AtualizarAguardandoTeleporte()
    {
        sai.move = Vector2.zero;
        Vector3 p = fpc.transform.position;
        bool pulou = temPosAnterior && (p - posAnterior).sqrMagnitude > 25f; // > 5 m num frame = teleporte
        bool pertoDoAlvo = alvoTeleporte != null && (p - alvoTeleporte.position).sqrMagnitude < 6.25f;
        if (pulou || pertoDoAlvo)
        {
            MudarFase(Fase.EsperaHub, "teleporte para o HUB detectado");
            return;
        }
        if (tFase > tempoMaxTeleporte) Parar("teleporte nao aconteceu em " + tempoMaxTeleporte + "s");
    }

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
        // o portal gira junto com os outros (PortaisGirar): acompanha o rumo ao vivo para terminar exatamente nele
        float rumo = RumoPara(PosicaoPortalRio());
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

    void LateUpdate()
    {
        if (!cenaAtiva || fpc == null) return;
        posAnterior = fpc.transform.position;
        temPosAnterior = true;
    }

    // ---------------- comandos ----------------
    void ComecarPorTecla()
    {
        Buscar();
        if (!PlayerOk()) { Debug.LogWarning("[CineAuto] F6: player ainda nao esta ativo (intro rodando?)"); return; }
        Destravar();
        if (NoHub()) MudarFase(Fase.EsperaHub, "F6 - player no HUB: comecando pela volta completa");
        else MudarFase(Fase.IndoAoCentro, "F6 - andando ate o centro do campo");
    }

    void Parar(string motivo)
    {
        Destravar();
        MudarFase(Fase.Desligado, "parado: " + motivo + " (controle devolvido; F6 recomeca)");
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
        if (!string.IsNullOrEmpty(log)) Debug.Log("[CineAuto] " + log);
    }

    // ---------------- travar/destravar o jogador ----------------
    void Travar()
    {
        if (sai == null || fpc == null) return;
        if (!travado)
        {
            travado = true;
            moveSpeedOriginal = fpc.MoveSpeed;
            analogOriginal = sai.analogMovement;
            lookOriginal = sai.cursorInputForLook;
            fpc.MoveSpeed = velocidadeCaminhada;
            sai.analogMovement = true; // permite acelerar/frear suave pela intensidade do "move"
        }
        // reforco a cada frame: o PlayerInput continua enviando OnMove/OnLook
        sai.cursorInputForLook = false; // OnLook passa a mandar zero
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
    void Buscar()
    {
        StarterAssets.FirstPersonController melhor = null;
        foreach (StarterAssets.FirstPersonController c in FindObjectsByType<StarterAssets.FirstPersonController>(FindObjectsInactive.Include))
        {
            if (c.gameObject.scene != SceneManager.GetActiveScene()) continue;
            if (melhor == null || c.name == nomePlayer) melhor = c;
            if (c.name == nomePlayer) break;
        }
        fpc = melhor;
        sai = fpc != null ? fpc.GetComponent<StarterAssets.StarterAssetsInputs>() : null;

        GameObject go = Achar(nomeCentroOpcional);
        centroOpcional = go != null ? go.transform : null;
        go = Achar(nomePortalOpcional);
        portalOpcional = go != null ? go.transform : null;

        colCentro = null;
        AntiGravityTrigger ag = FindAnyObjectByType<AntiGravityTrigger>(FindObjectsInactive.Include);
        if (ag != null) colCentro = ag.GetComponent<Collider>();
        Teletransporter tp = FindAnyObjectByType<Teletransporter>(FindObjectsInactive.Include);
        if (tp != null)
        {
            alvoTeleporte = tp.localTarget;
            if (colCentro == null) colCentro = tp.GetComponent<Collider>();
        }
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
        if (fpc == null || sai == null) Buscar();
        return fpc != null && sai != null && fpc.isActiveAndEnabled && sai.isActiveAndEnabled;
    }

    bool NoHub()
    {
        if (alvoTeleporte == null) Buscar();
        return alvoTeleporte != null && Mathf.Abs(fpc.transform.position.y - alvoTeleporte.position.y) < 4f;
    }

    bool PosicaoCentro(out Vector3 p)
    {
        if (centroOpcional != null) { p = centroOpcional.position; return true; }
        if (colCentro != null)
        {
            // collider desligado tem bounds zerado: usa o transform
            p = colCentro.enabled && colCentro.gameObject.activeInHierarchy ? colCentro.bounds.center : colCentro.transform.position;
            return true;
        }
        p = Vector3.zero;
        return false;
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

    // vira suave na direcao do alvo; devolve o erro de angulo (graus) antes de virar
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
        if (CampoPitch != null) CampoPitch.SetValue(fpc, pitch); // mantem o FirstPersonController coerente
        if (fpc.CinemachineCameraTarget != null) fpc.CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // area acumulada de uma velocidade normalizada: sobe em "suave" s (curva cosseno), fica 1 (constante), desce em "suave" s.
    // Perfil(duracao) = duracao - suave.
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

    // 0 -> 1 suave (cosseno) em "tempo" segundos
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
