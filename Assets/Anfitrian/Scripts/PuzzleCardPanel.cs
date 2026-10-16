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
    [Tooltip("Dispara só uma vez.")]
    public bool playOnce = true;

    AudioSource audioSource;
    bool hasPlayed;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (panel != null)
            panel.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (playOnce && hasPlayed) return;
        Show();
    }

    /// <summary>
    /// Chame por código ou pelo TriggerEvent.
    /// </summary>
    public void Show()
    {
        if (playOnce && hasPlayed) return;
        hasPlayed = true;

        if (cardImage != null && cardSprite != null)
            cardImage.sprite = cardSprite;

        if (panel != null)
            panel.SetActive(true);

        StartCoroutine(DisplayAndHide());
    }

    IEnumerator DisplayAndHide()
    {
        // Áudio 1
        if (audio1 != null)
            audioSource.PlayOneShot(audio1);

        // Espera e toca áudio 2
        if (audio2 != null)
        {
            if (audio1 != null)
                yield return new WaitForSeconds(audio1.length + delayBetweenAudio);
            else
                yield return new WaitForSeconds(delayBetweenAudio);

            audioSource.PlayOneShot(audio2);
        }

        // Espera o tempo de exibição
        yield return new WaitForSeconds(displayTime);

        if (panel != null)
            panel.SetActive(false);
    }

    /// <summary>
    /// Reseta para poder mostrar de novo.
    /// </summary>
    public void ResetCard()
    {
        hasPlayed = false;
    }
}
