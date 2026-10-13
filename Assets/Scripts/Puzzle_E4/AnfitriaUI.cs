using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections.Generic;

/// <summary>
/// Anfitriã em UI (Canvas).
/// Ativa por Box Collider (trigger) ou por código.
/// Escolhe qual clipe tocar na lista.
/// </summary>
public class AnfitriaUI : MonoBehaviour
{
    [System.Serializable]
    public class ClipEntry
    {
        public string id = "dica_baixo";
        public VideoClip clip;
        [TextArea] public string subtitle;
    }

    [Header("UI")]
    public CanvasGroup canvasGroup;
    public RawImage rawImage;
    public RenderTexture renderTexture;

    [Header("Vídeo")]
    public VideoPlayer videoPlayer;
    public List<ClipEntry> clips = new List<ClipEntry>();
    public string defaultClipId = "dica_baixo";

    [Header("Trigger (opcional)")]
    public bool useTrigger = true;
    public string playerTag = "Player";
    public bool playOnlyOnce = false;

    [Header("Opções")]
    public bool hideOnEnd = true;

    bool hasPlayed;
    string currentId;

    void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            if (renderTexture != null)
                videoPlayer.targetTexture = renderTexture;
            if (rawImage != null && renderTexture != null)
                rawImage.texture = renderTexture;
        }

        Hide();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useTrigger) return;
        if (!other.CompareTag(playerTag)) return;
        if (playOnlyOnce && hasPlayed) return;

        Play(defaultClipId);
    }

    /// <summary>
    /// Chame por código: anfitria.Play("dica_baixo");
    /// </summary>
    public void Play(string clipId)
    {
        var entry = clips.Find(c => c.id == clipId);
        if (entry == null || entry.clip == null)
        {
            Debug.LogWarning($"[AnfitriaUI] Clipe '{clipId}' não encontrado.");
            return;
        }

        currentId = clipId;
        hasPlayed = true;

        videoPlayer.clip = entry.clip;
        Show();
        videoPlayer.Stop();
        videoPlayer.Play();

        videoPlayer.loopPointReached -= OnEnd;
        videoPlayer.loopPointReached += OnEnd;
    }

    /// <summary>
    /// Atalho para o clipe padrão.
    /// </summary>
    public void PlayDefault() => Play(defaultClipId);

    public void Stop()
    {
        if (videoPlayer != null) videoPlayer.Stop();
        if (hideOnEnd) Hide();
    }

    void OnEnd(VideoPlayer vp)
    {
        if (hideOnEnd) Hide();
    }

    void Show()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1;
            canvasGroup.blocksRaycasts = false;
        }
        if (rawImage != null)
            rawImage.gameObject.SetActive(true);
    }

    void Hide()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0;
        if (rawImage != null)
            rawImage.gameObject.SetActive(false);
    }
}
