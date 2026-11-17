// CapturaFrame.cs
// Ferramenta de captura de um frame em alta resolucao, SOMENTE no Editor (nao entra no WebGL).
// Ative em Tools > Captura > Ativar captura de frame. Ao ligar, sobe a qualidade do render;
// ao desligar ou sair do Play, restaura tudo como estava (o asset URP no disco nao e modificado).
// Teclas (clique antes na Game view): P ou Pause trava a cinematica, F12 salva o PNG,
// ponto avanca um frame, virgula tenta voltar um frame.
// O PNG vai para <projeto>/Capturas/. Logs com prefixo [CapturaFrame].
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(10000)]
public sealed class CapturaFrame : MonoBehaviour
{
    public const string TagLog = "[CapturaFrame] ";

    public const string PrefAtivo = "CapturaFrame.Ativo";
    public const string PrefLargura = "CapturaFrame.Largura";
    public const string PrefAltura = "CapturaFrame.Altura";
    public const string PrefIncluirUI = "CapturaFrame.IncluirUI";
    public const string PrefTransparente = "CapturaFrame.Transparente";
    public const string PrefQualidadePC = "CapturaFrame.QualidadePC";
    public const string PrefAntiAliasing = "CapturaFrame.AntiAliasing";
    public const string PrefUsarTaa = "CapturaFrame.UsarTaa";
    public const string PrefSombrasURP = "CapturaFrame.SombrasURP";
    public const string PrefDesligarMobile = "CapturaFrame.DesligarMobile";
    public const string PrefLodBias = "CapturaFrame.LodBias";

    public const string PrefPrecisaRestaurar = "CapturaFrame.PrecisaRestaurar";
    public const string PrefNivelOrig = "CapturaFrame.NivelOrig";
    public const string PrefLodOrig = "CapturaFrame.LodOrig";
    public const string PrefMaxLodOrig = "CapturaFrame.MaxLodOrig";
    public const string PrefFpsOrig = "CapturaFrame.FpsOrig";
    public const string PrefVSyncOrig = "CapturaFrame.VSyncOrig";
    public const string PrefPipelineOrig = "CapturaFrame.PipelineOrig";

    public const int Largura4K = 3840;
    public const int Altura4K = 2160;
    public const int Largura8K = 7680;
    public const int Altura8K = 4320;

    const float LodBiasAlvo = 4f;
    const float DistanciaSombraAlvo = 150f;
    const int ResolucaoSombraAlvo = 4096;

    static CapturaFrame atual;
    static bool qualidadeAplicada;
    static int nivelOriginal;
    static float lodBiasOriginal;
    static int maxLodOriginal;
    static int targetFpsOriginal;
    static int vSyncOriginal;
    static RenderPipelineAsset pipelineOriginal;
    static UniversalRenderPipelineAsset pipelineClone;

    struct CameraAaBackup
    {
        public UniversalAdditionalCameraData dados;
        public AntialiasingMode modo;
        public AntialiasingQuality qualidade;
    }

    static readonly List<CameraAaBackup> camerasAa = new List<CameraAaBackup>();

    struct AnimatorBackup
    {
        public Animator anim;
        public float speed;
        public bool enabled;
    }

    struct DirectorBackup
    {
        public PlayableDirector dir;
        public PlayState estado;
        public DirectorWrapMode wrap;
        public double time;
    }

    struct AnimacaoBackup
    {
        public Animation anim;
        public List<float> speeds;
    }

    struct CanvasBackup
    {
        public Canvas canvas;
        public RenderMode modo;
        public Camera worldCamera;
        public float planeDistance;
    }

    bool pausado;
    float timeScaleAnterior = 1f;
    bool audioPauseAnterior;
    readonly List<AnimatorBackup> animators = new List<AnimatorBackup>();
    readonly List<DirectorBackup> directors = new List<DirectorBackup>();
    readonly List<AnimacaoBackup> animacoes = new List<AnimacaoBackup>();
    string aviso = "";
    float avisoAte;
    string ultimoArquivo = "";
    bool capturando;
    bool avancandoFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstalarAutomatico()
    {
        if (!EditorPrefs.GetBool(PrefAtivo, false)) return;
        InstalarSePreciso();
    }

