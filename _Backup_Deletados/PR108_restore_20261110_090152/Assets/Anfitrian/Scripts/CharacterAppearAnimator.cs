using UnityEngine;
using System.Collections;

/// <summary>
/// Ativa o personagem com dissolve, toca o áudio e dissolve de saída.
/// Coloque este script no objeto do trigger (não no personagem).
/// O Character pode ser um prefab - o script instancia.
/// </summary>
public class CharacterAppearDissolve : MonoBehaviour
{
    [Header("Personagem")]
    [Tooltip("Prefab ou objeto na cena. Se for prefab, será instanciado.")]
    public GameObject characterPrefab;

    [Tooltip("Ponto onde o personagem aparece. Se vazio, usa a posição do prefab.")]
    public Transform spawnPoint;

    [Header("Dissolve")]
    public bool useDissolve = true;
    [Min(0f)] public float dissolveDuration = 1.5f;
    public bool dissolveFromBottom = true;

    [Header("Áudio")]
    public AudioClip voiceClip;
    [Tooltip("Versao em ingles. Preenchido sozinho no Editor com o audio de mesmo nome + _eng.")]
    public AudioClip voiceClipEng;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Objeto extra (opcional)")]
    public GameObject objectToActivate;

    [Header("Tempo após áudio")]
    [Min(0f)] public float waitAfterAudio = 0.5f;

    [Header("Saida")]
    [Tooltip("Ligado = se o player sair do trigger, o audio faz fade-out e a Anfitria some com dissolve. Ela pode aparecer de novo quando ele voltar.")]
    public bool sairAoDeixarTrigger = true;

    [Tooltip("Ligado = a Anfitria aparece so na primeira vez que o player entra no trigger. Depois nao reativa mais.")]
    public bool aparecerSoUmaVez = false;

    [Header("Debug")]
    public bool showLogs = true;

    AudioSource audioSource;
    Material dissolveMat;
    GameObject characterInstance;
    bool sequenceStarted = false;
    bool encerrando = false;
    Coroutine sequencia;

    // So uma Anfitria ativa por vez: a anterior encerra quando outra comeca
    static CharacterAppearDissolve ativa;
    const float FadeAudio = 0.5f;

    static readonly int DissolveAmount = Shader.PropertyToID("_Dissolve");
    static readonly int Direction = Shader.PropertyToID("_Direction");

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    void OnTriggerEnter(Collider other)
    {
        Log("Trigger enter: " + other.name + " tag: " + other.tag);

        if (sequenceStarted) return;
        if (!other.CompareTag("Player")) return;

        if (ativa != null && ativa != this) ativa.Encerrar();
        ativa = this;

        sequenceStarted = true;
        encerrando = false;
        sequencia = StartCoroutine(FullSequence());
    }

    void OnTriggerExit(Collider other)
    {
        if (!sairAoDeixarTrigger) return;
        if (!other.CompareTag("Player")) return;
        Encerrar();
    }

    void OnDisable()
    {
        if (ativa == this) ativa = null;
    }

    /// <summary>
    /// Encerra agora: fade-out do audio e dissolve de saida.
    /// </summary>
    public void Encerrar()
    {
        if (!sequenceStarted || encerrando) return;
        if (sequencia != null) StopCoroutine(sequencia);
        StartCoroutine(SaidaRapida());
    }

    IEnumerator SaidaRapida()
    {
        encerrando = true;
        Log("Encerrando (fade-out)");

        float volIni = audioSource != null ? audioSource.volume : 0f;
        float from = (dissolveMat != null && dissolveMat.HasProperty(DissolveAmount)) ? dissolveMat.GetFloat(DissolveAmount) : 1f;
        float dur = Mathf.Max(FadeAudio, useDissolve ? dissolveDuration : 0f);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            if (audioSource != null && audioSource.isPlaying)
                audioSource.volume = Mathf.Lerp(volIni, 0f, Mathf.Clamp01(t / FadeAudio));
            if (useDissolve && dissolveDuration > 0f)
                SetDissolve(Mathf.Lerp(from, 1f, Mathf.Clamp01(t / dissolveDuration)));
            yield return null;
        }

