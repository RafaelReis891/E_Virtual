
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
    [Min(0f)] public float dissolveDuration = 1.5f;

    [Tooltip("Marque para o dissolve começar de baixo para cima.")]
    public bool dissolveFromBottom = true;

    [Header("Áudio")]
    public AudioClip voiceClip;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Objeto extra (opcional)")]
    public GameObject objectToActivate;

    [Header("Tempo")]
    [Min(0f)] public float waitAfterAudio = 2f;

    [Header("Campo de visão")]
    public Camera playerCamera;
    [Range(0f, 0.2f)] public float viewportMargin = 0.05f;

    private AudioSource audioSource;
    private Material dissolveInstance;

    private bool sequenceStarted = false;
    private bool waitingToHide = false;

    private static readonly int DissolveAmount =
        Shader.PropertyToID("_DissolveAmount");

    private static readonly int Direction =
        Shader.PropertyToID("_Direction");

    void Awake()
    {
        // Encontrar o Renderer antes de desativar o personagem.
        if (character != null && characterRenderer == null)
        {
            characterRenderer =
                character.GetComponentInChildren<Renderer>(true);
        }

        // Encontrar a câmera.
        if (playerCamera == null)
            playerCamera = Camera.main;

        // Configurar áudio.
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // O personagem começa desativado.
        if (character != null)
            character.SetActive(false);
        else
            Debug.LogWarning(
                "Nenhum personagem foi atribuído no Inspector.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (sequenceStarted)
            return;

        if (!other.CompareTag("Player"))
            return;

        sequenceStarted = true;
        StartCoroutine(FullSequence());
    }

    IEnumerator FullSequence()
    {
        // 1. Ativar personagem.
        if (character != null)
            character.SetActive(true);

        // 2. Executar o Dissolve de entrada.
        if (useDissolve)
        {
            if (characterRenderer == null)
            {
                Debug.LogWarning(
                    "Renderer do personagem não encontrado.", this);
            }
            else if (dissolveMaterial == null)
            {
                Debug.LogWarning(
                    "Material de Dissolve não atribuído.", this);
            }
            else if (originalMaterial == null)
            {
                Debug.LogWarning(
                    "Material original não atribuído.", this);
            }
            else
            {
                dissolveInstance = new Material(dissolveMaterial);

                if (!dissolveInstance.HasProperty(DissolveAmount))
                {
                    Debug.LogError(
                        "O shader não possui _DissolveAmount. " +
                        "Verifique o nome da propriedade.", this);

                    Destroy(dissolveInstance);
                    dissolveInstance = null;
                }
                else
                {
                    if (dissolveInstance.HasProperty(Direction))
                    {
                        float dir = dissolveFromBottom ? 1f : -1f;
                        dissolveInstance.SetFloat(Direction, dir);
                    }

                    // Aplicar o material temporário.
                    characterRenderer.material = dissolveInstance;

                    // Revelar o personagem: 1 para 0.
                    yield return StartCoroutine(Dissolve(1f, 0f));
                }
            }
        }

        // 3. Restaurar o material original.
        if (characterRenderer != null && originalMaterial != null)
        {
            characterRenderer.material = originalMaterial;
        }

        // Liberar a instância temporária.
        if (dissolveInstance != null)
        {
            Destroy(dissolveInstance);
            dissolveInstance = null;
        }

        // 4. Reproduzir áudio.
        if (voiceClip != null)
        {
            audioSource.Stop();
            audioSource.clip = voiceClip;
            audioSource.volume = volume;
            audioSource.Play();

            yield return new WaitForSeconds(voiceClip.length);
        }

        // 5. Ativar objeto extra.
        if (objectToActivate != null)
            objectToActivate.SetActive(true);

        // 6. Aguardar antes de monitorar a câmera.
        yield return new WaitForSeconds(waitAfterAudio);

        // 7. Permitir que o personagem desapareça fora da visão.
        waitingToHide = true;
    }

    IEnumerator Dissolve(float from, float to)
    {
        if (dissolveInstance == null)
            yield break;

        if (dissolveDuration <= 0f)
        {
            dissolveInstance.SetFloat(DissolveAmount, to);
            yield break;
        }

        float elapsed = 0f;

        dissolveInstance.SetFloat(DissolveAmount, from);

        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / dissolveDuration);

            float value = Mathf.Lerp(from, to, progress);

            dissolveInstance.SetFloat(DissolveAmount, value);

            yield return null;
        }

        // Garantir o valor final.
        dissolveInstance.SetFloat(DissolveAmount, to);
    }

    void Update()
    {
        if (!waitingToHide)
            return;

        if (character == null || !character.activeSelf)
        {
            waitingToHide = false;
            return;
        }

        if (IsOutOfView())
        {
            // Desaparecer sem executar outro Dissolve.
            character.SetActive(false);
            waitingToHide = false;
        }
    }

    bool IsOutOfView()
    {
        if (playerCamera == null || character == null)
            return false;

        Vector3 viewportPos =
            playerCamera.WorldToViewportPoint(
                character.transform.position);

        bool behindCamera = viewportPos.z < 0f;

        bool outsideX =
            viewportPos.x < -viewportMargin ||
            viewportPos.x > 1f + viewportMargin;

        bool outsideY =
            viewportPos.y < -viewportMargin ||
            viewportPos.y > 1f + viewportMargin;

        return behindCamera || outsideX || outsideY;
    }

    void OnDestroy()
    {
        // Evitar deixar a instância temporária na memória.
        if (dissolveInstance != null)
            Destroy(dissolveInstance);
    }
}