    public static int Largura => Mathf.Max(16, EditorPrefs.GetInt(PrefLargura, Largura4K));
    public static int Altura => Mathf.Max(16, EditorPrefs.GetInt(PrefAltura, Altura4K));
    public static bool IncluirUI => EditorPrefs.GetBool(PrefIncluirUI, false);
    public static bool FundoTransparente => EditorPrefs.GetBool(PrefTransparente, false);

    public static string PastaCapturas
    {
        get
        {
            string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Capturas"));
            if (!Directory.Exists(pasta)) Directory.CreateDirectory(pasta);
            return pasta;
        }
    }

    public static void DefinirResolucao(int w, int h)
    {
        w = Mathf.Clamp(w, 16, 15360);
        h = Mathf.Clamp(h, 16, 15360);
        EditorPrefs.SetInt(PrefLargura, w);
        EditorPrefs.SetInt(PrefAltura, h);
        Debug.Log(TagLog + "resolucao = " + w + "x" + h);
    }

    public static void AoMudarAtivo(bool ligado)
    {
        if (!Application.isPlaying)
        {
            if (!ligado) RestaurarQualidadeSePreciso();
            Debug.Log(TagLog + (ligado
                ? "LIGADO (vale no proximo Play). P pausa, F12 captura, . avanca um frame."
                : "DESLIGADO."));
            return;
        }

        if (ligado)
        {
            InstalarSePreciso();
            AplicarQualidade();
            Debug.Log(TagLog + "LIGADO. Clique na Game view: P pausa, F12 salva o frame.");
        }
        else
        {
            if (atual != null)
            {
                if (atual.pausado) atual.DefinirPausa(false);
                Destroy(atual.gameObject);
                atual = null;
            }
            RestaurarQualidadeSePreciso();
            Debug.Log(TagLog + "DESLIGADO e qualidade restaurada.");
        }
    }

    public static void InstalarSePreciso()
    {
        if (atual != null) return;
        var existente = FindAnyObjectByType<CapturaFrame>();
        if (existente != null)
        {
            atual = existente;
            return;
        }
        var go = new GameObject("[CapturaFrame]");
        atual = go.AddComponent<CapturaFrame>();
        DontDestroyOnLoad(go);
        Debug.Log(TagLog + "pronto. Clique na Game view: P pausa, F12 salva o frame, . avanca um frame.");
    }

