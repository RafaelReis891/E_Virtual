using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cadeado com quantos dials você quiser (2, 3, 4...).
/// Cada dial gira para o próximo número quando é clicado.
/// O puzzle é resolvido quando todos os dials estão no número certo.
/// </summary>
public class CadeadoDials : MonoBehaviour
{
    [System.Serializable]
    public class Dial
    {
        [Tooltip("Objeto do disco (precisa ter Collider).")]
        public Transform disco;

        [Tooltip("Número correto deste disco (índice do array de ângulos).")]
        public int numeroCorreto;

        [Tooltip("Disparado quando este disco chega no numero correto (VFX, shader, som...).")]
        public UnityEvent OnNumeroCorreto;

        [Tooltip("Disparado quando este disco sai do numero correto.")]
        public UnityEvent OnSaiuNumeroCorreto;

        [HideInInspector] public bool estavaCorreto;

        [Tooltip("Renderers dos numeros deste disco (use o material Numeros_Emissivo). Vazio = procura no proprio disco.")]
        public Renderer[] renderersNumeros;

        [HideInInspector] public int numeroAtual;
        [HideInInspector] public Coroutine rotacao;
        [HideInInspector] public Coroutine feedback;
    }

    [Header("Dials (adicione quantos quiser)")]
    public Dial[] dials = new Dial[4];

    [Header("Configuração dos discos")]
    [Tooltip("Ângulos de rotação para os números 0 a 9.")]
    public float[] dialAngles = { 0f, 36f, 72f, 108f, 144f, 180f, 216f, 252f, 288f, 324f };

    [Tooltip("Eixo em que os discos giram.")]
    public Vector3 eixo = Vector3.up;

    [Tooltip("Ajuste entre o numero da logica e o numero que aparece de frente. Se valida com o numero escondido, mude este valor (ex.: 2, 3, -2) ate o numero da frente bater.")]
    public int ajusteNumeroVisivel = 0;

    [Tooltip("Marque se o numero da frente conta ao contrario do giro.")]
    public bool inverterContagem = false;

    [Header("Debug")]
    [Tooltip("Mostra no Console o dial e o numero a cada clique.")]
    public bool mostrarDebug = false;

    [Header("Objetos da cena")]
    public Animator doorAnimator;
    public GameObject successEffect;

    [Header("Feedback")]
    public AudioClip clickSound;
    public AudioClip successSound;
    AudioSource audioSource;

    [Header("Feedback emissivo (numeros)")]
    [Tooltip("Liga o brilho vermelho/verde nos numeros.")]
    public bool usarFeedbackEmissivo = true;
    [Tooltip("Segundos parado no numero antes de mostrar a cor.")]
    [Min(0f)] public float tempoParado = 1.2f;
    [ColorUsage(false, true)] public Color corErrado = new Color(4f, 0.15f, 0.15f);
    [ColorUsage(false, true)] public Color corCorreto = new Color(0.2f, 4f, 0.3f);
    [Tooltip("Quantas vezes pisca no numero errado.")]
    [Min(1)] public int piscadasErrado = 3;
    [Tooltip("Duracao de cada piscada (aceso + apagado).")]
    [Min(0.05f)] public float duracaoPiscada = 0.3f;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;

    bool isSolved;

