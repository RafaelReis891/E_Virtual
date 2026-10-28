using UnityEngine;
using System.Collections;

/// <summary>
/// Ativa o personagem com dissolve, toca o áudio e dissolve de saída.
/// Coloque este script no objeto do trigger (não no personagem).
/// O Character pode ser um prefab — o script instancia.
/// </summary>
public class CharacterAppearDissolve : MonoBehaviour
{
    [Header("Personagem")]
    [Tooltip("Prefab ou objeto na cena. Se for prefab, será instanciado.")]
    public GameObject characterPrefab;

    [Tooltip("Ponto onde o personagem aparece. Se vazio, usa a posição do prefab.")]
    public Transform spawnPoint;

    [Header("Dissolve")]
    public bool useDissolve = true;
    [Min(0f)] public float dissolveDuration = 1.5f;
    public bool dissolveFromBottom = true;

    [Header("Áudio")]
    public AudioClip voiceClip;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Objeto extra (opcional)")]
    public GameObject objectToActivate;

    [Header("Tempo após áudio")]
    [Min(0f)] public float waitAfterAudio = 0.5f;

    [Header("Debug")]
    public bool showLogs = true;

    AudioSource audioSource;
    Material dissolveMat;
    GameObject characterInstance;
    bool sequenceStarted = false;

    static readonly int DissolveAmount = Shader.PropertyToID("_Dissolve");
    static readonly int Direction = Shader.PropertyToID("_Direction");

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    void OnTriggerEnter(Collider other)
    {
        Log("Trigger enter: " + other.name + " tag: " + other.tag);

        if (sequenceStarted) return;
        if (!other.CompareTag("Player")) return;

        sequenceStarted = true;
        StartCoroutine(FullSequence());
    }

    IEnumerator FullSequence()
    {
        Log("Iniciando sequência");

        if (characterPrefab == null)
        {
            Log("Character Prefab está vazio!");
            yield break;
        }

        // Instancia o personagem
        Vector3 pos = spawnPoint != null ? spawnPoint.position : characterPrefab.transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : characterPrefab.transform.rotation;
        characterInstance = Instantiate(characterPrefab, pos, rot);
        characterInstance.SetActive(true);

        Log("Instanciado: " + characterInstance.name);

        // Pega o Renderer da instância
        Renderer rend = characterInstance.GetComponentInChildren<Renderer>(true);
        Log("Renderer: " + (rend != null ? rend.name : "NULL"));

        if (rend != null)
        {
            dissolveMat = rend.material;
            Log("Material: " + dissolveMat.name);
            Log("Tem _DissolveAmount: " + dissolveMat.HasProperty(DissolveAmount));
        }

        SetDissolve(1f);
        SetDirection();
        yield return null;

        // Dissolve entrada
        if (useDissolve && dissolveMat != null && dissolveMat.HasProperty(DissolveAmount))
        {
            Log("Dissolve entrada 1→0");
            yield return StartCoroutine(Dissolve(1f, 0f));
        }
        else
        {
            Log("Sem dissolve. Mostrando direto.");
            SetDissolve(0f);
        }

        // Áudio
        if (voiceClip != null && audioSource != null)
        {
            Log("Tocando áudio: " + voiceClip.name);
            audioSource.clip = voiceClip;
            audioSource.volume = volume;
            audioSource.Play();
            yield return new WaitForSeconds(voiceClip.length);
        }

        if (objectToActivate != null)
            objectToActivate.SetActive(true);

        if (waitAfterAudio > 0f)
            yield return new WaitForSeconds(waitAfterAudio);

        // Dissolve saída
        if (useDissolve && dissolveMat != null && dissolveMat.HasProperty(DissolveAmount))
        {
            Log("Dissolve saída 0→1");
            yield return StartCoroutine(Dissolve(0f, 1f));
        }

        Log("Destruindo instância");
        if (characterInstance != null)
            Destroy(characterInstance);
    }

    IEnumerator Dissolve(float from, float to)
    {
        if (dissolveMat == null || !dissolveMat.HasProperty(DissolveAmount))
            yield break;

        if (dissolveDuration <= 0f)
        {
            SetDissolve(to);
            yield break;
        }

        float elapsed = 0f;
        SetDissolve(from);

        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dissolveDuration);
            SetDissolve(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetDissolve(to);
        Log("Dissolve terminou em " + to);
    }

    void SetDissolve(float value)
    {
        if (dissolveMat != null && dissolveMat.HasProperty(DissolveAmount))
            dissolveMat.SetFloat(DissolveAmount, value);
    }

    void SetDirection()
    {
        if (dissolveMat != null && dissolveMat.HasProperty(Direction))
            dissolveMat.SetFloat(Direction, dissolveFromBottom ? 1f : -1f);
    }

    void Log(string msg)
    {
        if (showLogs)
            Debug.Log("[Dissolve] " + msg, this);
    }
}
