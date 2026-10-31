using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Overlay de FPS / diagnostico para otimizar as cenas. Cria-se sozinho (DontDestroyOnLoad), nao precisa de Inspector.
//
//  - F3 liga/desliga o painel (Input System novo e legado).
//  - Ligado por padrao: Editor, Development Build, ou URL com ?fps=1 (WebGL, Application.absoluteURL).
//    ?fps=log  = sem painel, so as linhas [FPS] no log.   ?fps=0 = desligado (F3 ainda liga).
//  - A cada 5 s escreve UMA linha "[FPS] ..." no Debug.Log (no teste local cai em Builds/log_teste.txt).
//  - A cada cena carregada escreve uma linha "[FPS] scene load ..." com o tempo de carga.
//  - Custo baixo: so um Update com contas simples; o texto do painel e remontado 2x por segundo.
//    Quando desligado, nao inicia ProfilerRecorders nem escreve logs.
public sealed class FpsDebugOverlay : MonoBehaviour
{
    const float JanelaSeg = 5f;         // janela para avg / 1% low / min / max
    const float IntervaloLog = 5f;      // linha [FPS] periodica
    const float IntervaloPainel = 0.5f; // remonta o texto do painel
    const int Capacidade = 2048;

    static FpsDebugOverlay instancia;

    // ---- estado ligado/desligado ----
    bool painel;
    bool logando;

    // ---- amostras de frame (ring buffer) ----
    readonly float[] dts = new float[Capacidade];
    readonly double[] tempos = new double[Capacidade];
    readonly float[] scratch = new float[Capacidade];
    int cabeca;          // proxima posicao a escrever
    int contagem;
    double ultimoT;      // realtime do frame anterior
    float proxPainel;
    float proxLog;

    // ---- estatisticas calculadas ----
    float fpsAtual, fpsMedio, fps1Low, msMin, msMax;
    int amostrasJanela;
    float acumPainelDt; int acumPainelN;

    // ---- carga de cena ----
    bool cargaPendente;
    string cargaCena;
    LoadSceneMode cargaModo;
    double cargaT;           // realtime no sceneLoaded
    double cargaGapMs;       // tempo desde o ultimo Update ate o sceneLoaded (inclui a carga sincrona)
    double ultimaCargaT;

    // ---- ProfilerRecorders ----
    ProfilerRecorder rDraw, rBatches, rSetPass, rTris, rVerts;
    ProfilerRecorder rMain, rTexMem, rMeshMem, rTotalUsed;
    bool recordersAtivos;

    // ---- painel ----
    string textoPainel = "";
    string linhaFps = "";
    GUIStyle estilo;
    Texture2D fundo;
    readonly StringBuilder sb = new StringBuilder(1024);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { instancia = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Criar()
    {
        if (instancia != null) return;
        var go = new GameObject("FpsDebugOverlay");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<FpsDebugOverlay>();
    }

    void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;

        string modo = LerParametroUrl("fps");
        bool padrao = Application.isEditor || Debug.isDebugBuild;
        if (modo == "1" || modo == "true" || modo == "on") { painel = true; logando = true; }
        else if (modo == "log") { painel = false; logando = true; }
        else if (modo == "0" || modo == "false" || modo == "off") { painel = false; logando = false; }
        else { painel = padrao; logando = padrao; }

        ultimoT = Time.realtimeSinceStartupAsDouble;
        ultimaCargaT = 0;
        SceneManager.sceneLoaded += AoCarregarCena;
        AtualizarRecorders();

        Debug.Log("[FPS] overlay pronto: painel=" + painel + " log=" + logando + " (F3 alterna; URL ?fps=1 / ?fps=log / ?fps=0) url='" + Application.absoluteURL + "'");
        if (logando) LogSistema();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        PararRecorders();
        if (instancia == this) instancia = null;
        if (fundo != null) Destroy(fundo);
    }

    // ------------------------------------------------------------------ URL
    static string LerParametroUrl(string chave)
    {
        string url = Application.absoluteURL;
        if (string.IsNullOrEmpty(url)) return null;
        int q = url.IndexOf('?');
        if (q < 0) return null;
        string qs = url.Substring(q + 1);
        int h = qs.IndexOf('#');
        if (h >= 0) qs = qs.Substring(0, h);
        foreach (string par in qs.Split('&'))
        {
            int eq = par.IndexOf('=');
            string k = eq < 0 ? par : par.Substring(0, eq);
            if (!string.Equals(k, chave, StringComparison.OrdinalIgnoreCase)) continue;
            return eq < 0 ? "1" : par.Substring(eq + 1).Trim().ToLowerInvariant();
        }
        return null;
    }

    // ------------------------------------------------------------------ recorders
    static ProfilerRecorder Iniciar(ProfilerCategory cat, string nome)
    {
        try { return ProfilerRecorder.StartNew(cat, nome); }
        catch (Exception) { return default(ProfilerRecorder); }
    }

