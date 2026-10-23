using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Coloque no Prefab_FinalCard (que tem o SphereCollider com Is Trigger).
// Fora do trigger: board pequeno (25%) e girando (RotateObject ligado).
// Dentro: anima para 100%, desliga o rotator e vira o board para o jogador.
[RequireComponent(typeof(Collider))]
public class FinalCardProximidade : MonoBehaviour
{
    [Tooltip("O que escala e gira. Use um filho (ex: Visual) para o trigger nao encolher junto.")]
    public Transform visual;

    [Tooltip("Componente que faz girar (ex: RotateObject)")]
    public Behaviour rotator;

    public string tagJogador = "Player";

    [Header("Escala")]
    [Range(0.05f, 1f)] public float escalaPequena = 0.25f;
    public float duracaoAnimacao = 0.5f;
    public AnimationCurve curva = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Mirar no jogador")]
    public bool mirarNoJogador = true;
    public bool somenteEixoY = true;
    public float velocidadeMira = 6f;
    [Tooltip("Ligue se o board ficar de costas ao mirar")]
    public bool inverterFrente = false;

    [Header("Eventos")]
    public UnityEvent OnAproximou;
    public UnityEvent OnAfastou;

    [Header("Debug")]
    public bool mostrarDebug;

    Vector3 escalaOriginal;
    Transform jogador;
    Coroutine anim;
    SphereCollider esfera;
    float raioOriginal;

    void Awake()
    {
        if (visual == null) visual = transform;
        escalaOriginal = visual.localScale;
        esfera = GetComponent<SphereCollider>();
        if (esfera != null) raioOriginal = esfera.radius;
        visual.localScale = escalaOriginal * escalaPequena;
        CompensarRaio();
        GetComponent<Collider>().isTrigger = true;
        if (visual == transform)
            Log("visual = proprio objeto: o raio do trigger sera compensado automaticamente");
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagJogador)) return;
        jogador = other.transform;
        if (rotator != null) rotator.enabled = false;
        Animar(1f);
        Log("jogador entrou");
        OnAproximou.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagJogador)) return;
        jogador = null;
        Animar(escalaPequena);
        if (rotator != null) rotator.enabled = true;
        Log("jogador saiu");
        OnAfastou.Invoke();
    }

    void Update()
    {
        if (!mirarNoJogador || jogador == null) return;
        Vector3 dir = jogador.position - visual.position;
        if (somenteEixoY) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        if (inverterFrente) dir = -dir;
        Quaternion alvo = Quaternion.LookRotation(dir);
        visual.rotation = Quaternion.Slerp(visual.rotation, alvo, Time.deltaTime * velocidadeMira);
    }

    void Animar(float fator)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(AnimarEscala(escalaOriginal * fator));
    }

    IEnumerator AnimarEscala(Vector3 destino)
    {
        Vector3 inicio = visual.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duracaoAnimacao);
            visual.localScale = Vector3.LerpUnclamped(inicio, destino, curva.Evaluate(Mathf.Clamp01(t)));
            CompensarRaio();
            yield return null;
        }
        visual.localScale = destino;
        CompensarRaio();
    }

    // Mantem o tamanho real do trigger igual, mesmo com o objeto encolhido.
    void CompensarRaio()
    {
        if (esfera == null || visual != transform || escalaOriginal.x == 0f) return;
        float fator = visual.localScale.x / escalaOriginal.x;
        if (fator > 0.0001f) esfera.radius = raioOriginal / fator;
    }

    void Log(string m) { if (mostrarDebug) Debug.Log("[FinalCardProx] " + name + ": " + m, this); }
}
