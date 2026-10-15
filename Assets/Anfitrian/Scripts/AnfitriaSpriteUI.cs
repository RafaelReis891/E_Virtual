using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Anfitriã em UI com sprites.
/// A animação dura o mesmo tempo do áudio e cicla as bocas.
/// AudioSource é criado automaticamente se não for atribuído.
/// </summary>
public class AnfitriaSpriteUI : MonoBehaviour
{
    [System.Serializable]
    public class Sequence
    {
        public string id = "dica_baixo";
        public List<Sprite> frames = new List<Sprite>();
        public AudioClip voice;
        [Tooltip("Tempo de cada boca. Menor = mais rápido.")]
        public float mouthInterval = 0.12f;
    }

    [Header("UI")]
    public Image portrait;

    [Header("Áudio")]
    public AudioSource audioSource;

    [Header("Sequências")]
    public List<Sequence> sequences = new List<Sequence>();
    public string defaultId = "dica_baixo";

    [Header("Trigger")]
    public bool useTrigger = true;
    public string playerTag = "Player";
    public bool playOnlyOnce = false;
    public bool hideOnEnd = true;

    bool hasPlayed;
    Coroutine playing;

    void Awake()
    {
        // Cria AudioSource se não tiver
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D

        if (portrait != null)
            portrait.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useTrigger || !other.CompareTag(playerTag)) return;
        if (playOnlyOnce && hasPlayed) return;
        Play(defaultId);
    }

    public void Play(string id)
    {
        var seq = sequences.Find(s => s.id == id);
        if (seq == null || seq.frames == null || seq.frames.Count == 0)
        {
            Debug.LogWarning("[Anfitria] Sequência não encontrada: " + id);
            return;
        }

        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(PlayRoutine(seq));
    }

    public void PlayDefault() => Play(defaultId);

    IEnumerator PlayRoutine(Sequence seq)
    {
        hasPlayed = true;
        if (portrait != null)
            portrait.enabled = true;

        // Toca o áudio
        float duration = 2f;
        if (seq.voice != null && audioSource != null)
        {
            audioSource.clip = seq.voice;
            audioSource.Play();
            duration = seq.voice.length;
        }

        // Cicla as bocas durante todo o áudio
        float elapsed = 0f;
        int frameIndex = 0;
        float nextMouth = 0f;

        while (elapsed < duration)
        {
            if (elapsed >= nextMouth)
            {
                if (portrait != null && seq.frames.Count > 0)
                    portrait.sprite = seq.frames[frameIndex % seq.frames.Count];

                frameIndex++;
                nextMouth += seq.mouthInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Último frame (boca fechada, se existir)
        if (portrait != null && seq.frames.Count > 0)
            portrait.sprite = seq.frames[0];

        yield return new WaitForSeconds(0.25f);

        if (hideOnEnd && portrait != null)
            portrait.enabled = false;

        playing = null;
    }

    public void Hide()
    {
        if (playing != null) StopCoroutine(playing);
        if (portrait != null) portrait.enabled = false;
        if (audioSource != null) audioSource.Stop();
    }
}