    public static void PedirCaptura()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(TagLog + "entre em Play para capturar um frame.");
            return;
        }
        InstalarSePreciso();
        if (atual != null) atual.StartCoroutine(atual.CapturarRotina());
    }

    public static void PedirPausa()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(TagLog + "entre em Play para pausar a cinematica.");
            return;
        }
        InstalarSePreciso();
        if (atual != null) atual.AlternarPausa();
    }

    public static void PedirAvanco(int direcao)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(TagLog + "entre em Play para avancar um frame.");
            return;
        }
        InstalarSePreciso();
        if (atual != null) atual.StartCoroutine(atual.AvancarFrameRotina(direcao));
    }

    void OnEnable()
    {
        atual = this;
        SceneManager.sceneLoaded += AoCarregarCena;
        if (EditorPrefs.GetBool(PrefAtivo, false)) AplicarQualidade();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        if (pausado) DefinirPausa(false);
        if (atual == this) atual = null;
    }

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (!EditorPrefs.GetBool(PrefAtivo, false)) return;
        // MobileRenderRuntime tambem usa sceneLoaded; o delayCall roda depois e reaplica AA/qualidade.
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(PrefAtivo, false)) AplicarQualidade();
        };
    }

    void Update()
    {
        if (!EditorPrefs.GetBool(PrefAtivo, false)) return;
        if (capturando || avancandoFrame) return;
        if (TeclaApertouF12()) StartCoroutine(CapturarRotina());
        else if (TeclaApertouP() || TeclaApertouPause()) AlternarPausa();
        else if (TeclaApertouPonto()) StartCoroutine(AvancarFrameRotina(1));
        else if (TeclaApertouVirgula()) StartCoroutine(AvancarFrameRotina(-1));
    }

    void OnGUI()
    {
        if (!EditorPrefs.GetBool(PrefAtivo, false)) return;
        var stilo = new GUIStyle(GUI.skin.box);
        stilo.fontSize = 14;
        stilo.alignment = TextAnchor.MiddleLeft;
        stilo.wordWrap = true;
        stilo.normal.textColor = Color.white;
        stilo.padding = new RectOffset(10, 10, 8, 8);

        string texto = "Captura " + Largura + "x" + Altura;
        if (pausado) texto += "   |   PAUSADO  (P continua,  . avanca,  , volta)";
        texto += "   |   F12 salva PNG";
        if (!string.IsNullOrEmpty(ultimoArquivo)) texto += "\nUltimo: " + ultimoArquivo;
        if (Time.unscaledTime < avisoAte && !string.IsNullOrEmpty(aviso)) texto += "\n" + aviso;

        var pad = stilo.CalcSize(new GUIContent(texto));
        float w = Mathf.Min(Screen.width - 20, Mathf.Max(480, pad.x + 16));
        float h = pad.y + 16;
        GUI.Box(new Rect(10, 10, w, h), texto, stilo);
    }

    void MostrarAviso(string msg, float segundos)
    {
        aviso = msg;
        avisoAte = Time.unscaledTime + segundos;
        Debug.Log(TagLog + msg);
    }

    static bool TeclaApertouF12() => TeclaApertou(
#if ENABLE_INPUT_SYSTEM
        Key.F12,
#endif
        KeyCode.F12);

    static bool TeclaApertouP() => TeclaApertou(
#if ENABLE_INPUT_SYSTEM
        Key.P,
#endif
        KeyCode.P);

    static bool TeclaApertouPause() => TeclaApertou(
#if ENABLE_INPUT_SYSTEM
        Key.Pause,
#endif
        KeyCode.Pause) || TeclaApertouLegacy(KeyCode.Break);

    static bool TeclaApertouPonto() => TeclaApertou(
#if ENABLE_INPUT_SYSTEM
        Key.Period,
#endif
        KeyCode.Period);

    static bool TeclaApertouVirgula() => TeclaApertou(
#if ENABLE_INPUT_SYSTEM
        Key.Comma,
#endif
        KeyCode.Comma);

    static bool TeclaApertou(
#if ENABLE_INPUT_SYSTEM
        Key nova,
#endif
        KeyCode antiga)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current[nova].wasPressedThisFrame;
#endif
        return TeclaApertouLegacy(antiga);
    }

    static bool TeclaApertouLegacy(KeyCode antiga)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(antiga);
#else
        return false;