    void AtualizarRecorders()
    {
        bool precisa = painel || logando;
        if (precisa && !recordersAtivos)
        {
            rDraw = Iniciar(ProfilerCategory.Render, "Draw Calls Count");
            rBatches = Iniciar(ProfilerCategory.Render, "Batches Count");
            rSetPass = Iniciar(ProfilerCategory.Render, "SetPass Calls Count");
            rTris = Iniciar(ProfilerCategory.Render, "Triangles Count");
            rVerts = Iniciar(ProfilerCategory.Render, "Vertices Count");
            rMain = Iniciar(ProfilerCategory.Internal, "Main Thread");
            rTotalUsed = Iniciar(ProfilerCategory.Memory, "Total Used Memory");
            rTexMem = Iniciar(ProfilerCategory.Memory, "Texture Memory");
            rMeshMem = Iniciar(ProfilerCategory.Memory, "Mesh Memory");
            recordersAtivos = true;
        }
        else if (!precisa && recordersAtivos)
        {
            PararRecorders();
        }
    }

    void PararRecorders()
    {
        if (!recordersAtivos) return;
        Liberar(ref rDraw); Liberar(ref rBatches); Liberar(ref rSetPass); Liberar(ref rTris); Liberar(ref rVerts);
        Liberar(ref rMain); Liberar(ref rTotalUsed); Liberar(ref rTexMem); Liberar(ref rMeshMem);
        recordersAtivos = false;
    }

    static void Liberar(ref ProfilerRecorder r)
    {
        if (r.Valid) r.Dispose();
        r = default(ProfilerRecorder);
    }

    // valor do recorder ou "n/a" (invalido, ou 0 em build sem Development onde as estatisticas de render nao existem)
    static string Contagem(ProfilerRecorder r)
    {
        if (!r.Valid) return "n/a";
        long v = r.LastValue;
        if (v == 0 && !Application.isEditor && !Debug.isDebugBuild) return "n/a";
        return v.ToString();
    }

    static string Megas(ProfilerRecorder r)
    {
        if (!r.Valid) return "n/a";
        long v = r.LastValue;
        if (v == 0 && !Application.isEditor && !Debug.isDebugBuild) return "n/a";
        return (v / 1048576.0).ToString("0.0") + "MB";
    }

    static string MsMain(ProfilerRecorder r)
    {
        if (!r.Valid) return "n/a";
        long v = r.LastValue; // nanossegundos
        if (v <= 0) return "n/a";
        return (v / 1000000.0).ToString("0.0") + "ms";
    }

    // ------------------------------------------------------------------ cena
    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        double agora = Time.realtimeSinceStartupAsDouble;
        cargaPendente = true;
        cargaCena = cena.name;
        cargaModo = modo;
        cargaT = agora;
        cargaGapMs = (agora - ultimoT) * 1000.0;
    }

    // ------------------------------------------------------------------ loop
    void Update()
    {
        double agora = Time.realtimeSinceStartupAsDouble;
        float dt = (float)(agora - ultimoT);
        ultimoT = agora;

        if (TeclaF3()) Alternar();

        if (dt > 0f)
        {
            dts[cabeca] = dt;
            tempos[cabeca] = agora;
            cabeca = (cabeca + 1) % Capacidade;
            if (contagem < Capacidade) contagem++;
            acumPainelDt += dt; acumPainelN++;
        }

        if (cargaPendente)
        {
            cargaPendente = false;
            if (logando)
            {
                double desdeUltima = ultimaCargaT > 0 ? cargaT - ultimaCargaT : -1;
                Debug.Log("[FPS] scene load: scene=" + cargaCena + " mode=" + cargaModo
                          + " startup=" + cargaT.ToString("0.00") + "s"
                          + " gapSinceLastFrame=" + cargaGapMs.ToString("0") + "ms"
                          + " firstFrameAfterLoad=" + (dt * 1000f).ToString("0") + "ms"
                          + " sinceLastSceneLoad=" + (desdeUltima >= 0 ? desdeUltima.ToString("0.00") + "s" : "n/a")
                          + " res=" + Screen.width + "x" + Screen.height);
            }
            ultimaCargaT = cargaT;
        }

        if (!painel && !logando) return;

        float t = Time.unscaledTime;
        if (painel && t >= proxPainel)
        {
            proxPainel = t + IntervaloPainel;
            fpsAtual = acumPainelDt > 0f ? acumPainelN / acumPainelDt : 0f;
            acumPainelDt = 0f; acumPainelN = 0;
            CalcularJanela();
            MontarPainel();
        }
        if (logando && t >= proxLog)
        {
            proxLog = t + IntervaloLog;
            CalcularJanela();
            LogPeriodico();
        }
    }

    void Alternar()
    {
        if (painel) { painel = false; logando = false; }
        else { painel = true; logando = true; }
        AtualizarRecorders();
        proxPainel = 0f;
        proxLog = 0f;
        Debug.Log("[FPS] F3: painel=" + painel + " log=" + logando);
        if (logando) LogSistema();
    }

    static bool TeclaF3()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.f3Key.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F3)) return true;
