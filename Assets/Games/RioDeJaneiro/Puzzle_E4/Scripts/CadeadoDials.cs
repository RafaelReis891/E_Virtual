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

        [HideInInspector] public int numeroAtual;
        [HideInInspector] public Coroutine rotacao;
    }

    [Header("Dials (adicione quantos quiser)")]
    public Dial[] dials = new Dial[4];

    [Header("Configuração dos discos")]
    [Tooltip("Ângulos de rotação para os números 0 a 9.")]
    public float[] dialAngles = { 0f, 36f, 72f, 108f, 144f, 180f, 216f, 252f, 288f, 324f };

    [Tooltip("Eixo em que os discos giram.")]
    public Vector3 eixo = Vector3.up;

    [Header("Objetos da cena")]
    public Animator doorAnimator;
    public GameObject successEffect;

    [Header("Feedback")]
    public AudioClip clickSound;
    public AudioClip successSound;
    AudioSource audioSource;

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
                CheckWinCondition();
                return;
            }
        }
    }

    void CheckWinCondition()
    {
        foreach (var d in dials)
        {
            if (d == null || d.disco == null) continue;
            if (d.numeroAtual != d.numeroCorreto) return;
        }
        SolvePuzzle();
    }

    void SolvePuzzle()
    {
        isSolved = true;
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

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}