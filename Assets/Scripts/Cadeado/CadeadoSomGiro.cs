using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toca um "tic" suave cada vez que um disco do cadeado gira.
/// Coloque este componente no mesmo objeto do LockDoor, CadeadoDials,
/// Enigma4Dials ou Enigma5Manager. Os discos sao preenchidos sozinhos.
/// Nao altera os scripts originais: observa o indice (quando publico)
/// ou a rotacao local dos discos.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Cadeado/Som de Giro")]
public class CadeadoSomGiro : MonoBehaviour
{
    [Header("Sons")]
    [Tooltip("Variacoes do tic. Se vazio, o Editor tenta achar Cadeado_Tick_ automaticamente.")]
    public AudioClip[] clips;

    [Range(0f, 1f)]
    [Tooltip("Volume de cada tic.")]
    public float volume = 0.35f;

    [Tooltip("Variacao aleatoria de tom (x = minimo, y = maximo).")]
    public Vector2 tomAleatorio = new Vector2(0.95f, 1.05f);

    [Min(0f)]
    [Tooltip("Tempo minimo entre tics, para nao empilhar o som.")]
    public float intervaloMinimo = 0.05f;

    [Header("Som 2D / 3D")]
    [Range(0f, 1f)]
    [Tooltip("0 = 2D (recomendado no WebGL). 1 = 3D espacial.")]
    public float misturaEspacial = 0f;

    [Header("Discos")]
    [Tooltip("Discos que giram. Vazio = preenche sozinho a partir do script do cadeado ou dos filhos com nome Dial/Disco.")]
    public Transform[] aneis;

    [Min(0.1f)]
    [Tooltip("Graus de giro, desde a posicao parada, para disparar o tic (modo reserva, quando nao ha indice publico).")]
    public float limiarGraus = 8f;

    [Tooltip("Se o script original ja tiver um clickSound preenchido, este componente nao toca (evita som em dobro).")]
    public bool ignorarSeJaTemClickSound = true;

    AudioSource _fonte;
    float _proximoHorario;
    bool _silenciado;
    bool _pronto;
    int _framesIgnorados;

    LockDoor _lockDoor;
    CadeadoDials _cadeadoDials;
    Enigma4Dials _enigma4;
    Enigma5Manager _enigma5;

    int _ultimoIndexDial1 = int.MinValue;
    int _ultimoIndexDial2 = int.MinValue;
    int[] _ultimosNumeros;

    Quaternion[] _rotParada;
    Quaternion[] _rotAnterior;
    bool[] _girando;
    float[] _paradoHa;

    const float VelocidadeParadaGraus = 0.35f;
    const float TempoParaConsiderarParado = 0.05f;

    void Reset()
    {
        volume = 0.35f;
        tomAleatorio = new Vector2(0.95f, 1.05f);
        intervaloMinimo = 0.05f;
        misturaEspacial = 0f;
        limiarGraus = 8f;
        ignorarSeJaTemClickSound = true;
        ConfigurarFonte();
        EncontrarScripts();
        PreencherAneisSeVazio();
#if UNITY_EDITOR
        PreencherClipsNoEditor();
#endif
    }

    void Awake()
    {
        ConfigurarFonte();
        EncontrarScripts();
        if (ignorarSeJaTemClickSound && JaTemClickSound())
            _silenciado = true;
        PreencherAneisSeVazio();
    }

    void Start()
    {
        InicializarEstado();
        _framesIgnorados = 2;
        _pronto = true;
    }

    void LateUpdate()
    {
        if (_silenciado || !_pronto || aneis == null || aneis.Length == 0)
            return;

        if (_framesIgnorados > 0)
        {
            _framesIgnorados--;
            AtualizarSnapshotsSemTocar();
            return;
        }

        bool tocouPorIndice = ObservarIndices();
        if (!tocouPorIndice)
            ObservarRotacao();
        else
            MarcarAneisComoGirando();
    }

    /// <summary>Toca um tic (pode ser chamado por um evento, se quiser).</summary>
    public void TocarGiro()
    {
        if (clips == null || clips.Length == 0 || _fonte == null)
            return;
        if (Time.unscaledTime < _proximoHorario)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
            return;

        float minP = Mathf.Min(tomAleatorio.x, tomAleatorio.y);
        float maxP = Mathf.Max(tomAleatorio.x, tomAleatorio.y);
        _fonte.pitch = Random.Range(minP, maxP);
        _fonte.spatialBlend = misturaEspacial;
        _fonte.PlayOneShot(clip, volume);
        _proximoHorario = Time.unscaledTime + intervaloMinimo;
    }