#endif
        return false;
    }

    // avg / 1% low / min / max dos ultimos JanelaSeg segundos
    void CalcularJanela()
    {
        int n = 0;
        double soma = 0;
        float mn = float.MaxValue, mx = 0f;
        double limite = ultimoT - JanelaSeg;
        for (int i = 1; i <= contagem; i++)
        {
            int idx = (cabeca - i + Capacidade) % Capacidade;
            if (tempos[idx] < limite && n > 0) break;
            float d = dts[idx];
            scratch[n++] = d;
            soma += d;
            if (d < mn) mn = d;
            if (d > mx) mx = d;
        }
        amostrasJanela = n;
        if (n == 0) { fpsMedio = fps1Low = msMin = msMax = 0f; return; }

        fpsMedio = (float)(n / soma);
        msMin = mn * 1000f;
        msMax = mx * 1000f;

        // 1% low = media dos piores 1% frames (minimo 1 frame), em FPS
        Array.Sort(scratch, 0, n);               // crescente
        int k = Mathf.Max(1, n / 100);
        double piores = 0;
        for (int i = 0; i < k; i++) piores += scratch[n - 1 - i];
        fps1Low = (float)(k / piores);
    }

    // ------------------------------------------------------------------ textos
    static string Pretty(string s) { return string.IsNullOrEmpty(s) ? "?" : s; }

    string Resolucao()
    {
        Resolution r = Screen.currentResolution;
        return Screen.width + "x" + Screen.height + " (display " + r.width + "x" + r.height + ")";
    }

    string Dpr()
    {
        float dpi = Screen.dpi;
        if (dpi <= 0f) return "n/a";
        // WebGL reporta dpi ~ devicePixelRatio * 96 (aproximacao)
        return "dpi " + dpi.ToString("0") + " (DPR~" + (dpi / 96f).ToString("0.00") + ")";
    }

    static string UrpInfo()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return GraphicsSettings.currentRenderPipeline == null ? "built-in" : GraphicsSettings.currentRenderPipeline.name;
        return urp.name + " renderScale=" + urp.renderScale.ToString("0.##") + " msaa=" + urp.msaaSampleCount
               + " shadowDist=" + urp.shadowDistance.ToString("0") + " hdr=" + urp.supportsHDR;
    }

    static string MB(long bytes) { return (bytes / 1048576.0).ToString("0.0"); }

    void MontarPainel()
    {
        string cor = fpsAtual >= 50f ? "#7CFC00" : (fpsAtual >= 30f ? "#FFD700" : "#FF5555");
        linhaFps = "<color=" + cor + "><b>FPS " + fpsAtual.ToString("0.0") + "</b></color>";

        sb.Length = 0;
        sb.Append(linhaFps).Append("  (").Append(fpsAtual > 0 ? (1000f / fpsAtual).ToString("0.0") : "0").Append(" ms)\n");
        sb.Append("avg ").Append(fpsMedio.ToString("0.0")).Append("   1% low ").Append(fps1Low.ToString("0.0")).Append("   (").Append(amostrasJanela).Append(" frames/").Append(JanelaSeg.ToString("0")).Append("s)\n");
        sb.Append("frame ms  min ").Append(msMin.ToString("0.0")).Append("  max ").Append(msMax.ToString("0.0"));
        sb.Append("   main thread ").Append(MsMain(rMain)).Append('\n');
        sb.Append("scene ").Append(SceneManager.GetActiveScene().name).Append("   t=").Append(Time.realtimeSinceStartup.ToString("0")).Append("s\n");
        sb.Append("res ").Append(Resolucao()).Append('\n');
        sb.Append(Dpr()).Append("   fs=").Append(Screen.fullScreen).Append("   vsync=").Append(QualitySettings.vSyncCount).Append(" target=").Append(Application.targetFrameRate).Append('\n');
        sb.Append("gfx ").Append(SystemInfo.graphicsDeviceType).Append(" | ").Append(Pretty(SystemInfo.graphicsDeviceVersion)).Append('\n');
        sb.Append("gpu ").Append(Pretty(SystemInfo.graphicsDeviceName)).Append(" (").Append(SystemInfo.graphicsMemorySize).Append("MB)\n");
        sb.Append("quality ").Append(QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "?").Append("   ").Append(UrpInfo()).Append('\n');
        sb.Append("mem alloc ").Append(MB(Profiler.GetTotalAllocatedMemoryLong())).Append("MB  reserved ").Append(MB(Profiler.GetTotalReservedMemoryLong()))
          .Append("MB  mono ").Append(MB(Profiler.GetMonoUsedSizeLong())).Append('/').Append(MB(Profiler.GetMonoHeapSizeLong())).Append("MB  gc ").Append(MB(GC.GetTotalMemory(false))).Append("MB\n");
        sb.Append("used ").Append(Megas(rTotalUsed)).Append("  tex ").Append(Megas(rTexMem)).Append("  mesh ").Append(Megas(rMeshMem)).Append('\n');
        sb.Append("draw ").Append(Contagem(rDraw)).Append("  batches ").Append(Contagem(rBatches)).Append("  setpass ").Append(Contagem(rSetPass)).Append('\n');
        sb.Append("tris ").Append(Contagem(rTris)).Append("  verts ").Append(Contagem(rVerts)).Append('\n');
        sb.Append("<color=#999999>F3 esconde</color>");
        textoPainel = sb.ToString();
    }

    void LogSistema()
    {
        Debug.Log("[FPS] sys: platform=" + Application.platform + " os=" + SystemInfo.operatingSystem
                  + " unity=" + Application.unityVersion + " dev=" + Debug.isDebugBuild
                  + " cpu='" + SystemInfo.processorType + "' cores=" + SystemInfo.processorCount
                  + " ram=" + SystemInfo.systemMemorySize + "MB"
                  + " gpu='" + SystemInfo.graphicsDeviceName + "' vendor='" + SystemInfo.graphicsDeviceVendor + "' api=" + SystemInfo.graphicsDeviceType
                  + " ver='" + SystemInfo.graphicsDeviceVersion + "' vram=" + SystemInfo.graphicsMemorySize + "MB"
                  + " maxTex=" + SystemInfo.maxTextureSize
                  + " quality=" + (QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "?")
                  + " urp=[" + UrpInfo() + "]"
                  + " res=" + Resolucao() + " " + Dpr()
                  + " vsync=" + QualitySettings.vSyncCount + " targetFps=" + Application.targetFrameRate);
    }

    void LogPeriodico()
    {
        sb.Length = 0;
        sb.Append("[FPS] t=").Append(Time.realtimeSinceStartup.ToString("0")).Append("s scene=").Append(SceneManager.GetActiveScene().name);
        sb.Append(" fps=").Append(fpsAtual > 0 ? fpsAtual.ToString("0.0") : (fpsMedio.ToString("0.0")));
        sb.Append(" avg=").Append(fpsMedio.ToString("0.0")).Append(" low1=").Append(fps1Low.ToString("0.0"));
        sb.Append(" msMin=").Append(msMin.ToString("0.0")).Append(" msMax=").Append(msMax.ToString("0.0")).Append(" n=").Append(amostrasJanela);
        sb.Append(" main=").Append(MsMain(rMain));
        sb.Append(" res=").Append(Screen.width).Append('x').Append(Screen.height).Append(" dpi=").Append(Screen.dpi.ToString("0"));
        sb.Append(" draw=").Append(Contagem(rDraw)).Append(" batches=").Append(Contagem(rBatches)).Append(" setpass=").Append(Contagem(rSetPass));
        sb.Append(" tris=").Append(Contagem(rTris)).Append(" verts=").Append(Contagem(rVerts));
        sb.Append(" memAlloc=").Append(MB(Profiler.GetTotalAllocatedMemoryLong())).Append("MB memRes=").Append(MB(Profiler.GetTotalReservedMemoryLong()))
          .Append("MB mono=").Append(MB(Profiler.GetMonoUsedSizeLong())).Append("MB");
        sb.Append(" tex=").Append(Megas(rTexMem)).Append(" mesh=").Append(Megas(rMeshMem));
        Debug.Log(sb.ToString());
    }

    // ------------------------------------------------------------------ GUI
    void OnGUI()
    {
        if (!painel || string.IsNullOrEmpty(textoPainel)) return;

        if (estilo == null)
        {
            fundo = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            fundo.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            fundo.Apply();
            estilo = new GUIStyle(GUI.skin.label);
            estilo.richText = true;
            estilo.fontSize = 12;
            estilo.wordWrap = false;
            estilo.alignment = TextAnchor.UpperLeft;
            estilo.normal.textColor = Color.white;
            estilo.normal.background = fundo;
            estilo.padding = new RectOffset(6, 6, 4, 4);
        }

        float escala = Mathf.Clamp(Screen.height / 720f, 1f, 2.5f);
        Matrix4x4 antiga = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(escala, escala, 1f));
        GUI.Label(new Rect(6f, 6f, 520f, 235f), textoPainel, estilo);
        GUI.matrix = antiga;
    }
}