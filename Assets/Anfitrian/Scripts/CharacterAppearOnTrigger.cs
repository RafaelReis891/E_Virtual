using UnityEngine;
using System.Collections;

public class CharacterAppearWithDissolve : MonoBehaviour
{
    [Header("Personagem")]
    public GameObject character;
    public Renderer characterRenderer;

    [Header("Dissolve")]
    public bool useDissolve = true;
    public Material dissolveMaterial;
    public Material originalMaterial;
    public float dissolveDuration = 1.5f;

    [Tooltip("Marque para o dissolve começar de baixo para cima. Desmarque para começar de cima para baixo.")]
    public bool dissolveFromBottom = true;

    [Header("Áudio")]
    public AudioClip voiceClip;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Objeto extra (opcional)")]
    public GameObject objectToActivate;

    [Header("Tempo")]
    public float waitAfterAudio = 2f;

    [Header("Field of View")]
    public Camera playerCamera;
    [Range(0f, 0.2f)] public float viewportMargin = 0.05f;

    private AudioSource audioSource;
    private bool sequenceStarted = false;
    private bool waitingToHide = false;

    private static readonly int DissolveAmount = Shader.PropertyToID("_DissolveAmount");
    private static readonly int Direction = Shader.PropertyToID("_Direction");

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (character != null)
            character.SetActive(false);

        if (characterRenderer == null && character != null)
            characterRenderer = character.GetComponentInChildren<Renderer>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (sequenceStarted) return;

        if (other.CompareTag("Player"))
        {
            sequenceStarted = true;
            StartCoroutine(FullSequence());
        }
    }

    IEnumerator FullSequence()
    {
        if (character != null)
            character.SetActive(true);

        // Dissolve de entrada (só se estiver ativado)
        if (useDissolve && characterRenderer != null && dissolveMaterial != null)
        {
            characterRenderer.material = dissolveMaterial;
            yield return Dissolve(1f, 0f);
        }

        // Troca para a textura original
        if (characterRenderer != null && originalMaterial != null)
            characterRenderer.material = originalMaterial;

        // Toca o áudio
        if (voiceClip != null)
        {
            audioSource.clip = voiceClip;
            audioSource.volume = volume;
            audioSource.Play();
            yield return new WaitForSeconds(voiceClip.length);
        }

        // Ativa objeto extra
        if (objectToActivate != null)
            objectToActivate.SetActive(true);

        // Espera
        yield return new WaitForSeconds(waitAfterAudio);

        // Começa a monitorar FOV
        waitingToHide = true;
    }

    void Update()
    {
        if (!waitingToHide || character == null || !character.activeSelf)
            return;

        if (IsOutOfView())
        {
            // Saiu da visão → só desativa (sem dissolve)
            character.SetActive(false);
            waitingToHide = false;
        }
    }

    IEnumerator Dissolve(float from, float to)
    {
        float dir = dissolveFromBottom ? 1f : -1f;
        dissolveMaterial.SetFloat(Direction, dir);

        float t = 0f;
        while (t < dissolveDuration)
        {
            t += Time.deltaTime;
            float value = Mathf.Lerp(from, to, t / dissolveDuration);
            dissolveMaterial.SetFloat(DissolveAmount, value);
            yield return null;
        }
        dissolveMaterial.SetFloat(DissolveAmount, to);
    }

    bool IsOutOfView()
    {
        if (playerCamera == null) return false;

        Vector3 viewportPos = playerCamera.WorldToViewportPoint(character.transform.position);

        bool behind = viewportPos.z < 0;
        bool outsideX = viewportPos.x < -viewportMargin || viewportPos.x > 1f + viewportMargin;
        bool outsideY = viewportPos.y < -viewportMargin || viewportPos.y > 1f + viewportMargin;

        return behind || outsideX || outsideY;
    }
}