    public UnityEvent OnEndGame;
    public UnityEvent OnPassNextScene;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        foreach (var d in dials)
        {
            if (d == null || d.disco == null) continue;
            d.numeroAtual = 0;
            d.disco.localRotation = Quaternion.AngleAxis(dialAngles[0], eixo);
        }
    }

    void Update()
    {
        if (isSolved) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                ProcessClick(hit.collider.transform);
        }
    }

    void ProcessClick(Transform clicked)
    {
        foreach (var d in dials)
        {
            if (d == null || d.disco == null) continue;
            if (clicked == d.disco || clicked.IsChildOf(d.disco))
            {
                PlaySound(clickSound);
                d.numeroAtual = (d.numeroAtual + 1) % dialAngles.Length;
                if (d.rotacao != null) StopCoroutine(d.rotacao);
                d.rotacao = StartCoroutine(SmoothRotate(d.disco, dialAngles[d.numeroAtual], 0.3f));
                int vis = NumeroVisivel(d);
                bool correto = vis == d.numeroCorreto;
                if (correto && !d.estavaCorreto) d.OnNumeroCorreto?.Invoke();
                else if (!correto && d.estavaCorreto) d.OnSaiuNumeroCorreto?.Invoke();
                d.estavaCorreto = correto;
                IniciarFeedback(d, correto);
                if (mostrarDebug)
                    Debug.Log($"[Cadeado] {d.disco.name}: numero {vis}" + (correto ? " -> CORRETO!" : ""), d.disco);
                CheckWinCondition();
                return;
            }
        }
    }

    int NumeroVisivel(Dial d)
    {
        int n = dialAngles.Length;
        int v = inverterContagem ? ajusteNumeroVisivel - d.numeroAtual : d.numeroAtual + ajusteNumeroVisivel;
        return ((v % n) + n) % n;
    }

    void CheckWinCondition()
    {
        foreach (var d in dials)
        {
            if (d == null || d.disco == null) continue;
            int visivel = NumeroVisivel(d);
            if (visivel != d.numeroCorreto) return;
        }
        SolvePuzzle();
    }

    void SolvePuzzle()
    {
        isSolved = true;
        if (usarFeedbackEmissivo)
            foreach (var d in dials) { if (d == null) continue; if (d.feedback != null) StopCoroutine(d.feedback); SetEmissao(d, corCorreto); }
        Debug.Log("Cadeado resolvido!");

        PlaySound(successSound);
        if (successEffect != null) successEffect.SetActive(true);
        if (doorAnimator != null) doorAnimator.SetTrigger("Open");

        Invoke(nameof(CallEndGame), 3f);
    }

    void CallEndGame()
    {
        OnEndGame?.Invoke();
        Invoke(nameof(PassNextSceneEvent), 3f);
    }

    void PassNextSceneEvent()
    {
        OnPassNextScene?.Invoke();
    }

    IEnumerator SmoothRotate(Transform disco, float angulo, float duracao)
    {
        Quaternion inicio = disco.localRotation;
        Quaternion alvo = Quaternion.AngleAxis(angulo, eixo);
        float t = 0f;
        while (t < duracao)
        {
            t += Time.deltaTime;
            disco.localRotation = Quaternion.Slerp(inicio, alvo, Mathf.Clamp01(t / duracao));
            yield return null;
        }
        disco.localRotation = alvo;
    }

    // ---------- Feedback emissivo ----------
    void IniciarFeedback(Dial d, bool correto)
    {
        if (!usarFeedbackEmissivo) return;
        if (d.feedback != null) StopCoroutine(d.feedback);
        SetEmissao(d, Color.black);
        d.feedback = StartCoroutine(FeedbackRotina(d, correto));
    }

    IEnumerator FeedbackRotina(Dial d, bool correto)
    {
        yield return new WaitForSeconds(tempoParado);
        if (correto)
        {
            SetEmissao(d, corCorreto); // fica verde
            yield break;
        }
        for (int i = 0; i < piscadasErrado; i++)
        {
            SetEmissao(d, corErrado);
            yield return new WaitForSeconds(duracaoPiscada * 0.5f);
            SetEmissao(d, Color.black);
            yield return new WaitForSeconds(duracaoPiscada * 0.5f);
        }
        d.feedback = null;
    }

    void SetEmissao(Dial d, Color cor)
    {
        if (d == null || d.disco == null) return;
        if (d.renderersNumeros == null || d.renderersNumeros.Length == 0)
            d.renderersNumeros = d.disco.GetComponentsInChildren<Renderer>();
        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (var r in d.renderersNumeros)
        {
            if (r == null) continue;
            foreach (var m in r.sharedMaterials)
                if (m != null && !m.IsKeywordEnabled("_EMISSION")) { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorId, cor);
            r.SetPropertyBlock(mpb);
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}