    void ConfigurarFonte()
    {
        _fonte = GetComponent<AudioSource>();
        if (_fonte == null)
            _fonte = gameObject.AddComponent<AudioSource>();
        _fonte.playOnAwake = false;
        _fonte.loop = false;
        _fonte.spatialBlend = misturaEspacial;
        _fonte.dopplerLevel = 0f;
    }

    void EncontrarScripts()
    {
        _lockDoor = EncontrarProximo<LockDoor>();
        _cadeadoDials = EncontrarProximo<CadeadoDials>();
        _enigma4 = EncontrarProximo<Enigma4Dials>();
        _enigma5 = EncontrarProximo<Enigma5Manager>();
    }

    T EncontrarProximo<T>() where T : Component
    {
        T c = GetComponent<T>();
        if (c != null) return c;
        c = GetComponentInParent<T>();
        if (c != null) return c;
        return GetComponentInChildren<T>(true);
    }

    bool JaTemClickSound()
    {
        if (_cadeadoDials != null && _cadeadoDials.clickSound != null) return true;
        if (_enigma4 != null && _enigma4.clickSound != null) return true;
        if (_enigma5 != null && _enigma5.clickSound != null) return true;
        return false;
    }

    void PreencherAneisSeVazio()
    {
        if (TemAneis()) return;

        var lista = new List<Transform>();
        AdicionarUnico(lista, _lockDoor != null ? _lockDoor.dial1 : null);
        AdicionarUnico(lista, _lockDoor != null ? _lockDoor.dial2 : null);

        if (_cadeadoDials != null && _cadeadoDials.dials != null)
        {
            for (int i = 0; i < _cadeadoDials.dials.Length; i++)
            {
                if (_cadeadoDials.dials[i] != null)
                    AdicionarUnico(lista, _cadeadoDials.dials[i].disco);
            }
        }

        AdicionarUnico(lista, _enigma4 != null ? _enigma4.dial1 : null);
        AdicionarUnico(lista, _enigma4 != null ? _enigma4.dial2 : null);
        AdicionarUnico(lista, _enigma5 != null ? _enigma5.dial1 : null);
        AdicionarUnico(lista, _enigma5 != null ? _enigma5.dial2 : null);

        if (lista.Count == 0)
        {
            Transform raiz = transform;
            if (_lockDoor != null) raiz = _lockDoor.transform;
            else if (_cadeadoDials != null) raiz = _cadeadoDials.transform.root;
            else if (_enigma4 != null) raiz = _enigma4.transform.root;
            else if (_enigma5 != null) raiz = _enigma5.transform.root;

            var todos = raiz.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < todos.Length; i++)
            {
                Transform t = todos[i];
                if (t == null || t == raiz) continue;
                string n = t.name;
                if (NomePareceDisco(n))
                    AdicionarUnico(lista, t);
            }
        }

