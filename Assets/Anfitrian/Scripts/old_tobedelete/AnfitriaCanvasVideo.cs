using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Anfitriã em vídeo no Canvas (RawImage + VideoPlayer).
/// Suporta sequência de vídeos MP4 e chroma key.
/// </summary>
public class AnfitriaCanvasVideo : MonoBehaviour
{
    [Header("Canvas")]
    [Tooltip("RawImage onde o vídeo aparece.")]
    public RawImage videoImage;

    [Header("Vídeo")]
    public VideoPlayer videoPlayer;
    public VideoClip[] videos;

    [Header("Áudio")]
    [Tooltip("Marque se os MP4 têm áudio embutido.")]
    public bool videoHasAudio = true;
    public AudioSource audioSource;

    [Header("Sequência")]
    [Tooltip("Toca a sequência ao iniciar a cena.")]
    public bool playSequenceOnStart = false;

    [Tooltip("Pausa individual antes de cada vídeo.")]
    public float[] delayBefore;

    [Tooltip("Esconde o RawImage ao terminar.")]
    public bool hideOnEnd = true;

    [Header("Chroma Key")]
    [Tooltip("Material com shader de chroma key. Arraste se usar.")]
    public Material chromaMaterial;

    [Header("Ajuste de Tela")]
    [Tooltip("Mantém a proporção do vídeo (evita cortar a cabeça).")]
    public bool fitAspectRatio = true;

    [Tooltip("Altura fixa do RawImage. A largura se ajusta.")]
    public float fixedHeight = 400f;

    int currentIndex = -1;
    Coroutine playRoutine;
    RenderTexture renderTexture;
    RectTransform imageRect;

    void Awake()
    {
        if (videoImage == null)
            videoImage = GetComponent<RawImage>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<VideoPlayer>();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = videoHasAudio
            ? VideoAudioOutputMode.AudioSource
            : VideoAudioOutputMode.None;

        if (videoHasAudio)
        {
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);
        }

        // RenderTexture
        renderTexture = new RenderTexture(1024, 1024, 0);
        renderTexture.Create();
        videoPlayer.targetTexture = renderTexture;

        if (videoImage != null)
        {
            videoImage.texture = renderTexture;
            imageRect = videoImage.rectTransform;
            if (chromaMaterial != null)
                videoImage.material = chromaMaterial;
        }

        videoPlayer.loopPointReached += OnVideoEnd;

        if (hideOnEnd && videoImage != null)
            videoImage.enabled = false;
    }

    void Start()
    {
        if (playSequenceOnStart)
            PlaySequence();
    }

    /// <summary>
    /// Toca todos os vídeos em sequência.
    /// </summary>
    public void PlaySequence()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlaySequenceRoutine());
    }

    /// <summary>
    /// Toca um vídeo específico.
    /// </summary>
    public void PlayVideo(int index)
    {
        if (videos == null || index < 0 || index >= videos.Length) return;
        if (playRoutine != null)
            StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlaySingle(index));
    }

    IEnumerator PlaySequenceRoutine()
    {
        currentIndex = -1;

        for (int i = 0; i < videos.Length; i++)
        {
            if (videos[i] == null) continue;

            // Pausa antes
            if (delayBefore != null && i < delayBefore.Length && delayBefore[i] > 0)
                yield return new WaitForSeconds(delayBefore[i]);

            yield return StartCoroutine(PlayClip(videos[i]));
        }

        OnSequenceEnd();
    }

    IEnumerator PlaySingle(int index)
    {
        if (delayBefore != null && index < delayBefore.Length && delayBefore[index] > 0)
            yield return new WaitForSeconds(delayBefore[index]);

        yield return StartCoroutine(PlayClip(videos[index]));
        OnSequenceEnd();
    }

    IEnumerator PlayClip(VideoClip clip)
    {
        Show();

        videoPlayer.Stop();
        videoPlayer.clip = clip;

        if (videoHasAudio)
            videoPlayer.SetTargetAudioSource(0, audioSource);

        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
            yield return null;

        // Cria RenderTexture na proporção do vídeo
        SetupRenderTexture();

        videoPlayer.Play();

        // Ajusta tamanho do RawImage
        if (fitAspectRatio)
            FitToVideo();

        // Espera terminar
        while (videoPlayer.isPlaying)
            yield return null;
    }

    void SetupRenderTexture()
    {
        uint w = videoPlayer.width;
        uint h = videoPlayer.height;
        if (w == 0 || h == 0) return;

        // Libera o anterior
        if (renderTexture != null)
        {
            videoPlayer.targetTexture = null;
            renderTexture.Release();
            Destroy(renderTexture);
        }

        // Novo RT na proporção correta
        renderTexture = new RenderTexture((int)w, (int)h, 0);
        renderTexture.Create();
        videoPlayer.targetTexture = renderTexture;

        if (videoImage != null)
        {
            videoImage.texture = renderTexture;
            videoImage.uvRect = new Rect(0, 0, 1, 1);
        }
    }

    void FitToVideo()
    {
        if (videoPlayer == null || imageRect == null) return;

        uint w = videoPlayer.width;
        uint h = videoPlayer.height;
        if (w == 0 || h == 0) return;

        float aspect = (float)w / h;
        float newWidth = fixedHeight * aspect;
        imageRect.sizeDelta = new Vector2(newWidth, fixedHeight);
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        // Usado só se não estiver em sequência controlada
    }

    void OnSequenceEnd()
    {
        currentIndex = -1;
        if (hideOnEnd)
            Hide();
    }

    public void Stop()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);
        if (videoPlayer != null)
            videoPlayer.Stop();
        Hide();
    }

    void Show()
    {
        if (videoImage != null && !videoImage.enabled)
            videoImage.enabled = true;
    }

    void Hide()
    {
        if (videoImage != null && videoImage.enabled)
            videoImage.enabled = false;
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoEnd;
        if (renderTexture != null)
            renderTexture.Release();
    }
}
