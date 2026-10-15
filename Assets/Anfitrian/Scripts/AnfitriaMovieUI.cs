using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.Events;

/// <summary>
/// Anfitriã em vídeo no Canvas (RawImage + VideoPlayer).
/// O vídeo pode ter ou não áudio. Se tiver AudioClip separado, toca junto.
/// </summary>
public class AnfitriaMovieUI : MonoBehaviour
{
    [Header("Canvas")]
    [Tooltip("RawImage que mostra o vídeo.")]
    public RawImage videoImage;

    [Header("Vídeo")]
    [Tooltip("Clip de vídeo (MP4 H.264 recomendado).")]
    public VideoClip videoClip;

    [Tooltip("VideoPlayer. Se vazio, o script cria um.")]
    public VideoPlayer videoPlayer;

    [Header("Áudio (opcional)")]
    [Tooltip("Marque se o vídeo já tem áudio embutido.")]
    public bool videoHasAudio = false;

    [Tooltip("Áudio separado (MP3/WAV). Deixe vazio se o vídeo já tiver som ou se não quiser áudio.")]
    public AudioClip audioClip;

    [Tooltip("AudioSource. Se vazio, o script cria um.")]
    public AudioSource audioSource;

    [Header("Comportamento")]
    [Tooltip("Esconde o RawImage quando não está tocando.")]
    public bool hideWhenIdle = true;

    [Tooltip("Desativa o RawImage ao terminar.")]
    public bool hideOnEnd = true;

    public UnityEvent OnVideoStart;
    public UnityEvent OnVideoEnd;

    RenderTexture renderTexture;
    bool isPlaying;

    void Awake()
    {
        if (videoImage == null)
            videoImage = GetComponent<RawImage>();

        SetupVideoPlayer();
        SetupAudio();

        if (hideWhenIdle && videoImage != null)
            videoImage.enabled = false;
    }

    void SetupVideoPlayer()
    {
        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<VideoPlayer>();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = videoHasAudio
            ? VideoAudioOutputMode.AudioSource
            : VideoAudioOutputMode.None;

        if (videoHasAudio && audioSource != null)
        {
            videoPlayer.SetTargetAudioSource(0, audioSource);
        }

        // RenderTexture para o RawImage
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(512, 512, 0);
            renderTexture.Create();
        }
        videoPlayer.targetTexture = renderTexture;

        if (videoImage != null)
            videoImage.texture = renderTexture;

        videoPlayer.loopPointReached += OnVideoFinished;
    }

    void SetupAudio()
    {
        if (audioSource == null && (audioClip != null || videoHasAudio))
            audioSource = gameObject.AddComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D
        }
    }

    /// <summary>
    /// Toca o vídeo. Se tiver AudioClip separado, toca junto.
    /// </summary>
    public void Play()
    {
        if (videoClip == null || videoPlayer == null) return;

        StopAllCoroutines();
        isPlaying = true;

        videoPlayer.clip = videoClip;
        videoPlayer.Prepare();

        StartCoroutine(PlayWhenReady());
    }

    IEnumerator PlayWhenReady()
    {
        while (!videoPlayer.isPrepared)
            yield return null;

        if (videoImage != null)
            videoImage.enabled = true;

        videoPlayer.Play();

        // Áudio separado (se o vídeo não tiver)
        if (!videoHasAudio && audioClip != null && audioSource != null)
        {
            audioSource.clip = audioClip;
            audioSource.Play();
        }

        OnVideoStart?.Invoke();
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        isPlaying = false;

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        if (hideOnEnd && videoImage != null)
            videoImage.enabled = false;

        OnVideoEnd?.Invoke();
    }

    /// <summary>
    /// Para o vídeo e o áudio.
    /// </summary>
    public void Stop()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Stop();

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        isPlaying = false;

        if (hideWhenIdle && videoImage != null)
            videoImage.enabled = false;
    }

    /// <summary>
    /// Chamado por TriggerEvent (OnTriggerEnter).
    /// </summary>
    public void PlayFromTrigger(Collider other)
    {
        Play();
    }

    void OnDestroy()
    {
        if (renderTexture != null)
            renderTexture.Release();
    }
}
