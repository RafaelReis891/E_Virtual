using System.Collections;
using UnityEngine;

/// <summary>
/// Toca até 3 áudios em sequência, com intervalo entre eles.
/// Cada áudio pode ativar um objeto no início e desativar no fim.
/// </summary>
public class AudioSequencePlayer : MonoBehaviour
{
    [System.Serializable]
    public class AudioEntry
    {
        public AudioClip clip;

        [Tooltip("Objeto a ativar quando o áudio começar (ex.: imagem no Canvas).")]
        public GameObject showOnPlay;

        [Tooltip("Se marcado, desativa o objeto quando o áudio terminar.")]
        public bool hideOnEnd = true;
    }

    [Header("Áudios (até 3)")]
    public AudioEntry audio1;
    public AudioEntry audio2;
    public AudioEntry audio3;

    [Header("Intervalo")]
    [Tooltip("Segundos de espera entre um áudio e o próximo.")]
    public float delayBetween = 1f;

    [Header("Opções")]
    public bool playOnAwake = true;
    public bool playOnce = false;

    AudioSource audioSource;
    bool hasPlayed;
    Coroutine playing;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // Garante que os objetos começam desativados
        SetActive(audio1, false);
        SetActive(audio2, false);
        SetActive(audio3, false);
    }

    void Start()
    {
        if (playOnAwake)
            Play();
    }

    public void Play()
    {
        if (playOnce && hasPlayed) return;

        if (playing != null)
            StopCoroutine(playing);

        playing = StartCoroutine(PlaySequence());
    }

    public void Stop()
    {
        if (playing != null)
        {
            StopCoroutine(playing);
            playing = null;
        }
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        SetActive(audio1, false);
        SetActive(audio2, false);
        SetActive(audio3, false);
    }

    IEnumerator PlaySequence()
    {
        hasPlayed = true;

        AudioEntry[] entries = { audio1, audio2, audio3 };

        for (int i = 0; i < entries.Length; i++)
        {
            AudioEntry entry = entries[i];
            if (entry == null || entry.clip == null) continue;

            // Ativa o objeto
            SetActive(entry, true);

            audioSource.clip = entry.clip;
            audioSource.Play();

            // Espera o áudio terminar
            yield return new WaitForSeconds(entry.clip.length);

            // Desativa o objeto se configurado
            if (entry.hideOnEnd)
                SetActive(entry, false);

            // Intervalo antes do próximo
            if (i < entries.Length - 1 && HasNext(entries, i))
                yield return new WaitForSeconds(delayBetween);
        }

        playing = null;
    }

    void SetActive(AudioEntry entry, bool active)
    {
        if (entry != null && entry.showOnPlay != null)
            entry.showOnPlay.SetActive(active);
    }

    bool HasNext(AudioEntry[] entries, int current)
    {
        for (int i = current + 1; i < entries.Length; i++)
            if (entries[i] != null && entries[i].clip != null) return true;
        return false;
    }
}
