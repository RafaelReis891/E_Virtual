using UnityEngine;
using System.Collections;

public class CharacterDissolveSequence : MonoBehaviour
{
    [Header("Materials")]
    public Material dissolveMaterial;
    public Material originalMaterial;

    [Header("Dissolve")]
    public float dissolveDuration = 1.5f;
    public bool fromBottomToTop = true;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip voiceClip;

    [Header("Timing")]
    public float waitAfterAudio = 1f;

    private Renderer characterRenderer;
    private static readonly int DissolveAmount = Shader.PropertyToID("_DissolveAmount");
    private static readonly int Direction = Shader.PropertyToID("_Direction");

    void Awake()
    {
        characterRenderer = GetComponentInChildren<Renderer>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            StartCoroutine(FullSequence());
    }

    IEnumerator FullSequence()
    {
        gameObject.SetActive(true);

        // Usa o material de dissolve para aparecer
        characterRenderer.material = dissolveMaterial;
        yield return Dissolve(1f, 0f);

        // Troca para a textura original
        characterRenderer.material = originalMaterial;

        // Toca o áudio
        if (audioSource && voiceClip)
        {
            audioSource.clip = voiceClip;
            audioSource.Play();
            yield return new WaitForSeconds(voiceClip.length);
        }

        // Espera configurável
        yield return new WaitForSeconds(waitAfterAudio);

        // Volta para o material de dissolve e some
        characterRenderer.material = dissolveMaterial;
        yield return Dissolve(0f, 1f);

        // Esconde o personagem
        gameObject.SetActive(false);
    }

    IEnumerator Dissolve(float from, float to)
    {
        float dir = fromBottomToTop ? 1f : -1f;
        dissolveMaterial.SetFloat(Direction, dir);

        float t = 0f;
        while (t < dissolveDuration)
        {
            t += Time.deltaTime;
            float value = Mathf.Lerp(from, to, t / dissolveDuration);
            dissolveMaterial.SetFloat(DissolveAmount, value);
            yield return null;
        }
        dissolveMaterial.SetFloat(DissolveAmount, to);
    }
}