        aneis = lista.ToArray();
        if (aneis.Length == 0)
            Debug.LogWarning("[CadeadoSomGiro] Nenhum disco encontrado em " + name + ". Arraste os Dials no campo Aneis.", this);
    }

    static bool NomePareceDisco(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        string s = n.ToLowerInvariant();
        return s.Contains("dial") || s.Contains("disco");
    }

    static void AdicionarUnico(List<Transform> lista, Transform t)
    {
        if (t == null) return;
        if (!lista.Contains(t)) lista.Add(t);
    }

    bool TemAneis()
    {
        if (aneis == null || aneis.Length == 0) return false;
        for (int i = 0; i < aneis.Length; i++)
            if (aneis[i] != null) return true;
        return false;
    }

    void InicializarEstado()
    {
        int n = aneis != null ? aneis.Length : 0;
        _rotParada = new Quaternion[n];
        _rotAnterior = new Quaternion[n];
        _girando = new bool[n];
        _paradoHa = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (aneis[i] == null) continue;
            _rotParada[i] = aneis[i].localRotation;
            _rotAnterior[i] = aneis[i].localRotation;
        }

        if (_lockDoor != null)
        {
            _ultimoIndexDial1 = _lockDoor.indexDial1;
            _ultimoIndexDial2 = _lockDoor.indexDial2;
        }

        if (_cadeadoDials != null && _cadeadoDials.dials != null)
        {
            _ultimosNumeros = new int[_cadeadoDials.dials.Length];
            for (int i = 0; i < _cadeadoDials.dials.Length; i++)
            {
                if (_cadeadoDials.dials[i] != null)
                    _ultimosNumeros[i] = _cadeadoDials.dials[i].numeroAtual;
            }
        }
    }

    void AtualizarSnapshotsSemTocar()
    {
        if (_lockDoor != null)
        {
            _ultimoIndexDial1 = _lockDoor.indexDial1;
            _ultimoIndexDial2 = _lockDoor.indexDial2;
        }
        if (_cadeadoDials != null && _cadeadoDials.dials != null && _ultimosNumeros != null)
        {
            int n = Mathf.Min(_ultimosNumeros.Length, _cadeadoDials.dials.Length);
            for (int i = 0; i < n; i++)
            {
                if (_cadeadoDials.dials[i] != null)
                    _ultimosNumeros[i] = _cadeadoDials.dials[i].numeroAtual;
            }
        }
        if (aneis == null) return;
        for (int i = 0; i < aneis.Length; i++)
        {
            if (aneis[i] == null) continue;
            _rotParada[i] = aneis[i].localRotation;
            _rotAnterior[i] = aneis[i].localRotation;
            _girando[i] = false;
            _paradoHa[i] = 0f;
        }
    }

    bool ObservarIndices()
    {
        bool tocou = false;
        if (_lockDoor != null)
        {
            if (_lockDoor.indexDial1 != _ultimoIndexDial1)
            {
                TocarGiro();
                tocou = true;
            }
            if (_lockDoor.indexDial2 != _ultimoIndexDial2)
            {
                TocarGiro();
                tocou = true;
            }
            _ultimoIndexDial1 = _lockDoor.indexDial1;
            _ultimoIndexDial2 = _lockDoor.indexDial2;
        }

        if (_cadeadoDials != null && _cadeadoDials.dials != null)
        {
            if (_ultimosNumeros == null || _ultimosNumeros.Length != _cadeadoDials.dials.Length)
                InicializarEstado();
            for (int i = 0; i < _cadeadoDials.dials.Length; i++)
            {
                var d = _cadeadoDials.dials[i];
                if (d == null) continue;
                if (d.numeroAtual != _ultimosNumeros[i])
                {
                    TocarGiro();
                    tocou = true;
                    _ultimosNumeros[i] = d.numeroAtual;
                }
            }
        }

        return tocou;
    }

    void ObservarRotacao()
    {
        if (aneis == null || _rotParada == null) return;
        int n = Mathf.Min(aneis.Length, _rotParada.Length);
        for (int i = 0; i < n; i++)
        {
            Transform anel = aneis[i];
            if (anel == null) continue;

            Quaternion atual = anel.localRotation;
            float vel = Quaternion.Angle(_rotAnterior[i], atual);

            if (!_girando[i])
            {
                if (Quaternion.Angle(_rotParada[i], atual) >= limiarGraus)
                {
                    TocarGiro();
                    _girando[i] = true;
                    _paradoHa[i] = 0f;
                }
            }
            else
            {
                if (vel < VelocidadeParadaGraus)
                {
                    _paradoHa[i] += Time.unscaledDeltaTime;
                    if (_paradoHa[i] >= TempoParaConsiderarParado)
                    {
                        _girando[i] = false;
                        _rotParada[i] = atual;
                        _paradoHa[i] = 0f;
                    }
                }
                else
                {
                    _paradoHa[i] = 0f;
                }
            }

            _rotAnterior[i] = atual;
        }
    }

    void MarcarAneisComoGirando()
    {
        if (aneis == null || _girando == null) return;
        int n = Mathf.Min(aneis.Length, _girando.Length);
        for (int i = 0; i < n; i++)
        {
            if (aneis[i] == null) continue;
            _girando[i] = true;
            _paradoHa[i] = 0f;
            _rotAnterior[i] = aneis[i].localRotation;
        }
    }

#if UNITY_EDITOR
    void PreencherClipsNoEditor()
    {
        if (TemClips()) return;
        string[] guids = UnityEditor.AssetDatabase.FindAssets("Cadeado_Tick_ t:AudioClip");
        if (guids == null || guids.Length == 0) return;
        var encontrados = new List<AudioClip>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) encontrados.Add(clip);
        }
        encontrados.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (encontrados.Count > 0)
            clips = encontrados.ToArray();
    }

    bool TemClips()
    {
        if (clips == null || clips.Length == 0) return false;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null) return true;
        return false;
    }

    void OnValidate()
    {
        if (tomAleatorio.x <= 0f) tomAleatorio.x = 0.95f;
        if (tomAleatorio.y <= 0f) tomAleatorio.y = 1.05f;
        if (intervaloMinimo < 0f) intervaloMinimo = 0f;
        if (Application.isPlaying) return;
        if (!TemClips())
            PreencherClipsNoEditor();
    }
#endif
}
