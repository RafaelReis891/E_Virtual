using UnityEngine;

// Troca a voz da Anfitria (CharacterAppearDissolve) pela do site.
// Coloque NO MESMO GameObject do CharacterAppearDissolve. Nao cria AudioSource: so troca voiceClip / voiceClipEng,
// entao continua tocando um unico audio. Sem audio remoto (sem key no site, erro, offline) mantem os clips do build.
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAppearDissolve))]
public sealed class RemoteVoiceClip : MonoBehaviour
{
    [Tooltip("Key do site (PT). Ex: voz_anfitrian_teste")]
    public string key = "";

    [Tooltip("Key do site para o EN (audio). Vazia = tenta key + \"_en\".")]
    public string keyEn = "";

    public bool showLogs = true;

    CharacterAppearDissolve alvo;
    AudioClip clipPtBuild, clipEnBuild;
    bool guardou, trocouPt, trocouEn;

    void Awake()
    {
        alvo = GetComponent<CharacterAppearDissolve>();
        Guardar();
    }

    void OnEnable()
    {
        if (alvo == null) alvo = GetComponent<CharacterAppearDissolve>();
        Guardar();
        EmuseuSettingsCatalog.Changed += AoMudarCatalogo;
        Atualizar();
    }

    void OnDisable()
    {
        EmuseuSettingsCatalog.Changed -= AoMudarCatalogo;
    }

    // Guarda os clips do build uma unica vez (antes de qualquer troca).
    void Guardar()
    {
        if (guardou || alvo == null) return;
        clipPtBuild = alvo.voiceClip;
        clipEnBuild = alvo.voiceClipEng;
        guardou = true;
    }

    void AoMudarCatalogo(string k)
    {
        // k == null: payload novo (ou idioma mudou). Senao so a key alterada.
        if (k != null && k != key && k != RemoteAudioCatalog.ChaveEn(key, keyEn)) return;
        Atualizar();
    }

    void Atualizar()
    {
        if (alvo == null || string.IsNullOrWhiteSpace(key)) return;

        // Pre-carrega PT e EN (a troca de idioma fica instantanea).
        RemoteAudioCatalog.Pedir(key, keyEn, false, clip =>
        {
            if (this == null || alvo == null) return;
            if (clip != null) { alvo.voiceClip = clip; trocouPt = true; Log("PT remoto aplicado (" + clip.length.ToString("0.00") + "s)"); }
            else if (trocouPt) { alvo.voiceClip = clipPtBuild; trocouPt = false; Log("PT voltou para o clip do build"); }
        });

        RemoteAudioCatalog.Pedir(key, keyEn, true, clip =>
        {
            if (this == null || alvo == null) return;
            if (clip != null) { alvo.voiceClipEng = clip; trocouEn = true; Log("EN remoto aplicado (" + clip.length.ToString("0.00") + "s)"); }
            else if (trocouEn) { alvo.voiceClipEng = clipEnBuild; trocouEn = false; Log("EN voltou para o clip do build"); }
        });
    }

    void Log(string msg)
    {
        if (showLogs) Debug.Log("[RemoteVoiceClip:" + key + "] " + msg, this);
    }
}