// CapturaFrameMenu.cs (Editor)
// Menu: Tools > Captura
// Liga/desliga a captura de frame em alta resolucao no Play do Editor.
// Fica salvo so nesta maquina (EditorPrefs); nao entra na build.
// Ao desligar, ou ao sair do Play, restaura a qualidade original.
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CapturaFrameMenu
{
    const string CaminhoAtivar = "Tools/Captura/Ativar captura de frame";
    const string CaminhoCapturar = "Tools/Captura/Capturar frame agora (F12)";
    const string CaminhoPausar = "Tools/Captura/Pausar ou continuar (P)";
    const string CaminhoAvancar = "Tools/Captura/Avancar um frame (.)";
    const string CaminhoVoltar = "Tools/Captura/Voltar um frame (,)";
    const string CaminhoPasta = "Tools/Captura/Abrir pasta das capturas";

    const string Caminho4K = "Tools/Captura/Resolucao/4K (3840 x 2160)";
    const string Caminho8K = "Tools/Captura/Resolucao/8K (7680 x 4320)";
    const string CaminhoPersonalizada = "Tools/Captura/Resolucao/Personalizada...";

    const string CaminhoIncluirUI = "Tools/Captura/Incluir UI da tela";
    const string CaminhoTransparente = "Tools/Captura/Fundo transparente";

    const string CaminhoQualidadePC = "Tools/Captura/Qualidade/Subir Quality Level para PC";
    const string CaminhoAA = "Tools/Captura/Qualidade/Anti-aliasing da camera";
    const string CaminhoTAA = "Tools/Captura/Qualidade/Usar TAA em vez de SMAA Alto";
    const string CaminhoSombras = "Tools/Captura/Qualidade/Melhorar sombras do URP";
    const string CaminhoMobile = "Tools/Captura/Qualidade/Desligar ajustes mobile e escala";
    const string CaminhoLod = "Tools/Captura/Qualidade/Aumentar lodBias";

    static CapturaFrameMenu()
    {
        EditorApplication.playModeStateChanged += AoMudarPlay;
        if (EditorPrefs.GetBool(CapturaFrame.PrefPrecisaRestaurar, false)
            && !EditorApplication.isPlaying
            && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            CapturaFrame.RestaurarQualidadeSePreciso();
        }
    }

    static void AoMudarPlay(PlayModeStateChange estado)
    {
        if (estado == PlayModeStateChange.ExitingPlayMode)
            CapturaFrame.RestaurarQualidadeSePreciso();
    }

    [MenuItem(CaminhoAtivar, false, 0)]
    static void AlternarAtivo()
    {
        bool novo = !EditorPrefs.GetBool(CapturaFrame.PrefAtivo, false);
        EditorPrefs.SetBool(CapturaFrame.PrefAtivo, novo);
        CapturaFrame.AoMudarAtivo(novo);
    }

    [MenuItem(CaminhoAtivar, true)]
    static bool ValidarAtivo()
    {
        Menu.SetChecked(CaminhoAtivar, EditorPrefs.GetBool(CapturaFrame.PrefAtivo, false));
        return true;
    }

    [MenuItem(CaminhoCapturar, false, 20)]
    static void CapturarAgora() => CapturaFrame.PedirCaptura();

    [MenuItem(CaminhoPausar, false, 21)]
    static void Pausar() => CapturaFrame.PedirPausa();

    [MenuItem(CaminhoAvancar, false, 22)]
    static void Avancar() => CapturaFrame.PedirAvanco(1);

    [MenuItem(CaminhoVoltar, false, 23)]
    static void Voltar() => CapturaFrame.PedirAvanco(-1);

    [MenuItem(CaminhoPasta, false, 24)]
    static void AbrirPasta()
    {
        string pasta = CapturaFrame.PastaCapturas;
        EditorUtility.RevealInFinder(pasta);
        Debug.Log(CapturaFrame.TagLog + "pasta: " + pasta);
    }

    [MenuItem(Caminho4K, false, 40)]
    static void Usar4K()
    {
        CapturaFrame.DefinirResolucao(CapturaFrame.Largura4K, CapturaFrame.Altura4K);
    }

    [MenuItem(Caminho4K, true)]
    static bool Validar4K()
    {
        Menu.SetChecked(Caminho4K,
            CapturaFrame.Largura == CapturaFrame.Largura4K && CapturaFrame.Altura == CapturaFrame.Altura4K);
        return true;
    }

    [MenuItem(Caminho8K, false, 41)]
    static void Usar8K()
    {
        CapturaFrame.DefinirResolucao(CapturaFrame.Largura8K, CapturaFrame.Altura8K);
        Debug.Log(CapturaFrame.TagLog + "8K usa bastante memoria (MSAA 4). Se falhar, volte para 4K.");
    }

    [MenuItem(Caminho8K, true)]
    static bool Validar8K()
    {
        Menu.SetChecked(Caminho8K,
            CapturaFrame.Largura == CapturaFrame.Largura8K && CapturaFrame.Altura == CapturaFrame.Altura8K);
        return true;
    }

    [MenuItem(CaminhoPersonalizada, false, 42)]
    static void UsarPersonalizada()
    {
        CapturaResWindow.Abrir();
    }

    [MenuItem(CaminhoIncluirUI, false, 60)]
    static void AlternarUI()
    {
        bool novo = !EditorPrefs.GetBool(CapturaFrame.PrefIncluirUI, false);
        EditorPrefs.SetBool(CapturaFrame.PrefIncluirUI, novo);
        Debug.Log(CapturaFrame.TagLog + "incluir UI da tela: " + (novo ? "LIGADO" : "DESLIGADO (padrao)"));
    }

    [MenuItem(CaminhoIncluirUI, true)]
    static bool ValidarUI()
    {
        Menu.SetChecked(CaminhoIncluirUI, EditorPrefs.GetBool(CapturaFrame.PrefIncluirUI, false));
        return true;
    }

    [MenuItem(CaminhoTransparente, false, 61)]
    static void AlternarTransparente()
    {
        bool novo = !EditorPrefs.GetBool(CapturaFrame.PrefTransparente, false);
        EditorPrefs.SetBool(CapturaFrame.PrefTransparente, novo);
        Debug.Log(CapturaFrame.TagLog + "fundo transparente: " + (novo ? "LIGADO" : "DESLIGADO (padrao)"));
    }

    [MenuItem(CaminhoTransparente, true)]
    static bool ValidarTransparente()
    {
        Menu.SetChecked(CaminhoTransparente, EditorPrefs.GetBool(CapturaFrame.PrefTransparente, false));
        return true;
    }

    [MenuItem(CaminhoQualidadePC, false, 80)]
    static void AlternarQualidadePC() => AlternarPref(CapturaFrame.PrefQualidadePC, true, "Quality Level PC");

    [MenuItem(CaminhoQualidadePC, true)]
    static bool ValidarQualidadePC() => ValidarPref(CaminhoQualidadePC, CapturaFrame.PrefQualidadePC, true);

    [MenuItem(CaminhoAA, false, 81)]
    static void AlternarAA() => AlternarPref(CapturaFrame.PrefAntiAliasing, true, "anti-aliasing da camera");

    [MenuItem(CaminhoAA, true)]
    static bool ValidarAA() => ValidarPref(CaminhoAA, CapturaFrame.PrefAntiAliasing, true);

    [MenuItem(CaminhoTAA, false, 82)]
    static void AlternarTAA() => AlternarPref(CapturaFrame.PrefUsarTaa, false, "usar TAA em vez de SMAA Alto");

    [MenuItem(CaminhoTAA, true)]
    static bool ValidarTAA() => ValidarPref(CaminhoTAA, CapturaFrame.PrefUsarTaa, false);

    [MenuItem(CaminhoSombras, false, 83)]
    static void AlternarSombras() => AlternarPref(CapturaFrame.PrefSombrasURP, true, "melhorar sombras do URP");

    [MenuItem(CaminhoSombras, true)]
    static bool ValidarSombras() => ValidarPref(CaminhoSombras, CapturaFrame.PrefSombrasURP, true);

    [MenuItem(CaminhoMobile, false, 84)]
    static void AlternarMobile() => AlternarPref(CapturaFrame.PrefDesligarMobile, true, "desligar ajustes mobile e escala");

    [MenuItem(CaminhoMobile, true)]
    static bool ValidarMobile() => ValidarPref(CaminhoMobile, CapturaFrame.PrefDesligarMobile, true);

    [MenuItem(CaminhoLod, false, 85)]
    static void AlternarLod() => AlternarPref(CapturaFrame.PrefLodBias, true, "aumentar lodBias");

    [MenuItem(CaminhoLod, true)]
    static bool ValidarLod() => ValidarPref(CaminhoLod, CapturaFrame.PrefLodBias, true);

    static void AlternarPref(string chave, bool padrao, string rotulo)
    {
        bool novo = !EditorPrefs.GetBool(chave, padrao);
        EditorPrefs.SetBool(chave, novo);
        Debug.Log(CapturaFrame.TagLog + rotulo + ": " + (novo ? "LIGADO" : "DESLIGADO"));
        CapturaFrame.ReaplicarSeEstiverLigado();
    }

    static bool ValidarPref(string caminho, string chave, bool padrao)
    {
        Menu.SetChecked(caminho, EditorPrefs.GetBool(chave, padrao));
        return true;
    }
}

class CapturaResWindow : EditorWindow
{
    int largura;
    int altura;

    public static void Abrir()
    {
        var w = GetWindow<CapturaResWindow>(true, "Resolucao da captura", true);
        w.minSize = new Vector2(320, 120);
        w.largura = CapturaFrame.Largura;
        w.altura = CapturaFrame.Altura;
        w.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Resolucao personalizada (pixels)");
        largura = EditorGUILayout.IntField("Largura", largura);
        altura = EditorGUILayout.IntField("Altura", altura);
        EditorGUILayout.Space();
        if (GUILayout.Button("Aplicar"))
        {
            CapturaFrame.DefinirResolucao(largura, altura);
            Close();
        }
    }
}
