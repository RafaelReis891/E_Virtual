using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ativa um painel de card ao passar pelo trigger.
/// Troca a imagem, espera um tempo e desativa. Toca até 2 áudios.
/// </summary>
public class PuzzleCardPanel : MonoBehaviour
{
    [Header("Painel")]
    [Tooltip("Painel do Canvas que será ativado.")]
    public GameObject panel;

    [Tooltip("Image do card dentro do painel.")]
    public Image cardImage;

    [Header("Conteúdo")]
    [Tooltip("Sprite do card desta carta.")]
    public Sprite cardSprite;

    [Tooltip("Segundos que o painel fica ativo.")]
    public float displayTime = 3f;

    [Header("Áudios (opcional)")]
    public AudioClip audio1;
    public AudioClip audio2;

    [Tooltip("Espera entre o áudio 1 e o áudio 2.")]
    public float delayBetweenAudio = 0.5f;

    [Header("Opções")]
    [Tooltip("Dispara só uma vez por sessão de play.")]
    public bool playOnce = true;

    [Tooltip("Mostra logs no Console.")]
    public bool debugLog = true;

    AudioSource audioSource;
    bool hasPlayed;
    Coroutine displayRoutine;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (debugLog)
            Debug.Log($"[PuzzleCardPanel] Awake. Panel ref: {(panel != null ? panel.name : "NULL")}");
    }

    void OnEnable()
    {
        hasPlayed = false;

        // Só desativa se o painel for diferente deste objeto
        if (panel != null && panel != gameObject)
            panel.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (debugLog)
            Debug.Log($"[PuzzleCardPanel] Trigger enter: {other.name}");
        Show();
    }

    /// <summary>
    /// Chame por código ou pelo TriggerEvent.
    /// </summary>
    public void Show()
    {
        if (playOnce && hasPlayed)
        {
            if (debugLog) Debug.Log("[PuzzleCardPanel] Já mostrou. Ignorando.");
            return;
        }

        hasPlayed = true;

        if (debugLog)
            Debug.Log($"[PuzzleCardPanel] Show(). Panel: {(panel != null ? panel.name : "NULL")}, Sprite: {(cardSprite != null ? cardSprite.name : "NULL")}");

        // Ativa o painel primeiro
        if (panel != null)
        {
            // Garante que o pai está ativo
            Transform parent = panel.transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    if (debugLog) Debug.Log($"[PuzzleCardPanel] Ativando pai: {parent.name}");
                    parent.gameObject.SetActive(true);
                }
                parent = parent.parent;
            }

            panel.SetActive(true);
            if (debugLog) Debug.Log($"[PuzzleCardPanel] Panel ativo: {panel.activeSelf}");
        }
        else
        {
            Debug.LogWarning("[PuzzleCardPanel] Panel está NULL! Arraste o painel no Inspector.");
        }

        // Troca a imagem
        if (cardImage != null && cardSprite != null)
        {
            cardImage.sprite = cardSprite;
            if (debugLog) Debug.Log("[PuzzleCardPanel] Sprite atribuído.");
        }
        else if (cardImage == null)
        {
            Debug.LogWarning("[PuzzleCardPanel] Card Image está NULL!");
        }

        // Cancela coroutine anterior
        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        displayRoutine = StartCoroutine(DisplayAndHide());
    }

    IEnumerator DisplayAndHide()
    {
        // Áudio 1
        if (audio1 != null && audioSource != null)
            audioSource.PlayOneShot(audio1);

        // Espera e toca áudio 2
        if (audio2 != null)
        {
            float wait = (audio1 != null) ? audio1.length + delayBetweenAudio : delayBetweenAudio;
            yield return new WaitForSeconds(wait);

            if (audioSource != null)
                audioSource.PlayOneShot(audio2);
        }

        // Espera o tempo de exibição
        if (debugLog) Debug.Log($"[PuzzleCardPanel] Esperando {displayTime}s...");
        yield return new WaitForSeconds(displayTime);

        if (panel != null)
        {
            panel.SetActive(false);
            if (debugLog) Debug.Log("[PuzzleCardPanel] Panel desativado.");
        }

        displayRoutine = null;
    }

    /// <summary>
    /// Reseta para poder mostrar de novo.
    /// </summary>
    public void ResetCard()
    {
        hasPlayed = false;
        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
            displayRoutine = null;
        }
        if (panel != null)
            panel.SetActive(false);
    }
}