        if (audioSource != null) { audioSource.Stop(); audioSource.volume = volume; }
        SetDissolve(1f);
        FinalizarSequencia();
    }

    void FinalizarSequencia()
    {
        if (characterInstance != null) Destroy(characterInstance);
        characterInstance = null;
        dissolveMat = null;
        encerrando = false;
        sequencia = null;
        if (ativa == this) ativa = null;
        // Com a opcao ligada, pode aparecer de novo quando o player voltar
        if (sairAoDeixarTrigger && !aparecerSoUmaVez) sequenceStarted = false;
    }

    IEnumerator FullSequence()
    {
        Log("Iniciando sequência");

        if (characterPrefab == null)
        {
            Log("Character Prefab está vazio!");
            yield break;
        }

        // Instancia o personagem
        Vector3 pos = spawnPoint != null ? spawnPoint.position : characterPrefab.transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : characterPrefab.transform.rotation;
        characterInstance = Instantiate(characterPrefab, pos, rot);
        characterInstance.SetActive(true);

        Log("Instanciado: " + characterInstance.name);

        // Pega o Renderer da instância
        Renderer rend = characterInstance.GetComponentInChildren<Renderer>(true);
        Log("Renderer: " + (rend != null ? rend.name : "NULL"));

        if (rend != null)
        {
            dissolveMat = rend.material;
            Log("Material: " + dissolveMat.name);
            Log("Tem _DissolveAmount: " + dissolveMat.HasProperty(DissolveAmount));
        }

        SetDissolve(1f);
        SetDirection();
        yield return null;

        // Dissolve entrada
        if (useDissolve && dissolveMat != null && dissolveMat.HasProperty(DissolveAmount))
        {
            Log("Dissolve entrada 1?0");
            yield return StartCoroutine(Dissolve(1f, 0f));
        }
        else
        {
            Log("Sem dissolve. Mostrando direto.");
            SetDissolve(0f);
        }

        // Áudio
        AudioClip clip = (Idioma.Ingles && voiceClipEng != null) ? voiceClipEng : voiceClip;
        if (clip != null && audioSource != null)
        {
            Log("Tocando áudio: " + clip.name);
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.Play();
            yield return new WaitForSeconds(clip.length);
        }

        if (objectToActivate != null)
            objectToActivate.SetActive(true);

        if (waitAfterAudio > 0f)
            yield return new WaitForSeconds(waitAfterAudio);

        // Dissolve saída
        if (useDissolve && dissolveMat != null && dissolveMat.HasProperty(DissolveAmount))
        {
            Log("Dissolve saída 0?1");
            yield return StartCoroutine(Dissolve(0f, 1f));
        }

        Log("Destruindo instância");
        FinalizarSequencia();
    }

    IEnumerator Dissolve(float from, float to)
    {
        if (dissolveMat == null || !dissolveMat.HasProperty(DissolveAmount))
            yield break;

        if (dissolveDuration <= 0f)
        {
            SetDissolve(to);
            yield break;
        }

        float elapsed = 0f;
        SetDissolve(from);

        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dissolveDuration);
            SetDissolve(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetDissolve(to);
        Log("Dissolve terminou em " + to);
    }

    void SetDissolve(float value)
    {
        if (dissolveMat != null && dissolveMat.HasProperty(DissolveAmount))
            dissolveMat.SetFloat(DissolveAmount, value);
    }

    void SetDirection()
    {
        if (dissolveMat != null && dissolveMat.HasProperty(Direction))
            dissolveMat.SetFloat(Direction, dissolveFromBottom ? 1f : -1f);
    }

#if UNITY_EDITOR
    // Procura automaticamente o audio <nome>_eng na mesma pasta
    void OnValidate()
    {
        if (voiceClip == null) return;
        string path = UnityEditor.AssetDatabase.GetAssetPath(voiceClip);
        if (string.IsNullOrEmpty(path)) return;
        string dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        foreach (string g in UnityEditor.AssetDatabase.FindAssets(voiceClip.name + "_eng t:AudioClip", new[] { dir }))
        {
            string ap = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(ap).Equals(voiceClip.name + "_eng", System.StringComparison.OrdinalIgnoreCase))
            {
                var c = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(ap);
                if (c != voiceClipEng) { voiceClipEng = c; UnityEditor.EditorUtility.SetDirty(this); }
                return;
            }
        }
    }
#endif

    void Log(string msg)
    {
        if (showLogs)
            Debug.Log("[Dissolve] " + msg, this);
    }
}
