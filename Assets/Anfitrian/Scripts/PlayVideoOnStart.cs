using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class PlayVideoOnStart : MonoBehaviour
{
    [System.Serializable]
    public class VideoEntry
    {
        public VideoClip clip;

        [Tooltip("Segundos de espera antes deste vídeo.")]
        public float delayBefore = 0f;
    }

    [Header("Referências")]
    public VideoPlayer videoPlayer;
    public GameObject quadVisual;

    [Header("Vídeos")]
    public VideoEntry[] videos;

    [Header("Opções")]
    public bool playSequenceOnStart = false;
    public bool hideOnEnd = true;

    int currentIndex = -1;

    void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (quadVisual == null)
            quadVisual = gameObject;

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.loopPointReached += OnVideoEnd;
        }

        Hide();
    }

    void Start()
    {
        if (playSequenceOnStart)
            PlaySequence();
    }

    public void PlaySequence()
    {
        currentIndex = -1;
        PlayNext();
    }

    void PlayNext()
    {
        currentIndex++;
        if (videos == null || currentIndex >= videos.Length)
        {
            OnSequenceEnd();
            return;
        }

        VideoEntry entry = videos[currentIndex];
        if (entry == null || entry.clip == null)
        {
            PlayNext();
            return;
        }

        StartCoroutine(PlayClip(entry));
    }

    IEnumerator PlayClip(VideoEntry entry)
    {
        // Pausa antes do vídeo
        if (entry.delayBefore > 0f)
        {
            Hide();
            yield return new WaitForSeconds(entry.delayBefore);
        }

        Show();

        if (videoPlayer == null) yield break;

        videoPlayer.Stop();
        videoPlayer.clip = entry.clip;

        yield return null;

        videoPlayer.Play();
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        PlayNext();
    }

    void OnSequenceEnd()
    {
        currentIndex = -1;
        if (hideOnEnd)
            Hide();
    }

    public void Stop()
    {
        StopAllCoroutines();
        if (videoPlayer != null)
            videoPlayer.Stop();
        currentIndex = -1;
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
