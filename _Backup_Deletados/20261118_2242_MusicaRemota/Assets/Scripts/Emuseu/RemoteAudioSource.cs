using UnityEngine;

// Troca o clip de um AudioSource (musica ambiente, torcida etc.) pelo audio do site.
// Coloque no MESMO GameObject do AudioSource. Se o AudioSource estava tocando, reinicia com o clip novo.
// Sem audio remoto (key ausente, erro, offline) mantem o clip do build.
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class RemoteAudioSource : MonoBehaviour
{
    [Tooltip("Key do site (tipo audio). Ex: music_ambiente")]
    public string key = "";

    [Tooltip("So usado se 'Por Idioma' estiver ligado. Vazia = key + \"_en\".")]
    public string keyEn = "";

    [Tooltip("Ligado = troca o clip conforme o idioma do jogo (PT/EN). Desligado = sempre a key (ex: musica).")]
    public bool porIdioma = false;

    public bool showLogs = true;

    AudioSource fonte;
    AudioClip clipBuild;
    bool guardou, trocou;

    void Awake()
    {
        fonte = GetComponent<AudioSource>();
        Guardar();
    }

    void OnEnable()
    {
        if (fonte == null) fonte = GetComponent<AudioSource>();
        Guardar();
        EmuseuSettingsCatalog.Changed += AoMudarCatalogo;
        Idioma.OnMudou += AoMudarIdioma;
        Atualizar();
    }

    void OnDisable()
    {
        EmuseuSettingsCatalog.Changed -= AoMudarCatalogo;
        Idioma.OnMudou -= AoMudarIdioma;
    }

    void Guardar()
    {
        if (guardou || fonte == null) return;
        clipBuild = fonte.clip;
        guardou = true;
    }

    void AoMudarCatalogo(string k)
    {
        if (k != null && k != key && k != RemoteAudioCatalog.ChaveEn(key, keyEn)) return;
        Atualizar();
    }

    void AoMudarIdioma(bool ingles)
    {
        if (porIdioma) Atualizar();
    }

    void Atualizar()
    {
        if (fonte == null || string.IsNullOrWhiteSpace(key)) return;
        bool ingles = porIdioma && Idioma.Ingles;

        RemoteAudioCatalog.Pedir(key, keyEn, ingles, clip =>
        {
            if (this == null || fonte == null) return;
            if (clip != null) Aplicar(clip, true);
            else if (trocou) Aplicar(clipBuild, false);   // remoto sumiu: volta para o do build
            // sem remoto e nunca trocou: nao mexe em nada (fica o clip do build)
        });
    }

    void Aplicar(AudioClip novo, bool remoto)
    {
        if (fonte.clip == novo) { trocou = remoto; return; }
        bool tocando = fonte.isPlaying;
        fonte.clip = novo;
        trocou = remoto;
        if (tocando && novo != null) fonte.Play();   // reinicia com o clip novo
        if (showLogs) Debug.Log("[RemoteAudioSource:" + key + "] " + (remoto ? "clip remoto aplicado" : "voltou para o clip do build") + (tocando ? " (reiniciado)" : ""), this);
    }
}