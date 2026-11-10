using System.Collections;
using UnityEngine;

// Troca a voz da Anfitria (CharacterAppearDissolve) pelo audio do payload, no idioma do site.
// O OGG do build so entra quando o idioma atual nao tem audio definido. Trocar o idioma troca o clipe
// se o novo tiver audio; senao o clipe que ja esta em uso continua.
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
    AudioClip clipPtBuild, clipEnBuild, clipEmUso;
    bool guardou, decidido;
    int geracao;

    public AudioClip ClipEmUso { get { return clipEmUso; } }

    // A fala espera aqui. Enquanto o payload nao chegou, ou o audio do idioma ainda baixa, nao decide.
    public IEnumerator EsperarClip()
    {
        while (!decidido)
            yield return null;
    }

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
        if (guardou || alvo == null) return;
        clipPtBuild = alvo.voiceClip;
        clipEnBuild = alvo.voiceClipEng;
        guardou = true;
    }

    void AoMudarCatalogo(string k)
    {
        if (k != null && k != key && k != RemoteAudioCatalog.ChaveEn(key, keyEn)) return;
        Atualizar();
    }

    void AoMudarIdioma(bool ingles)
    {
        Atualizar();
    }

    void Atualizar()
    {
        if (alvo == null) return;
        if (string.IsNullOrWhiteSpace(key))
        {
            if (clipEmUso == null) clipEmUso = ClipBuild(Idioma.Ingles);
            decidido = true;
            return;
        }
        // Sem payload ainda: nao tratar como "sem audio".
        if (EmuseuBridge.PlayConfig == null) return;

        bool ingles = Idioma.Ingles;
        int pedido = ++geracao;
        string url;
        if (!RemoteAudioCatalog.TryResolverUrl(key, keyEn, ingles, out url))
        {
            if (clipEmUso == null)
            {
                clipEmUso = ClipBuild(ingles);
                Log("sem audio no payload, usando OGG do " + (ingles ? "EN" : "PT"));
            }
            else
                Log("idioma " + (ingles ? "EN" : "PT") + " sem audio, mantendo o clipe atual");
            decidido = true;
            return;
        }

        if (clipEmUso == null) decidido = false;
        RemoteAudioCatalog.Pedir(key, keyEn, ingles, clip =>
        {
            if (this == null || pedido != geracao) return;
            if (clip != null)
            {
                clipEmUso = clip;
                Log((ingles ? "EN" : "PT") + " remoto aplicado (" + clip.length.ToString("0.00") + "s)");
                ReiniciarSeTocando(clip);
            }
            else if (clipEmUso == null)
            {
                clipEmUso = ClipBuild(ingles);
                Log("download falhou, usando OGG do " + (ingles ? "EN" : "PT"));
            }
            else
                Log("download falhou, mantendo o clipe atual");
            decidido = true;
        });
    }

    AudioClip ClipBuild(bool ingles)
    {
        if (ingles && clipEnBuild != null) return clipEnBuild;
        return clipPtBuild;
    }

    void ReiniciarSeTocando(AudioClip novo)
    {
        AudioSource fonte = GetComponent<AudioSource>();
        if (fonte == null || novo == null || !fonte.isPlaying || fonte.clip == novo) return;
        fonte.clip = novo;
        fonte.Play();
        Log("fala reiniciada no idioma atual");
    }

    void Log(string msg)
    {
        if (showLogs) Debug.Log("[RemoteVoiceClip:" + key + "] " + msg, this);
    }
}