#endif
    }

    public void AlternarPausa()
    {
        DefinirPausa(!pausado);
    }

    void DefinirPausa(bool ligar)
    {
        if (ligar == pausado) return;
        if (ligar)
        {
            timeScaleAnterior = Time.timeScale;
            audioPauseAnterior = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            animators.Clear();
            foreach (var a in FindObjectsByType<Animator>(FindObjectsInactive.Include))
            {
                if (a == null) continue;
                animators.Add(new AnimatorBackup { anim = a, speed = a.speed, enabled = a.enabled });
                a.speed = 0f;
            }

            directors.Clear();
            foreach (var d in FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include))
            {
                if (d == null) continue;
                directors.Add(new DirectorBackup
                {
                    dir = d,
                    estado = d.state,
                    wrap = d.extrapolationMode,
                    time = d.time
                });
                if (d.state == PlayState.Playing) d.Pause();
            }

            animacoes.Clear();
            foreach (var an in FindObjectsByType<Animation>(FindObjectsInactive.Include))
            {
                if (an == null) continue;
                var speeds = new List<float>();
                foreach (AnimationState st in an)
                {
                    speeds.Add(st.speed);
                    st.speed = 0f;
                }
                animacoes.Add(new AnimacaoBackup { anim = an, speeds = speeds });
            }

            pausado = true;
            MostrarAviso("Cinematica pausada. F12 captura, P continua, . avanca um frame.", 4f);
        }
        else
        {
            Time.timeScale = timeScaleAnterior;
            AudioListener.pause = audioPauseAnterior;

            for (int i = 0; i < animators.Count; i++)
            {
                var b = animators[i];
                if (b.anim != null) b.anim.speed = b.speed;
            }
            for (int i = 0; i < directors.Count; i++)
            {
                var b = directors[i];
                if (b.dir == null) continue;
                if (b.estado == PlayState.Playing) b.dir.Play();
                else if (b.estado == PlayState.Paused) b.dir.Pause();
            }
            for (int i = 0; i < animacoes.Count; i++)
            {
                var b = animacoes[i];
                if (b.anim == null || b.speeds == null) continue;
                int k = 0;
                foreach (AnimationState st in b.anim)
                {
                    if (k < b.speeds.Count) st.speed = b.speeds[k];
                    k++;
                }
            }

            animators.Clear();
            directors.Clear();
            animacoes.Clear();
            pausado = false;
            MostrarAviso("Cinematica retomada.", 2f);
        }
    }

    IEnumerator AvancarFrameRotina(int direcao)
    {
        if (avancandoFrame || capturando) yield break;
        if (!pausado)
        {
            MostrarAviso("Pause com P antes de avancar frame a frame.", 3f);
            yield break;
        }

        avancandoFrame = true;
        float dt = (direcao < 0 ? -1f : 1f) * (1f / 60f);

        // Time.time nao volta: o recuo e melhor esforco so em Timeline/Animator.
        if (direcao < 0)
        {
            for (int i = 0; i < directors.Count; i++)
            {
                var b = directors[i];
                if (b.dir == null) continue;
                b.dir.time = Math.Max(0d, b.dir.time + dt);
                b.dir.Evaluate();
            }
            for (int i = 0; i < animators.Count; i++)
            {
                var b = animators[i];
                if (b.anim == null) continue;
                b.anim.Update(dt);
            }
            MostrarAviso("Voltou um frame (Animator/Timeline). Scripts que usam Time.time nao rebobinam.", 3f);
        }
        else
        {
            Time.timeScale = 1f;
            AudioListener.pause = audioPauseAnterior;
            for (int i = 0; i < animators.Count; i++)
            {
                var b = animators[i];
                if (b.anim != null) b.anim.speed = b.speed;
            }
            for (int i = 0; i < directors.Count; i++)
            {
                var b = directors[i];
                if (b.dir != null && b.estado == PlayState.Playing) b.dir.Play();
            }
            for (int i = 0; i < animacoes.Count; i++)
            {
                var b = animacoes[i];
                if (b.anim == null || b.speeds == null) continue;
                int k = 0;
                foreach (AnimationState st in b.anim)
                {
                    if (k < b.speeds.Count) st.speed = b.speeds[k];
                    k++;
                }
            }

            yield return new WaitForSecondsRealtime(0f);
            yield return new WaitForEndOfFrame();

            Time.timeScale = 0f;
            AudioListener.pause = true;
            for (int i = 0; i < animators.Count; i++)
            {
                var b = animators[i];
                if (b.anim != null) b.anim.speed = 0f;
            }
            for (int i = 0; i < directors.Count; i++)
            {
                var b = directors[i];
                if (b.dir != null && b.dir.state == PlayState.Playing) b.dir.Pause();
            }
            for (int i = 0; i < animacoes.Count; i++)
            {
                var b = animacoes[i];
                if (b.anim == null) continue;
                foreach (AnimationState st in b.anim) st.speed = 0f;
            }
            MostrarAviso("Avancou um frame.", 2f);
        }

        avancandoFrame = false;
    }

    IEnumerator CapturarRotina()
    {
        if (capturando) yield break;
        capturando = true;
        yield return new WaitForEndOfFrame();

        int w = Largura;
        int h = Altura;
        Camera cam = EscolherCamera();
        string pasta = PastaCapturas;
        string cena = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(cena)) cena = "sem_cena";
        string nome = "Frame_" + Sanitizar(cena) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + w + "x" + h + ".png";
        string caminho = Path.Combine(pasta, nome);

        bool ok = false;
        string erro = null;
        if (cam != null)
        {
            try
            {
                ok = RenderizarParaArquivo(cam, w, h, caminho);
            }
            catch (Exception e)
            {
                erro = e.Message;
                ok = false;
            }
        }

        if (!ok)
        {
            int super = Mathf.Max(1, Mathf.RoundToInt(w / (float)Mathf.Max(1, Screen.width)));
            Debug.LogWarning(TagLog + "render da camera falhou" + (cam == null ? " (sem camera ativa)" : (erro != null ? " (" + erro + ")" : ""))
                + ". Usando ScreenCapture com superSize=" + super + ".");
            try
            {
                ScreenCapture.CaptureScreenshot(caminho, super);
                ok = true;
                MostrarAviso("Fallback ScreenCapture: " + caminho, 6f);
            }
            catch (Exception e)
            {
                MostrarAviso("Falha ao salvar: " + e.Message, 6f);
                capturando = false;
                yield break;
            }
        }

        if (ok)
        {
            ultimoArquivo = caminho;
            MostrarAviso("Salvo: " + caminho, 8f);
        }

        capturando = false;
    }

    bool RenderizarParaArquivo(Camera cam, int w, int h, string caminho)
    {
        int msaa = w >= Largura8K ? 4 : 8;
        RenderTexture rt = CriarRT(w, h, msaa);
        if (rt == null && msaa > 1)
        {
            Debug.LogWarning(TagLog + "nao deu para alocar RT com MSAA " + msaa + ", tentando sem MSAA.");
            rt = CriarRT(w, h, 1);
        }
        if (rt == null) return false;

        var clearFlags = cam.clearFlags;
        var bg = cam.backgroundColor;
        var alvo = cam.targetTexture;
        var rect = cam.rect;
        int mask = cam.cullingMask;
        bool allowDyn = cam.allowDynamicResolution;

        List<CanvasBackup> canvases = null;
        if (IncluirUI) canvases = PrepararUI(cam);

        if (FundoTransparente)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        cam.allowDynamicResolution = false;
        cam.rect = new Rect(0f, 0f, 1f, 1f);
        cam.targetTexture = rt;

        try
        {
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            tex.Apply(false, false);
            RenderTexture.active = prev;

            byte[] png = tex.EncodeToPNG();
            DestroyImmediate(tex);
            File.WriteAllBytes(caminho, png);
            return File.Exists(caminho) && png != null && png.Length > 0;
        }
        finally
        {
            cam.targetTexture = alvo;
            cam.rect = rect;
            cam.allowDynamicResolution = allowDyn;
            cam.clearFlags = clearFlags;
            cam.backgroundColor = bg;
            cam.cullingMask = mask;
            if (canvases != null) RestaurarUI(canvases);
            rt.Release();
            DestroyImmediate(rt);
        }
    }

    static RenderTexture CriarRT(int w, int h, int msaa)
    {
        try
        {
            var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24);
            desc.msaaSamples = msaa;
            desc.sRGB = true;
            desc.autoGenerateMips = false;
            desc.useMipMap = false;
            var rt = new RenderTexture(desc)
            {
                name = "CapturaFrameRT",
                antiAliasing = msaa,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!rt.Create())
            {
                DestroyImmediate(rt);
                return null;
            }
            return rt;
        }
        catch (Exception e)
        {
            Debug.LogWarning(TagLog + "falha ao criar RenderTexture " + w + "x" + h + " MSAA " + msaa + ": " + e.Message);
            return null;
        }
    }

    static Camera EscolherCamera()
    {
        Camera intro = null;
        Camera melhor = null;
        float melhorDepth = float.NegativeInfinity;
        var cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++)
        {
            var c = cams[i];
            if (c == null || !c.isActiveAndEnabled) continue;
            var dados = c.GetComponent<UniversalAdditionalCameraData>();
            if (dados != null && dados.renderType == CameraRenderType.Overlay) continue;
            string n = c.name ?? "";
            if (n.StartsWith("CameraView", StringComparison.OrdinalIgnoreCase) ||
                n.IndexOf("Intro", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                intro = c;
            }
            if (c.depth >= melhorDepth)
            {
                melhorDepth = c.depth;
                melhor = c;
            }
        }
        if (intro != null) return intro;
        if (melhor != null) return melhor;
        return Camera.main;
    }

    static List<CanvasBackup> PrepararUI(Camera cam)
    {
        var lista = new List<CanvasBackup>();
        var todos = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        for (int i = 0; i < todos.Length; i++)
        {
            var cv = todos[i];
            if (cv == null || cv.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            lista.Add(new CanvasBackup
            {
                canvas = cv,
                modo = cv.renderMode,
                worldCamera = cv.worldCamera,
                planeDistance = cv.planeDistance
            });
            cv.renderMode = RenderMode.ScreenSpaceCamera;
            cv.worldCamera = cam;
            if (cv.planeDistance < 0.1f) cv.planeDistance = 1f;
        }
        return lista;
    }

    static void RestaurarUI(List<CanvasBackup> lista)
    {
        for (int i = 0; i < lista.Count; i++)
        {
            var b = lista[i];
            if (b.canvas == null) continue;
            b.canvas.renderMode = b.modo;
            b.canvas.worldCamera = b.worldCamera;
            b.canvas.planeDistance = b.planeDistance;
        }
    }

    static string Sanitizar(string nome)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            nome = nome.Replace(c, '_');
        return nome.Replace(' ', '_');
    }

    // ---------- qualidade (sobe ao ligar, restaura ao desligar / sair do Play) ----------

    public static void AplicarQualidade()
    {
        if (!Application.isPlaying) return;
        if (!qualidadeAplicada) GuardarOriginais();

        bool desligarMobile = EditorPrefs.GetBool(PrefDesligarMobile, true);
        bool qualidadePC = EditorPrefs.GetBool(PrefQualidadePC, true);
        bool sombras = EditorPrefs.GetBool(PrefSombrasURP, true);
        bool lod = EditorPrefs.GetBool(PrefLodBias, true);
        bool aa = EditorPrefs.GetBool(PrefAntiAliasing, true);

        if (qualidadePC || desligarMobile)
        {
            int pc = IndiceQualidadePC();
            if (pc >= 0 && QualitySettings.GetQualityLevel() != pc)
                QualitySettings.SetQualityLevel(pc, true);
        }

        if (desligarMobile)
        {
            Application.targetFrameRate = -1;
            if (QualitySettings.vSyncCount == 0 && targetFpsOriginal == 0)
                Application.targetFrameRate = -1;
        }

        if (lod)
        {
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, LodBiasAlvo);
            QualitySettings.maximumLODLevel = 0;
        }

        AplicarCloneUrp(sombras, desligarMobile);
        if (aa) AplicarAaCameras();
        else RestaurarAaCameras();

        qualidadeAplicada = true;
        EditorPrefs.SetBool(PrefPrecisaRestaurar, true);
        Debug.Log(TagLog + "qualidade de captura aplicada. " + ResumoQualidade());
    }

    static void GuardarOriginais()
    {
        nivelOriginal = QualitySettings.GetQualityLevel();
        lodBiasOriginal = QualitySettings.lodBias;
        maxLodOriginal = QualitySettings.maximumLODLevel;
        targetFpsOriginal = Application.targetFrameRate;
        vSyncOriginal = QualitySettings.vSyncCount;
        pipelineOriginal = QualitySettings.renderPipeline;
        if (pipelineOriginal == null)
            pipelineOriginal = GraphicsSettings.currentRenderPipeline;

        EditorPrefs.SetInt(PrefNivelOrig, nivelOriginal);
        EditorPrefs.SetFloat(PrefLodOrig, lodBiasOriginal);
        EditorPrefs.SetInt(PrefMaxLodOrig, maxLodOriginal);
        EditorPrefs.SetInt(PrefFpsOrig, targetFpsOriginal);
        EditorPrefs.SetInt(PrefVSyncOrig, vSyncOriginal);
        string caminhoPipe = "";
        if (pipelineOriginal != null)
            caminhoPipe = AssetDatabase.GetAssetPath(pipelineOriginal);
        EditorPrefs.SetString(PrefPipelineOrig, caminhoPipe ?? "");
    }

    static void AplicarCloneUrp(bool sombras, bool desligarMobile)
    {
        var fonte = pipelineOriginal as UniversalRenderPipelineAsset;
        if (fonte == null) fonte = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (fonte == null) fonte = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (fonte == null) return;

        // Nunca altera o asset do disco: trabalha numa copia em memoria.
        if (pipelineClone == null)
        {
            pipelineClone = Instantiate(fonte);
            pipelineClone.name = fonte.name + " (CapturaFrame temp)";
            pipelineClone.hideFlags = HideFlags.HideAndDontSave;
        }

        if (desligarMobile)
            pipelineClone.renderScale = 1f;

        if (sombras)
        {
            pipelineClone.shadowDistance = Mathf.Max(fonte.shadowDistance, DistanciaSombraAlvo);
            try { pipelineClone.shadowCascadeCount = 4; }
            catch (Exception e) { Debug.LogWarning(TagLog + "nao foi possivel ajustar cascatas de sombra: " + e.Message); }
            try { pipelineClone.mainLightShadowmapResolution = Math.Max(fonte.mainLightShadowmapResolution, ResolucaoSombraAlvo); }
            catch (Exception e) { Debug.LogWarning(TagLog + "nao foi possivel subir a sombra principal para 4096: " + e.Message); }
            try { pipelineClone.additionalLightsShadowmapResolution = Math.Max(fonte.additionalLightsShadowmapResolution, ResolucaoSombraAlvo); }
            catch (Exception e) { Debug.LogWarning(TagLog + "nao foi possivel subir a sombra adicional para 4096: " + e.Message); }
        }

        if (desligarMobile || sombras)
        {
            try
            {
                int msaaAlvo = Math.Max(fonte.msaaSampleCount, 4);
                if (msaaAlvo < 8 && Largura < Largura8K) msaaAlvo = 8;
                pipelineClone.msaaSampleCount = msaaAlvo;
            }
            catch (Exception e) { Debug.LogWarning(TagLog + "nao foi possivel ajustar MSAA do URP: " + e.Message); }
        }

        if (QualitySettings.renderPipeline != pipelineClone)
            QualitySettings.renderPipeline = pipelineClone;
    }

    static void AplicarAaCameras()
    {
        bool usarTaa = EditorPrefs.GetBool(PrefUsarTaa, false);
        var modo = usarTaa ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var dados = FindObjectsByType<UniversalAdditionalCameraData>(FindObjectsInactive.Include);
        for (int i = 0; i < dados.Length; i++)
        {
            var d = dados[i];
            if (d == null) continue;
            bool ja = false;
            for (int k = 0; k < camerasAa.Count; k++)
            {
                if (camerasAa[k].dados == d) { ja = true; break; }
            }
            if (!ja)
            {
                camerasAa.Add(new CameraAaBackup
                {
                    dados = d,
                    modo = d.antialiasing,
                    qualidade = d.antialiasingQuality
                });
            }
            d.antialiasing = modo;
            d.antialiasingQuality = AntialiasingQuality.High;
        }
    }

    static void RestaurarAaCameras()
    {
        for (int i = 0; i < camerasAa.Count; i++)
        {
            var b = camerasAa[i];
            if (b.dados == null) continue;
            b.dados.antialiasing = b.modo;
            b.dados.antialiasingQuality = b.qualidade;
        }
        camerasAa.Clear();
    }

    public static void RestaurarQualidadeSePreciso()
    {
        bool marcada = qualidadeAplicada || EditorPrefs.GetBool(PrefPrecisaRestaurar, false);
        if (!marcada) return;

        try
        {
            if (EditorPrefs.HasKey(PrefNivelOrig))
                nivelOriginal = EditorPrefs.GetInt(PrefNivelOrig, nivelOriginal);
            if (EditorPrefs.HasKey(PrefLodOrig))
                lodBiasOriginal = EditorPrefs.GetFloat(PrefLodOrig, lodBiasOriginal);
            if (EditorPrefs.HasKey(PrefMaxLodOrig))
                maxLodOriginal = EditorPrefs.GetInt(PrefMaxLodOrig, maxLodOriginal);
            if (EditorPrefs.HasKey(PrefFpsOrig))
                targetFpsOriginal = EditorPrefs.GetInt(PrefFpsOrig, targetFpsOriginal);
            if (EditorPrefs.HasKey(PrefVSyncOrig))
                vSyncOriginal = EditorPrefs.GetInt(PrefVSyncOrig, vSyncOriginal);

            string caminhoPipe = EditorPrefs.GetString(PrefPipelineOrig, "");
            if (pipelineOriginal == null && !string.IsNullOrEmpty(caminhoPipe))
                pipelineOriginal = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(caminhoPipe);

            if (QualitySettings.GetQualityLevel() != nivelOriginal)
                QualitySettings.SetQualityLevel(nivelOriginal, true);

            QualitySettings.lodBias = lodBiasOriginal;
            QualitySettings.maximumLODLevel = maxLodOriginal;
            Application.targetFrameRate = targetFpsOriginal;
            QualitySettings.vSyncCount = vSyncOriginal;

            if (pipelineOriginal != null)
                QualitySettings.renderPipeline = pipelineOriginal;

            if (pipelineClone != null)
            {
                if (QualitySettings.renderPipeline == pipelineClone)
                    QualitySettings.renderPipeline = pipelineOriginal;
                DestroyImmediate(pipelineClone);
                pipelineClone = null;
            }

            RestaurarAaCameras();
            Debug.Log(TagLog + "qualidade original restaurada.");
        }
        catch (Exception e)
        {
            Debug.LogWarning(TagLog + "falha ao restaurar qualidade: " + e.Message);
        }
        finally
        {
            qualidadeAplicada = false;
            EditorPrefs.SetBool(PrefPrecisaRestaurar, false);
        }
    }

    public static void ReaplicarSeEstiverLigado()
    {
        if (!EditorPrefs.GetBool(PrefAtivo, false) || !Application.isPlaying) return;
        RestaurarQualidadeSePreciso();
        AplicarQualidade();
    }

    static int IndiceQualidadePC()
    {
        var nomes = QualitySettings.names;
        for (int i = 0; i < nomes.Length; i++)
        {
            if (string.Equals(nomes[i], "PC", StringComparison.OrdinalIgnoreCase)) return i;
        }
        // Evita o nivel "Mobile" / "MobileWeb": pega o ultimo que nao pareca mobile.
        for (int i = nomes.Length - 1; i >= 0; i--)
        {
            string n = nomes[i] ?? "";
            if (n.IndexOf("Mobile", StringComparison.OrdinalIgnoreCase) < 0)
                return i;
        }
        return nomes.Length > 0 ? nomes.Length - 1 : -1;
    }

    static string ResumoQualidade()
    {
        var nomes = QualitySettings.names;
        int q = QualitySettings.GetQualityLevel();
        string nivel = (q >= 0 && q < nomes.Length) ? nomes[q] : q.ToString();
        string urp = pipelineClone != null
            ? "clone " + pipelineClone.name + " shadows=" + pipelineClone.shadowDistance + " map=" + pipelineClone.mainLightShadowmapResolution
              + " cascades=" + pipelineClone.shadowCascadeCount + " scale=" + pipelineClone.renderScale + " msaa=" + pipelineClone.msaaSampleCount
            : "sem clone URP";
        string aa = EditorPrefs.GetBool(PrefAntiAliasing, true)
            ? (EditorPrefs.GetBool(PrefUsarTaa, false) ? "TAA" : "SMAA High")
            : "off";
        return "Quality=" + nivel + " lodBias=" + QualitySettings.lodBias + " AA=" + aa + " " + urp
            + " fps=" + Application.targetFrameRate + ".";
    }
}
#endif
