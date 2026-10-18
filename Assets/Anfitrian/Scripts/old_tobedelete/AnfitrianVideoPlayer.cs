using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Controla o Quad da anfitriã. Esconde quando parado, mostra ao tocar, esconde ao terminar.
/// </summary>
public class AnfitriaVideoController : MonoBehaviour
{
    [Header("Referências")]
    public VideoPlayer videoPlayer;
    public GameObject quadVisual;

    [Header("Vídeos")]
    public VideoClip[] videos;

    [Header("Opções")]
    [Tooltip("Esconde o Quad ao terminar o vídeo.")]
    public bool hideOnEnd = true;

    void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (quadVisual == null)
            quadVisual = gameObject;

        videoPlayer.playOnAwake = true;
        videoPlayer.loopPointReached += OnVideoEnd;

        Hide();
    }

    /// <summary>
    /// Toca o vídeo pelo índice do array.
    /// </summary>
    public void PlayVideo(int index)
    {
        if (videos == null || index < 0 || index >= videos.Length) return;
        if (videos[index] == null) return;

        videoPlayer.Stop();
        videoPlayer.clip = videos[index];

        Show();
        videoPlayer.Play();
    }

    /// <summary>
    /// Para e esconde.
    /// </summary>
    public void Stop()
    {
        if (videoPlayer != null)
            videoPlayer.Stop();
        Hide();
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        if (hideOnEnd)
            Hide();
    }

    void Show()
    {
        if (quadVisual != null)
            quadVisual.SetActive(true);
    }

    void Hide()
    {
        if (quadVisual != null)
            quadVisual.SetActive(false);
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoEnd;
    }
}
