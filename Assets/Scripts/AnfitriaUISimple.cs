using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Liga o RawImage quando o vídeo começa e desliga quando termina.
/// </summary>
public class AnfitriaUISimple : MonoBehaviour
{
    [Header("Referências")]
    public VideoPlayer videoPlayer;
    public RawImage rawImage;
    public RenderTexture renderTexture;
    public VideoClip clip;

    [Header("Trigger")]
    public bool useTrigger = true;
    public string playerTag = "Player";
    public bool playOnlyOnce = false;

    bool hasPlayed;

    void Awake()
    {
        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            if (renderTexture != null)
                videoPlayer.targetTexture = renderTexture;

            videoPlayer.loopPointReached += OnVideoFinished;
        }

        if (rawImage != null)
        {
            if (renderTexture != null)
                rawImage.texture = renderTexture;
            rawImage.enabled = false; // começa desligado
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useTrigger) return;
        if (!other.CompareTag(playerTag)) return;
        if (playOnlyOnce && hasPlayed) return;
        Play();
    }

    public void Play()
    {
        if (videoPlayer == null || rawImage == null)
        {
            Debug.LogWarning("[Anfitria] VideoPlayer ou RawImage não atribuído.");
            return;
        }

        hasPlayed = true;

        if (clip != null)
            videoPlayer.clip = clip;

        // 1. Liga o RawImage ANTES de dar play
        rawImage.enabled = true;
        rawImage.texture = renderTexture;

        // 2. Toca
        videoPlayer.Stop();
        videoPlayer.Play();

        Debug.Log("[Anfitria] RawImage ligado e vídeo iniciado.");
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        // 3. Desliga quando terminar
        if (rawImage != null)
            rawImage.enabled = false;

        Debug.Log("[Anfitria] Vídeo terminou, RawImage desligado.");
    }

    public void Hide()
    {
        if (videoPlayer != null) videoPlayer.Stop();
        if (rawImage != null) rawImage.enabled = false;
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}
