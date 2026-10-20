using UnityEngine;
using System.Collections;

/// <summary>
/// Ativa o personagem com dissolve, toca o áudio e dissolve de saída.
/// Coloque este script no objeto do trigger (não no personagem).
/// </summary>
public class CharacterAppearDissolve : MonoBehaviour
{
    [Header("Personagem")]
    public GameObject character;
    public Renderer characterRenderer;

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

    AudioSource audioSource;
    Material dissolveMat;
    bool sequenceStarted = false;

    static readonly int DissolveAmount = Shader.PropertyToID("_DissolveAmount");
    static readonly int Direction = Shader.PropertyToID("_Direction");

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (character != null)
            character.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (sequenceStarted) return;
        if (!other.CompareTag("Player")) return;

        sequenceStarted = true;
        StartCoroutine(FullSequence());
    }

    IEnumerator FullSequence()
    {
        // Ativa
        if (character != null)
            character.SetActive(true);

        // Pega material
        if (characterRenderer == null && character != null)
            characterRenderer = character.GetComponentInChildren<Renderer>(true);

        if (characterRenderer != null)
            dissolveMat = characterRenderer.material;

        SetDissolve(1f);
        SetDirection();
        yield return null;

        // Dissolve entrada: 1 → 0
        if (useDissolve && dissolveMat != null)
            yield return StartCoroutine(Dissolve(1f, 0f));
        else
            SetDissolve(0f);

        // Áudio
        if (voiceClip != null && audioSource != null)
        {
            audioSource.clip = voiceClip;
            audioSource.volume = volume;
            audioSource.Play();
            yield return new WaitForSeconds(voiceClip.length);
        }

        // Objeto extra
        if (objectToActivate != null)
            objectToActivate.SetActive(true);

        // Espera extra
        if (waitAfterAudio > 0f)
            yield return new WaitForSeconds(waitAfterAudio);

        // Dissolve saída: 0 → 1
        if (useDissolve && dissolveMat != null)
            yield return StartCoroutine(Dissolve(0f, 1f));

        // Desativa
        if (character != null)
            character.SetActive(false);
    }

    IEnumerator Dissolve(float from, float to)
    {
        if (dissolveMat == null || !dissolveMat.HasProperty(DissolveAmount))
        {
            Debug.LogWarning("Material não tem _DissolveAmount.", this);
            yield break;
        }

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
}
