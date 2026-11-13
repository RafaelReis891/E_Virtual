// MobileRenderTool.cs
// Menu: Mobile > Tools > Iluminacao e Render mobile...
// Ferramenta de Editor para preparar iluminacao/render do E_Virtual para uma
// build WebGL unica que atende PC e celular.
//
// - Simular: auditoria completa (janela + CSV em <projeto>/MobileReports/). Nao muda nada.
// - Aplicar: pede confirmacao, faz backup em <projeto>/_Backup_Deletados/Render_<data>/ e aplica
//   so as secoes marcadas.
//     (a) Cria COPIAS do URP asset e do renderer do nivel Mobile (originais intactos) e ajusta as copias;
//         liga a copia no Quality Level "Mobile". Por padrao NAO troca o Quality padrao do WebGL
//         (hoje o projeto abre no nivel Mobile tambem no PC). O runtime no celular pode trocar
//         para o nivel Mobile; o PC permanece no nivel padrao se ele for o de PC.
//     (b) Desliga features pesadas (SSAO, Decal, ...) so no renderer copia.
//     (c) Pos-processamento: cria Resources/MobileRenderRuntimeConfig.asset, lido por
//         Assets/Scripts/Mobile/MobileRenderRuntime.cs em runtime (perfis de Volume nao mudam no disco).
//     (d) Luzes/probes das cenas do Build: so abre e salva cenas que realmente vao mudar
//         (backup dos .unity antes).
//     (e) Lightmapping e (f) Player/WebGL: so relatorio + recomendacoes.
// - Restaurar backup: volta Quality/Graphics, copia de volta cenas/assets, apaga o que foi criado.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace EVirtualMobile.EditorTools
{
    public class MobileRenderTool : EditorWindow
    {
        const string PrefsKey = "EVirtual.MobileRenderTool.Config.v2";
        const string PastaAssetsMobile = "Assets/Settings/MobileWeb";
        const string NomeAssetCopia = "MobileWeb_RPAsset";
        const string NomeRendererCopia = "MobileWeb_Renderer";
        const string CaminhoConfigRuntime = "Assets/Resources/MobileRenderRuntimeConfig.asset";

        // ----------------------------------------------------------------- config
        [Serializable]
        class Config
        {
            // (a) URP asset + Quality
            public bool secA = true;
            public string nivelMobile = "Mobile";
            public float renderScale = 0.8f;
            public bool hdr = false;
            public int msaa = 2;                 // 1 = off, 2, 4
            public float shadowDistance = 35f;
            public int cascades = 1;             // 1..2
            public int shadowRes = 1024;         // 512, 1024, 2048
            public bool softShadows = false;
            public int softShadowQuality = 1;    // 1 low, 2 medium, 3 high
            public int addLightsMode = 1;        // 0 desligado, 1 por pixel, 2 por vertice
            public int addLightsLimit = 2;       // 0..8
            public bool addLightShadows = false;
            public bool depthTexture = false;
            public bool opaqueTexture = false;
            public bool srpBatcher = true;
            public bool desligarLightLayers = true;
            public bool desligarLensFlare = true;
            public bool desligarLodCrossFade = true;
            public bool desligarProbeBlending = true;
            public bool atribuirNoNivel = true;
            public bool webglPadraoMobile = false;
            public bool mudarLodBias = false;
            public float lodBias = 0.8f;
            public bool excluirOutrosNiveisDoWebGL = false;
            public bool graphicsPadraoMobile = false;

            // (b) renderer
            public bool secB = true;
            public bool forwardSimples = true;
            public bool desligarFeaturesPesadas = true;
            public string featuresPesadas = "ScreenSpaceAmbientOcclusion\nDecalRendererFeature\nScreenSpaceShadows\nFullScreenPassRendererFeature";
            public bool desligarSombraTransparente = true;

            // (c) pos runtime
            public bool secC = true;
            public bool somenteEmCelular = true;
            public bool trocarNivelNoCelular = true;
            public bool desligarDOF = true, desligarMotionBlur = true, desligarChromatic = true,
                        desligarFilmGrain = true, desligarLensDistortion = true, desligarPanini = true, desligarVignette = false;
            public int bloom = 2;                 // 0 manter, 1 leve, 2 desligar
            public int aaCamera = 0;              // -1 manter, 0 nenhum, 1 FXAA
            public bool desligarPosNaCamera = false;

            // (d) cenas
            public bool secD = true;
            public bool sombraSoNaLuzPrincipal = true;
            public bool sombraResolucaoBaixa = true;
            public bool limitarLuzes = false;
            public int maxLuzesExtras = 4;
            public bool probesParaBaked = true;
            public int probeResMax = 128;
            public bool realtimeGIOff = true;

            public bool exportarCsv = true;
        }

        class Linha
        {
            public string secao, item, atual, novo, acao, obs;
        }

        // Plano por cena (vindo do YAML na simulacao)
        class LuzYaml { public string nome; public int tipo; public int modo; public int sombra; public int res; public bool ligada; public float intensidade; public float alcance; }
        class ProbeYaml { public string nome; public int modo; public int res; }
        class CenaInfo
        {
            public string path;
            public List<LuzYaml> luzes = new List<LuzYaml>();
            public List<ProbeYaml> probes = new List<ProbeYaml>();
            public int lightProbeGroups, prefabInstances;
            public bool realtimeGI, bakedGI, fog;
            public string lightingDataPath = "", lightingSettingsPath = "", skybox = "";
            public int mudancasPrevistas;
        }

        [Serializable]
        class Manifesto
        {
            public string data;
            public string nivelMobile;
            public int indiceNivelMobile = -1;
            public bool mudouQuality;
            public string rpNivelAntesGuid = "";
            public float lodBiasAntes;
            public bool mudouWebglPadrao;
            public int webglPadraoAntes = -1;
            public bool mudouExclusoes;
            public List<string> exclusoesAntes = new List<string>(); // "indice|plataformas,separadas"
            public bool mudouGraphics;
            public string graphicsRpAntesGuid = "";
            public List<string> criados = new List<string>();
            public List<string> copiados = new List<string>(); // caminhos relativos restauraveis por copia
        }

        // ----------------------------------------------------------------- estado
        [SerializeField] Config cfg;
        List<Linha> linhas = new List<Linha>();
        List<CenaInfo> cenas = new List<CenaInfo>();
        Vector2 scroll, scrollLista;
        bool foldA = true, foldB = true, foldC = true, foldD = true, foldEF = false;
        bool soMudancas = false;
        bool simulou;
        string ultimoCsv = "", ultimoBackup = "";

        static string RaizProjeto { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }
        static string PastaBackups { get { return Path.Combine(RaizProjeto, "_Backup_Deletados"); } }
        static string PastaRelatorios { get { return Path.Combine(RaizProjeto, "MobileReports"); } }
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [MenuItem("Mobile/Tools/Iluminacao e Render mobile...")]
        static void Abrir()
        {
            var w = GetWindow<MobileRenderTool>("Render mobile");
            w.minSize = new Vector2(640, 560);
            w.Show();
        }

        void OnEnable() { if (cfg == null) CarregarConfig(); }

        void CarregarConfig()
        {
            cfg = null;
            var json = EditorPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json)) { try { cfg = JsonUtility.FromJson<Config>(json); } catch { cfg = null; } }
            if (cfg == null) cfg = new Config();
        }

        void SalvarConfig() { EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(cfg)); }

        // ================================================================= GUI
        void OnGUI()
        {
            if (cfg == null) CarregarConfig();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.HelpBox(
                "Prepara iluminacao e render para WebGL (PC + celular na mesma build). Simular so le e gera relatorio. " +
                "Aplicar faz backup em _Backup_Deletados/Render_<data>/ e muda so as secoes marcadas. " +
                "Os URP assets/renderer originais e os perfis de Volume NAO sao editados (usa copias / ajuste em runtime). " +
                "Padrao: o corte de pos-processamento so roda no celular (Application.isMobilePlatform) " +
                "e o celular troca para o Quality 'Mobile' ao iniciar, para o PC nao ficar com o visual reduzido. " +
                "Restaurar backup desfaz tudo.", MessageType.Info);

            DesenharEstadoAtual();
            EditorGUI.BeginChangeCheck();

            foldA = EditorGUILayout.Foldout(foldA, "(a) URP asset mobile (copia) + Quality Level", true);
            if (foldA)
            {
                EditorGUI.indentLevel++;
                cfg.secA = EditorGUILayout.ToggleLeft("Aplicar esta secao", cfg.secA, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!cfg.secA))
                {
                    cfg.nivelMobile = EditorGUILayout.TextField("Quality Level mobile", cfg.nivelMobile);
                    cfg.renderScale = EditorGUILayout.Slider("Render Scale", cfg.renderScale, 0.5f, 1f);
                    cfg.hdr = EditorGUILayout.Toggle("HDR", cfg.hdr);
                    cfg.msaa = EditorGUILayout.IntPopup("MSAA", cfg.msaa, new[] { "Desligado", "2x", "4x" }, new[] { 1, 2, 4 });
                    cfg.shadowDistance = EditorGUILayout.Slider("Distancia de sombra (m)", cfg.shadowDistance, 10f, 60f);
                    cfg.cascades = EditorGUILayout.IntPopup("Cascatas", cfg.cascades, new[] { "1", "2" }, new[] { 1, 2 });
                    cfg.shadowRes = EditorGUILayout.IntPopup("Resolucao sombra (luz principal)", cfg.shadowRes, new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
                    cfg.softShadows = EditorGUILayout.Toggle("Sombras suaves", cfg.softShadows);
                    if (cfg.softShadows) cfg.softShadowQuality = EditorGUILayout.IntPopup("  Qualidade suave", cfg.softShadowQuality, new[] { "Baixa", "Media", "Alta" }, new[] { 1, 2, 3 });
                    cfg.addLightsMode = EditorGUILayout.IntPopup("Luzes adicionais", cfg.addLightsMode, new[] { "Desligadas", "Por pixel", "Por vertice" }, new[] { 0, 1, 2 });
                    cfg.addLightsLimit = EditorGUILayout.IntSlider("  Limite por objeto", cfg.addLightsLimit, 0, 8);
                    cfg.addLightShadows = EditorGUILayout.Toggle("  Sombras das luzes adicionais", cfg.addLightShadows);
                    cfg.depthTexture = EditorGUILayout.Toggle("Depth Texture", cfg.depthTexture);
                    cfg.opaqueTexture = EditorGUILayout.Toggle("Opaque Texture", cfg.opaqueTexture);
                    cfg.srpBatcher = EditorGUILayout.Toggle("SRP Batcher", cfg.srpBatcher);
                    cfg.desligarLightLayers = EditorGUILayout.ToggleLeft("Desligar Light Layers (menos variantes de shader)", cfg.desligarLightLayers);
                    cfg.desligarLensFlare = EditorGUILayout.ToggleLeft("Desligar Lens Flare (data-driven e screen space)", cfg.desligarLensFlare);
                    cfg.desligarLodCrossFade = EditorGUILayout.ToggleLeft("Desligar LOD Cross-Fade", cfg.desligarLodCrossFade);
                    cfg.desligarProbeBlending = EditorGUILayout.ToggleLeft("Desligar blend/box projection de reflection probes", cfg.desligarProbeBlending);
                    EditorGUILayout.Space(2);
                    cfg.atribuirNoNivel = EditorGUILayout.ToggleLeft("Ligar a copia no Quality Level '" + cfg.nivelMobile + "'", cfg.atribuirNoNivel);
                    cfg.webglPadraoMobile = EditorGUILayout.ToggleLeft("Quality padrao do WebGL = '" + cfg.nivelMobile + "' (desmarque para o PC nao abrir no visual mobile)", cfg.webglPadraoMobile);
                    if (cfg.webglPadraoMobile)
                        EditorGUILayout.HelpBox(
                            "Se o padrao do WebGL for Mobile, o PC tambem abre nesse nivel. Para uma build unica, deixe desmarcado e mantenha em (c) as opcoes de so aplicar / trocar nivel no celular.",
                            MessageType.Warning);
                    cfg.mudarLodBias = EditorGUILayout.ToggleLeft("Mudar LOD Bias do nivel mobile", cfg.mudarLodBias);
                    if (cfg.mudarLodBias) cfg.lodBias = EditorGUILayout.Slider("  LOD Bias", cfg.lodBias, 0.3f, 2f);
                    cfg.excluirOutrosNiveisDoWebGL = EditorGUILayout.ToggleLeft("Tirar os outros niveis (PC) do build WebGL (menos shaders; PC no navegador fica so no Mobile)", cfg.excluirOutrosNiveisDoWebGL);
                    cfg.graphicsPadraoMobile = EditorGUILayout.ToggleLeft("Graphics Settings > Default Render Pipeline = copia mobile", cfg.graphicsPadraoMobile);
                }
                EditorGUI.indentLevel--;
            }

            foldB = EditorGUILayout.Foldout(foldB, "(b) Renderer mobile (copia)", true);
            if (foldB)
            {
                EditorGUI.indentLevel++;
                cfg.secB = EditorGUILayout.ToggleLeft("Aplicar esta secao (precisa da secao a)", cfg.secB, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!cfg.secB))
                {
                    cfg.forwardSimples = EditorGUILayout.ToggleLeft("Rendering Path = Forward (nao Forward+/Deferred)", cfg.forwardSimples);
                    cfg.desligarSombraTransparente = EditorGUILayout.ToggleLeft("Transparentes nao recebem sombra", cfg.desligarSombraTransparente);
                    cfg.desligarFeaturesPesadas = EditorGUILayout.ToggleLeft("Desligar renderer features pesadas (lista abaixo, por nome do tipo)", cfg.desligarFeaturesPesadas);
                    if (cfg.desligarFeaturesPesadas) cfg.featuresPesadas = EditorGUILayout.TextArea(cfg.featuresPesadas, GUILayout.MinHeight(54));
                }
                EditorGUI.indentLevel--;
            }

            foldC = EditorGUILayout.Foldout(foldC, "(c) Pos-processamento no mobile (em runtime, perfis originais intactos)", true);
            if (foldC)
            {
                EditorGUI.indentLevel++;
                cfg.secC = EditorGUILayout.ToggleLeft("Aplicar esta secao (cria Resources/MobileRenderRuntimeConfig.asset)", cfg.secC, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!cfg.secC))
                {
                    cfg.somenteEmCelular = EditorGUILayout.ToggleLeft("So em celular (Application.isMobilePlatform no WebGL). Recomendado na build unica.", cfg.somenteEmCelular);
                    cfg.trocarNivelNoCelular = EditorGUILayout.ToggleLeft("No celular, trocar para o Quality mobile ao iniciar (PC nao e afetado)", cfg.trocarNivelNoCelular);
                    if (!cfg.somenteEmCelular)
                        EditorGUILayout.HelpBox(
                            "Desmarcado, o corte de pos-processamento tambem roda no PC se o Quality ativo for o nivel mobile.",
                            MessageType.Warning);
                    cfg.desligarDOF = EditorGUILayout.ToggleLeft("Desligar Depth of Field", cfg.desligarDOF);
                    cfg.desligarMotionBlur = EditorGUILayout.ToggleLeft("Desligar Motion Blur", cfg.desligarMotionBlur);
                    cfg.desligarChromatic = EditorGUILayout.ToggleLeft("Desligar Chromatic Aberration", cfg.desligarChromatic);
                    cfg.desligarFilmGrain = EditorGUILayout.ToggleLeft("Desligar Film Grain", cfg.desligarFilmGrain);
                    cfg.desligarLensDistortion = EditorGUILayout.ToggleLeft("Desligar Lens Distortion", cfg.desligarLensDistortion);
                    cfg.desligarPanini = EditorGUILayout.ToggleLeft("Desligar Panini Projection", cfg.desligarPanini);
                    cfg.desligarVignette = EditorGUILayout.ToggleLeft("Desligar Vignette", cfg.desligarVignette);
                    cfg.bloom = EditorGUILayout.IntPopup("Bloom", cfg.bloom, new[] { "Manter", "Leve", "Desligar" }, new[] { 0, 1, 2 });
                    if (!cfg.hdr && cfg.bloom != 2)
                        EditorGUILayout.HelpBox("Com HDR desligado, Bloom com threshold acima de 1 quase nao aparece, mas continua custando. Sugestao: Desligar.", MessageType.None);
                    cfg.aaCamera = EditorGUILayout.IntPopup("Anti-aliasing da camera", cfg.aaCamera, new[] { "Manter", "Nenhum (usa MSAA)", "FXAA" }, new[] { -1, 0, 1 });
                    cfg.desligarPosNaCamera = EditorGUILayout.ToggleLeft("Desligar pos-processamento inteiro na camera (tira tambem a correcao de cor)", cfg.desligarPosNaCamera);
                }
                EditorGUI.indentLevel--;
            }

            foldD = EditorGUILayout.Foldout(foldD, "(d) Luzes e probes nas cenas do Build", true);
            if (foldD)
            {
                EditorGUI.indentLevel++;
                cfg.secD = EditorGUILayout.ToggleLeft("Aplicar esta secao (so abre/salva cenas que vao mudar)", cfg.secD, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!cfg.secD))
                {
                    cfg.sombraSoNaLuzPrincipal = EditorGUILayout.ToggleLeft("Sombra so na luz principal (desliga sombra das outras luzes realtime/mixed)", cfg.sombraSoNaLuzPrincipal);
                    cfg.sombraResolucaoBaixa = EditorGUILayout.ToggleLeft("Luzes com resolucao de sombra propria -> Low", cfg.sombraResolucaoBaixa);
                    cfg.limitarLuzes = EditorGUILayout.ToggleLeft("Limitar luzes realtime extras (desliga as mais fracas)", cfg.limitarLuzes);
                    if (cfg.limitarLuzes) cfg.maxLuzesExtras = EditorGUILayout.IntSlider("  Maximo de luzes extras", cfg.maxLuzesExtras, 0, 16);
                    cfg.probesParaBaked = EditorGUILayout.ToggleLeft("Reflection probes realtime -> Baked (precisa bake depois)", cfg.probesParaBaked);
                    cfg.probeResMax = EditorGUILayout.IntPopup("Resolucao maxima das reflection probes", cfg.probeResMax, new[] { "64", "128", "256" }, new[] { 64, 128, 256 });
                    cfg.realtimeGIOff = EditorGUILayout.ToggleLeft("Realtime GI desligado", cfg.realtimeGIOff);
                }
                EditorGUI.indentLevel--;
            }

            foldEF = EditorGUILayout.Foldout(foldEF, "(e) Lightmapping e (f) Player/WebGL: so relatorio", true);
            if (foldEF)
                EditorGUILayout.HelpBox("Estas secoes nao mudam nada. Veja as linhas 'e-' e 'f-' no relatorio depois de Simular.", MessageType.None);

            cfg.exportarCsv = EditorGUILayout.ToggleLeft("Exportar CSV ao simular", cfg.exportarCsv);
            if (EditorGUI.EndChangeCheck()) SalvarConfig();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Simular (relatorio)", GUILayout.Height(30))) Simular();
            using (new EditorGUI.DisabledScope(!simulou))
                if (GUILayout.Button("Aplicar", GUILayout.Height(30))) Aplicar();
            if (GUILayout.Button("Restaurar backup", GUILayout.Height(30))) Restaurar();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Valores padrao")) { if (EditorUtility.DisplayDialog("Valores padrao", "Voltar todas as opcoes para o padrao?", "Sim", "Nao")) { cfg = new Config(); SalvarConfig(); } }
            if (GUILayout.Button("Abrir pasta de relatorios")) { Directory.CreateDirectory(PastaRelatorios); EditorUtility.RevealInFinder(PastaRelatorios); }
            if (GUILayout.Button("Abrir pasta de backups")) { Directory.CreateDirectory(PastaBackups); EditorUtility.RevealInFinder(PastaBackups); }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(ultimoCsv)) EditorGUILayout.LabelField("CSV: " + ultimoCsv, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(ultimoBackup)) EditorGUILayout.LabelField("Backup: " + ultimoBackup, EditorStyles.miniLabel);

            if (simulou)
            {
                EditorGUILayout.Space(4);
                soMudancas = EditorGUILayout.ToggleLeft("Mostrar so o que muda / avisos / recomendacoes", soMudancas);
                var st = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
                scrollLista = EditorGUILayout.BeginScrollView(scrollLista, GUILayout.MinHeight(220));
                foreach (var l in linhas)
                {
                    if (soMudancas && (l.acao == "OK" || l.acao == "Info")) continue;
                    var cor = l.acao == "Muda" ? "<color=#4fc3f7>" : l.acao == "Aviso" ? "<color=#ffb74d>" : l.acao == "Recomenda" ? "<color=#aed581>" : "<color=#bdbdbd>";
                    string txt = cor + "[" + l.acao + "]</color> <b>" + l.secao + "</b> " + l.item +
                                 (string.IsNullOrEmpty(l.atual) && string.IsNullOrEmpty(l.novo) ? "" : ": " + l.atual + (string.IsNullOrEmpty(l.novo) ? "" : "  ->  " + l.novo)) +
                                 (string.IsNullOrEmpty(l.obs) ? "" : "  <i>(" + l.obs + ")</i>");
                    EditorGUILayout.LabelField(txt, st);
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndScrollView();
        }

        void DesenharEstadoAtual()
        {
            int idx = IndiceNivel(cfg.nivelMobile);
            var rp = idx >= 0 ? QualitySettings.GetRenderPipelineAssetAt(idx) : null;
            EditorGUILayout.LabelField(string.Format("Plataforma ativa: {0}   |   Nivel '{1}': indice {2}, URP asset: {3}",
                EditorUserBuildSettings.activeBuildTarget, cfg.nivelMobile, idx, rp != null ? rp.name : "(nenhum)"), EditorStyles.miniLabel);
        }

        // ================================================================= helpers
        static int IndiceNivel(string nome) { return Array.IndexOf(QualitySettings.names, nome); }

        void Add(string secao, string item, string atual, string novo, string acao, string obs = "")
        {
            linhas.Add(new Linha { secao = secao, item = item, atual = atual ?? "", novo = novo ?? "", acao = acao, obs = obs ?? "" });
        }

        static UnityEngine.Object QualityObj()
        {
            var arr = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            return arr != null && arr.Length > 0 ? arr[0] : null;
        }

        static SerializedProperty AcharPorNome(SerializedProperty arr, string nome, out int indice)
        {
            indice = -1;
            if (arr == null) return null;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var n = e.FindPropertyRelative("name");
                if (n != null && n.stringValue == nome) { indice = i; return e; }
            }
            return null;
        }

        static SerializedProperty PlataformaPadrao(SerializedObject so, string plataforma)
        {
            var map = so.FindProperty("m_PerPlatformDefaultQuality");
            if (map == null || !map.isArray) return null;
            for (int i = 0; i < map.arraySize; i++)
            {
                var e = map.GetArrayElementAtIndex(i);
                var f = e.FindPropertyRelative("first");
                if (f != null && f.propertyType == SerializedPropertyType.String && f.stringValue == plataforma) return e.FindPropertyRelative("second");
            }
            return null;
        }

        static string ValorProp(SerializedProperty p)
        {
            if (p == null) return "(n/a)";
            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean: return p.boolValue ? "sim" : "nao";
                case SerializedPropertyType.Float: return p.floatValue.ToString("0.##", Inv);
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum: return p.intValue.ToString(Inv);
                case SerializedPropertyType.ObjectReference: return p.objectReferenceValue != null ? p.objectReferenceValue.name : "(nenhum)";
                default: return p.propertyType.ToString();
            }
        }

        class CampoUrp { public string campo, rotulo; public Func<string> novoTxt; public Action<SerializedProperty> aplicar; }

        List<CampoUrp> CamposUrp()
        {
            var c = cfg;
            var l = new List<CampoUrp>();
            Action<string, string, Func<string>, Action<SerializedProperty>> add = (f, r, t, a) => l.Add(new CampoUrp { campo = f, rotulo = r, novoTxt = t, aplicar = a });
            add("m_RenderScale", "Render Scale", () => c.renderScale.ToString("0.##", Inv), p => p.floatValue = c.renderScale);
            add("m_SupportsHDR", "HDR", () => c.hdr ? "sim" : "nao", p => p.boolValue = c.hdr);
            add("m_MSAA", "MSAA (amostras)", () => c.msaa.ToString(Inv), p => p.intValue = c.msaa);
            add("m_ShadowDistance", "Distancia de sombra", () => c.shadowDistance.ToString("0.##", Inv), p => p.floatValue = c.shadowDistance);
            add("m_ShadowCascadeCount", "Cascatas", () => c.cascades.ToString(Inv), p => p.intValue = c.cascades);
            add("m_MainLightShadowsSupported", "Sombra da luz principal", () => "sim", p => p.boolValue = true);
            add("m_MainLightShadowmapResolution", "Resolucao sombra principal", () => c.shadowRes.ToString(Inv), p => p.intValue = c.shadowRes);
            add("m_SoftShadowsSupported", "Sombras suaves", () => c.softShadows ? "sim" : "nao", p => p.boolValue = c.softShadows);
            if (c.softShadows) add("m_SoftShadowQuality", "Qualidade sombra suave", () => c.softShadowQuality.ToString(Inv), p => p.intValue = c.softShadowQuality);
            add("m_AdditionalLightsRenderingMode", "Luzes adicionais (0 off, 1 pixel, 2 vertice)", () => c.addLightsMode.ToString(Inv), p => p.intValue = c.addLightsMode);
            add("m_AdditionalLightsPerObjectLimit", "Limite de luzes por objeto", () => c.addLightsLimit.ToString(Inv), p => p.intValue = c.addLightsLimit);
            add("m_AdditionalLightShadowsSupported", "Sombras das luzes adicionais", () => c.addLightShadows ? "sim" : "nao", p => p.boolValue = c.addLightShadows);
            add("m_RequireDepthTexture", "Depth Texture", () => c.depthTexture ? "sim" : "nao", p => p.boolValue = c.depthTexture);
            add("m_RequireOpaqueTexture", "Opaque Texture", () => c.opaqueTexture ? "sim" : "nao", p => p.boolValue = c.opaqueTexture);
            add("m_UseSRPBatcher", "SRP Batcher", () => c.srpBatcher ? "sim" : "nao", p => p.boolValue = c.srpBatcher);
            if (c.desligarLightLayers) add("m_SupportsLightLayers", "Light Layers", () => "nao", p => p.boolValue = false);
            if (c.desligarLensFlare)
            {
                add("m_SupportDataDrivenLensFlare", "Lens Flare data-driven", () => "nao", p => p.boolValue = false);
                add("m_SupportScreenSpaceLensFlare", "Lens Flare screen space", () => "nao", p => p.boolValue = false);
            }
            if (c.desligarLodCrossFade) add("m_EnableLODCrossFade", "LOD Cross-Fade", () => "nao", p => p.boolValue = false);
            if (c.desligarProbeBlending)
            {
                add("m_ReflectionProbeBlending", "Reflection probe blending", () => "nao", p => p.boolValue = false);
                add("m_ReflectionProbeBoxProjection", "Reflection probe box projection", () => "nao", p => p.boolValue = false);
            }
            return l;
        }

        List<string> ListaFeaturesPesadas()
        {
            return (cfg.featuresPesadas ?? "").Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        bool FeatureEhPesada(ScriptableRendererFeature f)
        {
            if (f == null) return false;
            string tipo = f.GetType().Name;
            return ListaFeaturesPesadas().Any(n => tipo.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static string NomeRenderingMode(int v)
        {
            switch (v) { case 0: return "Forward"; case 1: return "Deferred"; case 2: return "Forward+"; case 3: return "Deferred+"; default: return v.ToString(Inv); }
        }

        static string On(bool b) { return b ? "off" : "manter"; }

        // ================================================================= SIMULAR
        void Simular()
        {
            linhas.Clear();
            cenas.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("Render mobile", "Quality / URP...", 0.05f);
                AuditarQuality();
                EditorUtility.DisplayProgressBar("Render mobile", "Renderer / Volumes...", 0.2f);
                AuditarRendererEVolumes();
                EditorUtility.DisplayProgressBar("Render mobile", "Shaders e cameras...", 0.35f);
                AuditarShadersECameras();
                EditorUtility.DisplayProgressBar("Render mobile", "Cenas do Build...", 0.5f);
                AuditarCenas();
                EditorUtility.DisplayProgressBar("Render mobile", "Player/WebGL...", 0.9f);
                AuditarPlayer();
            }
            catch (Exception e)
            {
                Debug.LogError("[MobileRender] Erro na simulacao: " + e);
                Add("!", "Erro na simulacao", e.Message, "", "Aviso");
            }
            finally { EditorUtility.ClearProgressBar(); }
            simulou = true;
            if (cfg.exportarCsv) ExportarCsv();
            Debug.Log(string.Format("[MobileRender] Simulacao: {0} mudancas previstas, {1} avisos. {2}",
                linhas.Count(x => x.acao == "Muda"), linhas.Count(x => x.acao == "Aviso"), ultimoCsv));
            Repaint();
        }

        void AuditarQuality()
        {
            var nomes = QualitySettings.names;
            var qo = QualityObj();
            var so = qo != null ? new SerializedObject(qo) : null;
            var niveis = so != null ? so.FindProperty("m_QualitySettings") : null;
            for (int i = 0; i < nomes.Length; i++)
            {
                var rp = QualitySettings.GetRenderPipelineAssetAt(i);
                string excl = "";
                if (niveis != null && i < niveis.arraySize)
                {
                    var ex = niveis.GetArrayElementAtIndex(i).FindPropertyRelative("excludedTargetPlatforms");
                    if (ex != null) for (int k = 0; k < ex.arraySize; k++) excl += (k > 0 ? "," : "") + ex.GetArrayElementAtIndex(k).stringValue;
                }
                Add("a-Quality", "Nivel " + i + " '" + nomes[i] + "'", "URP: " + (rp != null ? AssetDatabase.GetAssetPath(rp) : "(nenhum)"), "", "Info",
                    excl.Length > 0 ? "excluido em: " + excl : "");
            }
            if (so != null)
            {
                foreach (var plat in new[] { "WebGL", "Standalone", "Android", "iPhone" })
                {
                    var sp = PlataformaPadrao(so, plat);
                    string atual = sp != null && sp.intValue >= 0 && sp.intValue < nomes.Length ? nomes[sp.intValue] : "(nao definido)";
                    bool muda = plat == "WebGL" && cfg.secA && cfg.webglPadraoMobile && atual != cfg.nivelMobile;
                    Add("a-Quality", "Padrao " + plat, atual, muda ? cfg.nivelMobile : "", muda ? "Muda" : "Info");
                }
                var spW = PlataformaPadrao(so, "WebGL");
                if (spW != null && spW.intValue >= 0 && spW.intValue < nomes.Length && nomes[spW.intValue] == cfg.nivelMobile)
                    Add("a-Quality", "Troca PC x celular", "", "", "Aviso",
                        "Hoje o WebGL abre no nivel '" + cfg.nivelMobile + "' tambem no PC. Opcoes: 1) deixar assim (este projeto e so mobile); " +
                        "2) WebGL padrao = PC e marcar em (c) 'No celular, trocar para o Quality mobile ao iniciar' (um build so, PC continua bonito); " +
                        "3) dois builds (CiBuild PC + CiBuildMobile).");
            }

            int idx = IndiceNivel(cfg.nivelMobile);
            if (idx < 0) { Add("a-URP", "Nivel '" + cfg.nivelMobile + "'", "nao existe", "", "Aviso", "Corrija o nome do nivel mobile."); return; }
            var rpM = QualitySettings.GetRenderPipelineAssetAt(idx) as UniversalRenderPipelineAsset;
            if (rpM == null) { Add("a-URP", "URP asset do nivel mobile", "(nenhum / nao URP)", "", "Aviso"); return; }
            string pathM = AssetDatabase.GetAssetPath(rpM);
            bool jaCopia = pathM.StartsWith(PastaAssetsMobile + "/", StringComparison.OrdinalIgnoreCase);
            Add("a-URP", "Asset do nivel mobile", pathM,
                cfg.secA ? (jaCopia ? "(ja e a copia da tool; sera ajustada)" : PastaAssetsMobile + "/" + NomeAssetCopia + ".asset (copia nova)") : "",
                cfg.secA ? "Muda" : "Info", jaCopia ? "" : "o original fica intacto");
            var so2 = new SerializedObject(rpM);
            foreach (var c in CamposUrp())
            {
                var p = so2.FindProperty(c.campo);
                string atual = ValorProp(p);
                string novo = c.novoTxt();
                bool muda = cfg.secA && p != null && atual != novo;
                Add("a-URP", c.rotulo, atual, muda ? novo : "", p == null ? "Aviso" : (muda ? "Muda" : "OK"), p == null ? "campo " + c.campo + " nao encontrado nesta versao do URP" : "");
            }
            if (cfg.secA && cfg.mudarLodBias && niveis != null && idx < niveis.arraySize)
            {
                var lb = niveis.GetArrayElementAtIndex(idx).FindPropertyRelative("lodBias");
                Add("a-Quality", "LOD Bias do nivel mobile", lb != null ? lb.floatValue.ToString("0.##", Inv) : "(n/a)", cfg.lodBias.ToString("0.##", Inv), "Muda");
            }
            if (cfg.secA && cfg.excluirOutrosNiveisDoWebGL)
                Add("a-Quality", "Outros niveis no build WebGL", "incluidos", "excluidos (so '" + cfg.nivelMobile + "')", "Muda", "menos variantes de shader / build menor");
            var gd = GraphicsSettings.defaultRenderPipeline;
            Add("a-Graphics", "Default Render Pipeline", gd != null ? AssetDatabase.GetAssetPath(gd) : "(nenhum)",
                cfg.secA && cfg.graphicsPadraoMobile ? "copia mobile" : "", cfg.secA && cfg.graphicsPadraoMobile ? "Muda" : "Info",
                "cada nivel de Quality tem asset proprio; este so vale se um nivel nao tiver, mas entra no build e gera variantes");

            var usados = new HashSet<string>();
            for (int i = 0; i < nomes.Length; i++) { var r = QualitySettings.GetRenderPipelineAssetAt(i); if (r != null) usados.Add(AssetDatabase.GetAssetPath(r)); }
            if (gd != null) usados.Add(AssetDatabase.GetAssetPath(gd));
            var semUso = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset").Select(AssetDatabase.GUIDToAssetPath).Where(pth => !usados.Contains(pth)).ToList();
            if (semUso.Count > 0)
                Add("a-URP", "URP assets sem uso (" + semUso.Count + ")", string.Join(" | ", semUso.ToArray()), "", "Info", "nao entram no build; podem ser apagados depois (manual)");
        }

        void AuditarRendererEVolumes()
        {
            int idx = IndiceNivel(cfg.nivelMobile);
            var rpM = idx >= 0 ? QualitySettings.GetRenderPipelineAssetAt(idx) as UniversalRenderPipelineAsset : null;
            if (rpM != null)
            {
                var lista = new SerializedObject(rpM).FindProperty("m_RendererDataList");
                for (int i = 0; lista != null && i < lista.arraySize; i++)
                {
                    var rd = lista.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                    if (rd == null) continue;
                    var sr = new SerializedObject(rd);
                    bool b = cfg.secA && cfg.secB;
                    var modo = sr.FindProperty("m_RenderingMode");
                    bool mudaModo = b && cfg.forwardSimples && modo != null && modo.intValue != 0;
                    Add("b-Renderer", rd.name + " rendering path", modo != null ? NomeRenderingMode(modo.intValue) : "(n/a)", mudaModo ? "Forward" : "", mudaModo ? "Muda" : "OK");
                    var str = sr.FindProperty("m_ShadowTransparentReceive");
                    if (str != null)
                    {
                        bool m = b && cfg.desligarSombraTransparente && str.boolValue;
                        Add("b-Renderer", rd.name + " transparentes recebem sombra", str.boolValue ? "sim" : "nao", m ? "nao" : "", m ? "Muda" : "OK");
                    }
                    if (rd.rendererFeatures.Count == 0) Add("b-Renderer", rd.name + " renderer features", "nenhuma", "", "OK");
                    foreach (var f in rd.rendererFeatures)
                    {
                        if (f == null) { Add("b-Renderer", rd.name + " feature", "(script faltando)", "", "Aviso"); continue; }
                        bool m = b && cfg.desligarFeaturesPesadas && FeatureEhPesada(f) && f.isActive;
                        Add("b-Renderer", rd.name + " feature " + f.name + " (" + f.GetType().Name + ")", f.isActive ? "ativa" : "desligada", m ? "desligada (so na copia)" : "", m ? "Muda" : "OK");
                    }
                }
            }
            foreach (var g in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.StartsWith("Assets/Settings/", StringComparison.OrdinalIgnoreCase)) continue;
                var rd = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rd == null) continue;
                var feats = string.Join(", ", rd.rendererFeatures.Where(f => f != null).Select(f => f.name + (f.isActive ? "" : " (off)")).ToArray());
                var m = new SerializedObject(rd).FindProperty("m_RenderingMode");
                Add("b-Renderer", "Info " + path, (m != null ? NomeRenderingMode(m.intValue) : "") + (feats.Length > 0 ? " | features: " + feats : " | sem features"), "", "Info");
            }

            // perfis de Volume com efeitos caros ligados
            foreach (var g in AssetDatabase.FindAssets("t:VolumeProfile"))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (vp == null) continue;
                var ativos = new List<string>();
                bool caro = false;
                foreach (var comp in vp.components)
                {
                    if (comp == null || !comp.active) continue;
                    var bl = comp as Bloom;
                    if (bl != null && bl.intensity.value > 0f)
                    {
                        ativos.Add(string.Format(Inv, "Bloom(int {0:0.##}, threshold {1:0.##}, HQ {2})", bl.intensity.value, bl.threshold.value, bl.highQualityFiltering.value ? "sim" : "nao"));
                        caro = true; continue;
                    }
                    var dof = comp as DepthOfField;
                    if (dof != null && dof.mode.value != DepthOfFieldMode.Off) { ativos.Add("DepthOfField(" + dof.mode.value + ")"); caro = true; continue; }
                    var mb = comp as MotionBlur; if (mb != null && mb.intensity.value > 0f) { ativos.Add("MotionBlur"); caro = true; continue; }
                    var ca = comp as ChromaticAberration; if (ca != null && ca.intensity.value > 0f) { ativos.Add("ChromaticAberration"); caro = true; continue; }
                    var fg = comp as FilmGrain; if (fg != null && fg.intensity.value > 0f) { ativos.Add("FilmGrain"); caro = true; continue; }
                    var ld = comp as LensDistortion; if (ld != null && Mathf.Abs(ld.intensity.value) > 0.001f) { ativos.Add("LensDistortion"); continue; }
                    var vg = comp as Vignette; if (vg != null && vg.intensity.value > 0f) { ativos.Add("Vignette"); continue; }
                    var pn = comp as PaniniProjection; if (pn != null && pn.distance.value > 0f) { ativos.Add("PaniniProjection"); continue; }
                    if (comp is ColorAdjustments || comp is Tonemapping || comp is WhiteBalance || comp is LiftGammaGain || comp is ShadowsMidtonesHighlights ||
                        comp is ChannelMixer || comp is ColorCurves || comp is SplitToning || comp is ColorLookup)
                    {
                        if (comp.parameters.Any(pp => pp.overrideState)) ativos.Add(comp.GetType().Name);
                    }
                }
                if (ativos.Count == 0) continue;
                Add("c-Volume", path, string.Join(", ", ativos.ToArray()), cfg.secC && caro ? "reduzido em runtime no mobile (perfil intacto)" : "", caro ? "Aviso" : "Info");
            }
            string resumo = cfg.secC
                ? string.Format("DOF {0}, MotionBlur {1}, Chromatic {2}, FilmGrain {3}, LensDist {4}, Panini {5}, Vignette {6}, Bloom {7}, AA camera {8}{9}",
                    On(cfg.desligarDOF), On(cfg.desligarMotionBlur), On(cfg.desligarChromatic), On(cfg.desligarFilmGrain), On(cfg.desligarLensDistortion),
                    On(cfg.desligarPanini), On(cfg.desligarVignette), new[] { "manter", "leve", "off" }[Mathf.Clamp(cfg.bloom, 0, 2)],
                    cfg.aaCamera < 0 ? "manter" : cfg.aaCamera == 0 ? "nenhum" : "FXAA", cfg.desligarPosNaCamera ? ", pos da camera OFF" : "")
                : "";
            bool existeCfg = File.Exists(Path.Combine(RaizProjeto, CaminhoConfigRuntime));
            Add("c-Runtime", CaminhoConfigRuntime, existeCfg ? "existe" : "nao existe", resumo, cfg.secC ? "Muda" : "Info",
                "vale quando " + (cfg.somenteEmCelular ? "for celular" : "o Quality ativo for '" + cfg.nivelMobile + "'") + (cfg.trocarNivelNoCelular ? "; no celular troca para o nivel mobile" : ""));
            if (Type.GetType("EVirtualMobile.MobileRenderRuntime, Assembly-CSharp") == null)
                Add("c-Runtime", "Assets/Scripts/Mobile/MobileRenderRuntime.cs", "nao encontrado", "", "Aviso", "sem esse script a secao (c) nao tem efeito");
        }

        void AuditarShadersECameras()
        {
            var tokens = new[] { "SceneDepth", "SceneColor", "_CameraOpaqueTexture", "_CameraDepthTexture", "SampleSceneDepth", "SampleSceneColor", "DeclareOpaqueTexture", "DeclareDepthTexture" };
            var precisam = new List<string>();
            foreach (var ext in new[] { "*.shader", "*.shadergraph", "*.shadersubgraph", "*.hlsl" })
            {
                foreach (var f in Directory.GetFiles(Application.dataPath, ext, SearchOption.AllDirectories))
                {
                    var rel = "Assets" + f.Substring(Application.dataPath.Length).Replace('\\', '/');
                    string t;
                    try { t = File.ReadAllText(f); } catch { continue; }
                    var hit = tokens.Where(k => t.IndexOf(k, StringComparison.Ordinal) >= 0).ToList();
                    if (hit.Count > 0) precisam.Add(rel + " [" + string.Join(",", hit.ToArray()) + "]");
                }
            }
            if (precisam.Count == 0)
                Add("a-Shaders", "Shaders que leem Depth/Opaque Texture", "nenhum encontrado em Assets/", "", "OK", "pode desligar Depth/Opaque Texture no mobile");
            else
                Add("a-Shaders", "Shaders que leem Depth/Opaque Texture (" + precisam.Count + ")", string.Join(" | ", precisam.ToArray()), "",
                    (!cfg.depthTexture || !cfg.opaqueTexture) ? "Aviso" : "Info", "se usados no Build, ligue Depth/Opaque Texture");
            if (!cfg.desligarDOF && !cfg.depthTexture)
                Add("a-Shaders", "Depth of Field", "", "", "Aviso", "DOF precisa de depth texture; o URP liga sozinho quando o DOF esta ativo (custo extra)");

            foreach (var g in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                string t;
                try { t = File.ReadAllText(Path.Combine(RaizProjeto, path)); } catch { continue; }
                if (t.IndexOf("--- !u!20 ", StringComparison.Ordinal) < 0) continue;
                Add("c-Camera", path, ResumoCameraYaml(t), cfg.secC && cfg.aaCamera >= 0 ? "AA em runtime: " + (cfg.aaCamera == 0 ? "nenhum" : "FXAA") : "", "Info");
            }
        }

        static string ResumoCameraYaml(string t)
        {
            Func<string, string> g = k => { var m = Regex.Match(t, @"^\s*" + Regex.Escape(k) + @":\s*(.*)$", RegexOptions.Multiline); return m.Success ? m.Groups[1].Value.Trim() : "?"; };
            string aa = g("m_Antialiasing");
            string aaN = aa == "0" ? "nenhum" : aa == "1" ? "FXAA" : aa == "2" ? "SMAA" : aa == "3" ? "TAA" : aa;
            Func<string, string> opt = v => v == "0" ? "off" : v == "1" ? "on" : v == "2" ? "do asset" : v;
            return string.Format("HDR {0}, MSAA {1}, Pos {2}, AA {3} (qualidade {4}), Depth {5}, Opaque {6}, far {7}",
                g("m_HDR") == "1" ? "sim" : "nao", g("m_AllowMSAA") == "1" ? "sim" : "nao", g("m_RenderPostProcessing") == "1" ? "sim" : "nao",
                aaN, g("m_AntialiasingQuality"), opt(g("m_RequiresDepthTextureOption")), opt(g("m_RequiresOpaqueTextureOption")), g("far clip plane"));
        }

        // ----------------------------------------------------------------- cenas via YAML (sem abrir)
        static List<KeyValuePair<int, string>> DocsYaml(string texto, out Dictionary<string, string> nomesGO)
        {
            var res = new List<KeyValuePair<int, string>>();
            nomesGO = new Dictionary<string, string>();
            var ms = Regex.Matches(texto, @"^--- !u!(\d+) &(-?\d+)[^\n]*\n", RegexOptions.Multiline);
            for (int i = 0; i < ms.Count; i++)
            {
                int ini = ms[i].Index + ms[i].Length;
                int fim = i + 1 < ms.Count ? ms[i + 1].Index : texto.Length;
                int cls = int.Parse(ms[i].Groups[1].Value, Inv);
                string corpo = texto.Substring(ini, fim - ini);
                res.Add(new KeyValuePair<int, string>(cls, corpo));
                if (cls == 1) nomesGO[ms[i].Groups[2].Value] = Campo(corpo, "m_Name");
            }
            return res;
        }

        static string Campo(string corpo, string k)
        {
            var m = Regex.Match(corpo, @"^\s*" + Regex.Escape(k) + @":\s*(.*)$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        static int CampoInt(string corpo, string k, int def = 0)
        {
            int v; return int.TryParse(Campo(corpo, k), NumberStyles.Integer, Inv, out v) ? v : def;
        }

        static float CampoFloat(string corpo, string k, float def = 0f)
        {
            float v; return float.TryParse(Campo(corpo, k), NumberStyles.Float, Inv, out v) ? v : def;
        }

        static string NomeGO(Dictionary<string, string> nomes, string corpo)
        {
            var m = Regex.Match(Campo(corpo, "m_GameObject"), @"fileID: (-?\d+)");
            string n;
            return m.Success && nomes.TryGetValue(m.Groups[1].Value, out n) ? n : "(prefab)";
        }

        static string GuidParaPath(string refTxt)
        {
            var m = Regex.Match(refTxt ?? "", @"guid: ([0-9a-f]{32})");
            if (!m.Success) return "";
            var p = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
            return string.IsNullOrEmpty(p) ? "(guid " + m.Groups[1].Value + " nao encontrado)" : p;
        }

        static string NomeTipoLuz(int t) { switch (t) { case 0: return "Spot"; case 1: return "Directional"; case 2: return "Point"; case 3: return "Area"; case 4: return "Disc"; default: return t.ToString(Inv); } }
        static string NomeModoLuz(int m) { switch (m) { case 4: return "Realtime"; case 1: return "Mixed"; case 2: return "Baked"; default: return m.ToString(Inv); } }
        static string NomeSombra(int s) { switch (s) { case 0: return "sem sombra"; case 1: return "Hard"; case 2: return "Soft"; default: return s.ToString(Inv); } }

        static List<string> CenasDoBuild()
        {
            return EditorBuildSettings.scenes.Where(s => s.enabled && !string.IsNullOrEmpty(s.path)).Select(s => s.path).ToList();
        }

        void AuditarCenas()
        {
            var lista = CenasDoBuild();
            var lightingDataUso = new Dictionary<string, int>();
            for (int si = 0; si < lista.Count; si++)
            {
                var path = lista[si];
                string nomeCena = Path.GetFileNameWithoutExtension(path);
                EditorUtility.DisplayProgressBar("Render mobile", "Cena " + nomeCena, 0.5f + 0.35f * si / Math.Max(1, lista.Count));
                var full = Path.Combine(RaizProjeto, path);
                if (!File.Exists(full)) { Add("d-Cena", path, "arquivo nao encontrado", "", "Aviso"); continue; }
                Dictionary<string, string> nomes;
                var docs = DocsYaml(File.ReadAllText(full), out nomes);
                var ci = new CenaInfo { path = path };
                var volumes = new List<string>();
                foreach (var d in docs)
                {
                    var b = d.Value;
                    switch (d.Key)
                    {
                        case 104:
                            ci.fog = Campo(b, "m_Fog") == "1";
                            ci.skybox = GuidParaPath(Campo(b, "m_SkyboxMaterial"));
                            break;
                        case 157:
                            ci.realtimeGI = Campo(b, "m_EnableRealtimeLightmaps") == "1";
                            ci.bakedGI = Campo(b, "m_EnableBakedLightmaps") == "1";
                            ci.lightingDataPath = GuidParaPath(Campo(b, "m_LightingDataAsset"));
                            ci.lightingSettingsPath = GuidParaPath(Campo(b, "m_LightingSettings"));
                            break;
                        case 108:
                            var sm = Regex.Match(b, @"m_Shadows:\s*\n\s*m_Type:\s*(\d+)\s*\n\s*m_Resolution:\s*(-?\d+)");
                            ci.luzes.Add(new LuzYaml
                            {
                                nome = NomeGO(nomes, b), tipo = CampoInt(b, "m_Type"), modo = CampoInt(b, "m_Lightmapping", 4),
                                sombra = sm.Success ? int.Parse(sm.Groups[1].Value, Inv) : 0, res = sm.Success ? int.Parse(sm.Groups[2].Value, Inv) : -1,
                                ligada = Campo(b, "m_Enabled") != "0", intensidade = CampoFloat(b, "m_Intensity", 1f), alcance = CampoFloat(b, "m_Range", 10f)
                            });
                            break;
                        case 215:
                            ci.probes.Add(new ProbeYaml { nome = NomeGO(nomes, b), modo = CampoInt(b, "m_Mode"), res = CampoInt(b, "m_Resolution", 128) });
                            break;
                        case 220: ci.lightProbeGroups++; break;
                        case 1001: ci.prefabInstances++; break;
                        case 114:
                            if (b.IndexOf("sharedProfile:", StringComparison.Ordinal) >= 0)
                                volumes.Add(NomeGO(nomes, b) + " -> " + GuidParaPath(Campo(b, "sharedProfile")) + (Campo(b, "m_IsGlobal") == "1" ? " (global)" : " (local)"));
                            break;
                    }
                }
                cenas.Add(ci);

                var grupos = ci.luzes.GroupBy(l => NomeTipoLuz(l.tipo) + "/" + NomeModoLuz(l.modo) + "/" + (l.sombra > 0 ? "com sombra" : "sem sombra"))
                    .Select(gp => gp.Count() + "x " + gp.Key).ToArray();
                Add("d-Luzes", nomeCena, ci.luzes.Count + " luz(es): " + string.Join(", ", grupos), "", "Info",
                    ci.prefabInstances + " prefabs na cena (luzes dentro de prefab so aparecem ao Aplicar)");

                // plano (mesma logica do Aplicar)
                var realtime = ci.luzes.Where(l => l.modo != 2 && l.ligada).ToList();
                var principal = realtime.Where(l => l.tipo == 1).OrderByDescending(l => l.intensidade).FirstOrDefault();
                if (cfg.secD)
                {
                    foreach (var l in realtime)
                    {
                        if (cfg.sombraSoNaLuzPrincipal && l != principal && l.sombra > 0)
                        { Add("d-Luzes", nomeCena + " / " + l.nome, NomeTipoLuz(l.tipo) + " " + NomeSombra(l.sombra), "sem sombra", "Muda"); ci.mudancasPrevistas++; }
                        else if (cfg.sombraResolucaoBaixa && l.sombra > 0 && l.res > 0)
                        { Add("d-Luzes", nomeCena + " / " + l.nome, "resolucao de sombra " + l.res, "Low", "Muda"); ci.mudancasPrevistas++; }
                    }
                    if (cfg.limitarLuzes)
                    {
                        var extras = realtime.Where(l => l != principal && (l.tipo == 0 || l.tipo == 2)).OrderByDescending(l => l.intensidade * l.alcance).ToList();
                        for (int k = cfg.maxLuzesExtras; k < extras.Count; k++)
                        { Add("d-Luzes", nomeCena + " / " + extras[k].nome, "ligada", "desligada (limite " + cfg.maxLuzesExtras + ")", "Muda"); ci.mudancasPrevistas++; }
                    }
                    foreach (var pr in ci.probes)
                    {
                        if (cfg.probesParaBaked && pr.modo == 1)
                        { Add("d-Probes", nomeCena + " / " + pr.nome, "Realtime", "Baked (fazer bake)", "Muda"); ci.mudancasPrevistas++; }
                        if (pr.res > cfg.probeResMax)
                        { Add("d-Probes", nomeCena + " / " + pr.nome, "resolucao " + pr.res, cfg.probeResMax.ToString(Inv), "Muda"); ci.mudancasPrevistas++; }
                    }
                    if (cfg.realtimeGIOff && ci.realtimeGI && !string.IsNullOrEmpty(ci.lightingSettingsPath))
                    { Add("d-GI", nomeCena, "Realtime GI ligado", "desligado (" + ci.lightingSettingsPath + ")", "Muda"); ci.mudancasPrevistas++; }
                }

                if (principal != null)
                    Add("d-Luzes", nomeCena + " / principal: " + principal.nome, NomeModoLuz(principal.modo) + ", " + NomeSombra(principal.sombra) + ", intensidade " + principal.intensidade.ToString("0.##", Inv), "", "OK",
                        principal.sombra == 2 && !cfg.softShadows ? "Soft na luz, mas o URP mobile esta sem sombras suaves: vira Hard" : "");
                foreach (var pr in ci.probes)
                    Add("d-Probes", nomeCena + " / " + pr.nome, (pr.modo == 1 ? "Realtime" : pr.modo == 0 ? "Baked" : "Custom") + ", resolucao " + pr.res, "", pr.modo == 1 ? "Aviso" : "Info");
                Add("d-Probes", nomeCena, ci.probes.Count + " reflection probe(s), " + ci.lightProbeGroups + " light probe group(s)", "", "Info");
                Add("d-GI", nomeCena, "Realtime GI " + (ci.realtimeGI ? "ligado" : "desligado") + ", Baked GI " + (ci.bakedGI ? "ligado" : "desligado") + ", Fog " + (ci.fog ? "ligado" : "desligado"),
                    "", ci.realtimeGI ? "Aviso" : "OK", ci.realtimeGI && string.IsNullOrEmpty(ci.lightingSettingsPath) ? "sem Lighting Settings asset: desligue em Window > Rendering > Lighting" : "");
                foreach (var v in volumes) Add("c-Volume", nomeCena + " usa " + v, "", "", "Info");
                if (cfg.secD && ci.mudancasPrevistas == 0) Add("d-Cena", nomeCena, "nada a mudar", "a cena NAO sera aberta nem salva", "OK");

                // (e) lightmapping
                string ld = ci.lightingDataPath;
                bool ldValido = !string.IsNullOrEmpty(ld) && !ld.StartsWith("(");
                if (ldValido) { int n; lightingDataUso.TryGetValue(ld, out n); lightingDataUso[ld] = n + 1; }
                string pastaCena = (Path.GetDirectoryName(path) ?? "").Replace('\\', '/');
                bool ldDeOutra = ldValido && !ld.StartsWith(pastaCena + "/" + nomeCena + "/", StringComparison.OrdinalIgnoreCase);
                int lightmaps = 0;
                if (ldValido)
                {
                    var dir = Path.GetDirectoryName(Path.Combine(RaizProjeto, ld));
                    if (Directory.Exists(dir)) lightmaps = Directory.GetFiles(dir, "Lightmap-*").Count(f => !f.EndsWith(".meta"));
                }
                int baked = ci.luzes.Count(l => l.modo == 2), mixed = ci.luzes.Count(l => l.modo == 1);
                Add("e-Lightmap", nomeCena, string.Format("LightingData: {0} | lightmaps: {1} | luzes baked {2}, mixed {3} | Lighting Settings: {4}",
                    string.IsNullOrEmpty(ld) ? "(nenhum)" : ld, lightmaps, baked, mixed, string.IsNullOrEmpty(ci.lightingSettingsPath) ? "(padrao)" : ci.lightingSettingsPath),
                    "", ldDeOutra ? "Aviso" : "Info", ldDeOutra ? "LightingData de OUTRA cena: a luz ambiente e o reflexo do ceu vem de la. Gere a iluminacao desta cena." : "");
            }
            foreach (var kv in lightingDataUso.Where(k => k.Value > 1))
                Add("e-Lightmap", "LightingData compartilhado", kv.Key, "", "Aviso", kv.Value + " cenas do Build usam o mesmo LightingData");
            Add("e-Lightmap", "Recomendacao de bake mobile", "", "", "Recomenda",
                "Por cena, em Window > Rendering > Lighting: criar um Lighting Settings asset 'Mobile', Baked GI ligado, Realtime GI desligado, " +
                "Lightmapper Progressive GPU, Lightmap Resolution 8-15 texels/m, Max Lightmap Size 1024, Directional Mode Non-Directional (metade da memoria), " +
                "Lightmap Compression High Quality, Mixed Lighting Mode Subtractive (ou Shadowmask se precisar sombra dinamica longe), luz principal Mixed, " +
                "so os objetos fixos com Contribute GI. Depois Generate Lighting. Isso tambem cria a luz ambiente e o reflexo do ceu da propria cena.");
        }

        void AuditarPlayer()
        {
            Add("f-Player", "Color Space", PlayerSettings.colorSpace.ToString(), "", "OK", "Linear e ok no WebGL2");
            bool auto = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL);
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
            Add("f-Player", "Graphics API WebGL", (auto ? "Automatico: " : "") + string.Join(", ", apis.Select(a => a.ToString()).ToArray()), "", "Info",
                "WebGL2 e o mais compativel (iOS Safari e Android Chrome). WebGPU ainda e experimental.");
            var comp = PlayerSettings.WebGL.compressionFormat;
            Add("f-Player", "Compressao WebGL", comp.ToString(), "", comp == WebGLCompressionFormat.Disabled ? "Recomenda" : "OK",
                "Recomendado Brotli (ou Gzip). So ligue quando o servidor/CloudFront mandar o header Content-Encoding certo; senao deixe Decompression Fallback ligado.");
            Add("f-Player", "Decompression Fallback", PlayerSettings.WebGL.decompressionFallback ? "sim" : "nao", "", "Info");
            Add("f-Player", "Data Caching", PlayerSettings.WebGL.dataCaching ? "sim" : "nao", "", "OK", "bom para a segunda visita");
            Add("f-Player", "Exceptions", PlayerSettings.WebGL.exceptionSupport.ToString(), "", "Info", "'None' deixa o wasm um pouco menor e mais rapido, mas esconde erros");
            Add("f-Player", "Memoria inicial / maxima (MB)", PlayerSettings.WebGL.initialMemorySize + " / " + PlayerSettings.WebGL.maximumMemorySize, "", "Info",
                "iOS Safari costuma fechar a aba perto de 1-1,5 GB; manter texturas e audio leves");
            Add("f-Player", "Power preference", PlayerSettings.WebGL.powerPreference.ToString(), "", "Info");
            Add("f-Player", "Texturas WebGL", "ver Build Profiles > Web > Texture Compression", "", "Recomenda",
                "Para celular use ASTC (a tool Texturas mobile cuida disso). DXT no celular vira textura sem compressao na memoria.");
            Add("f-Player", "Resolucao do canvas", "devicePixelRatio do navegador", "", "Recomenda",
                "Celular tem devicePixelRatio 2-3: o jogo renderiza 2-3x mais pixels. No index.html do template/site use config.devicePixelRatio = Math.min(window.devicePixelRatio, 1.5) (ou 1). Ganho maior que o Render Scale.");
        }

        void ExportarCsv()
        {
            try
            {
                Directory.CreateDirectory(PastaRelatorios);
                var arq = Path.Combine(PastaRelatorios, "render_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                var sb = new StringBuilder();
                sb.AppendLine("Secao;Item;Atual;Novo;Acao;Obs");
                Func<string, string> esc = s => "\"" + (s ?? "").Replace("\"", "'").Replace("\n", " ").Replace("\r", " ") + "\"";
                foreach (var l in linhas) sb.AppendLine(string.Join(";", new[] { esc(l.secao), esc(l.item), esc(l.atual), esc(l.novo), esc(l.acao), esc(l.obs) }));
                File.WriteAllText(arq, sb.ToString(), new UTF8Encoding(true));
                ultimoCsv = arq;
            }
            catch (Exception e) { Debug.LogWarning("[MobileRender] Falha ao salvar CSV: " + e.Message); }
        }

        // ================================================================= APLICAR
        void Aplicar()
        {
            Simular(); // plano atualizado
            int mudancas = linhas.Count(l => l.acao == "Muda");
            var cenasMudar = cfg.secD ? cenas.Where(c => c.mudancasPrevistas > 0).ToList() : new List<CenaInfo>();
            var resumo = new StringBuilder();
            resumo.AppendLine(mudancas + " itens marcados como 'Muda' (veja a lista/CSV).");
            if (cfg.secA) resumo.AppendLine("- (a) copia do URP asset mobile + Quality Level");
            if (cfg.secA && cfg.secB) resumo.AppendLine("- (b) copia do renderer");
            if (cfg.secC) resumo.AppendLine("- (c) " + CaminhoConfigRuntime);
            if (cenasMudar.Count > 0) resumo.AppendLine("- (d) cenas que serao abertas e salvas: " + CenasTexto(cenasMudar));
            else if (cfg.secD) resumo.AppendLine("- (d) nenhuma cena precisa mudar (nenhuma sera aberta)");
            resumo.AppendLine("\nBackup em _Backup_Deletados/Render_<data>/. (e) e (f) nao mudam nada.");
            if (!EditorUtility.DisplayDialog("Aplicar render mobile", resumo.ToString(), "Aplicar", "Cancelar")) return;

            if (cenasMudar.Count > 0 && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            { EditorUtility.DisplayDialog("Render mobile", "Cancelado: salve ou descarte as cenas abertas primeiro.", "OK"); return; }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string pastaBackup = Path.Combine(PastaBackups, "Render_" + stamp);
            var man = new Manifesto { data = stamp, nivelMobile = cfg.nivelMobile };

            // ---------- backup ANTES de qualquer mudanca
            try
            {
                Directory.CreateDirectory(pastaBackup);
                Backup(pastaBackup, "ProjectSettings/QualitySettings.asset", null);
                Backup(pastaBackup, "ProjectSettings/GraphicsSettings.asset", null);
                int idx0 = IndiceNivel(cfg.nivelMobile);
                var rp0 = idx0 >= 0 ? QualitySettings.GetRenderPipelineAssetAt(idx0) as UniversalRenderPipelineAsset : null;
                if (rp0 != null)
                {
                    var p0 = AssetDatabase.GetAssetPath(rp0);
                    bool ehCopia = EhDaTool(p0);
                    Backup(pastaBackup, p0, ehCopia && cfg.secA ? man : null);
                    var lst = new SerializedObject(rp0).FindProperty("m_RendererDataList");
                    for (int i = 0; lst != null && i < lst.arraySize; i++)
                    {
                        var r = lst.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (r == null) continue;
                        var rpth = AssetDatabase.GetAssetPath(r);
                        Backup(pastaBackup, rpth, EhDaTool(rpth) && cfg.secA && cfg.secB ? man : null);
                    }
                }
                if (cfg.secC && File.Exists(Path.Combine(RaizProjeto, CaminhoConfigRuntime))) Backup(pastaBackup, CaminhoConfigRuntime, man);
                foreach (var c in cenasMudar) Backup(pastaBackup, c.path, man);
                foreach (var c in cenasMudar)
                    if (cfg.realtimeGIOff && c.realtimeGI && !string.IsNullOrEmpty(c.lightingSettingsPath) && !c.lightingSettingsPath.StartsWith("("))
                        Backup(pastaBackup, c.lightingSettingsPath, man);
                SalvarManifesto(pastaBackup, man);
            }
            catch (Exception e)
            {
                Debug.LogError("[MobileRender] Falha no backup, nada foi alterado: " + e);
                EditorUtility.DisplayDialog("Render mobile", "Falha no backup. Nada foi alterado.\n" + e.Message, "OK");
                return;
            }
            ultimoBackup = pastaBackup;

            var log = new StringBuilder();
            try
            {
                UniversalRenderPipelineAsset copia = null;
                if (cfg.secA) copia = AplicarA(man, log);
                SalvarManifesto(pastaBackup, man);
                if (cfg.secA && copia != null) AplicarQuality(copia, man, log);
                SalvarManifesto(pastaBackup, man);
                if (cfg.secC) AplicarC(man, log);
                SalvarManifesto(pastaBackup, man);
                AssetDatabase.SaveAssets();
                if (cenasMudar.Count > 0) AplicarD(cenasMudar, log);
            }
            catch (Exception e)
            {
                Debug.LogError("[MobileRender] Erro ao aplicar (use Restaurar backup): " + e);
                log.AppendLine("ERRO: " + e.Message + " -> use Restaurar backup");
            }
            finally
            {
                SalvarManifesto(pastaBackup, man);
                File.WriteAllText(Path.Combine(pastaBackup, "LEIA-ME.txt"),
                    "Backup antes de Mobile > Tools > Iluminacao e Render mobile (" + stamp + ").\n" +
                    "Para voltar: abra a ferramenta, clique em 'Restaurar backup' e escolha esta pasta.\n" +
                    "ProjectSettings/*.asset aqui sao so para conferencia; a ferramenta restaura esses valores pelo manifesto.json.\n\n" + log, new UTF8Encoding(true));
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();
            }
            Debug.Log("[MobileRender] Aplicado. Backup: " + pastaBackup + "\n" + log);
            EditorUtility.DisplayDialog("Render mobile", "Pronto.\n\n" + log + "\nBackup: " + pastaBackup, "OK");
            Simular();
        }

        static bool EhDaTool(string path) { return !string.IsNullOrEmpty(path) && path.StartsWith(PastaAssetsMobile + "/", StringComparison.OrdinalIgnoreCase); }

        static string CenasTexto(List<CenaInfo> l) { return string.Join(", ", l.Select(c => Path.GetFileNameWithoutExtension(c.path)).ToArray()); }

        static void Backup(string pastaBackup, string rel, Manifesto man)
        {
            if (string.IsNullOrEmpty(rel)) return;
            var src = Path.Combine(RaizProjeto, rel);
            if (!File.Exists(src)) return;
            var dst = Path.Combine(pastaBackup, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
            if (File.Exists(src + ".meta")) File.Copy(src + ".meta", dst + ".meta", true);
            if (man != null && !man.copiados.Contains(rel)) man.copiados.Add(rel);
        }

        static void SalvarManifesto(string pasta, Manifesto man)
        {
            File.WriteAllText(Path.Combine(pasta, "manifesto.json"), JsonUtility.ToJson(man, true), new UTF8Encoding(false));
        }

        static Manifesto CarregarManifesto(string pasta)
        {
            var f = Path.Combine(pasta, "manifesto.json");
            if (!File.Exists(f)) return null;
            try { return JsonUtility.FromJson<Manifesto>(File.ReadAllText(f)); } catch { return null; }
        }

        static void CriarPasta(string pasta)
        {
            if (AssetDatabase.IsValidFolder(pasta)) return;
            var pai = (Path.GetDirectoryName(pasta) ?? "").Replace('\\', '/');
            CriarPasta(pai);
            AssetDatabase.CreateFolder(pai, Path.GetFileName(pasta));
        }

        // ---------- (a) + (b)
        UniversalRenderPipelineAsset AplicarA(Manifesto man, StringBuilder log)
        {
            int idx = IndiceNivel(cfg.nivelMobile);
            if (idx < 0) { log.AppendLine("(a) Nivel '" + cfg.nivelMobile + "' nao existe. Pulado."); return null; }
            var orig = QualitySettings.GetRenderPipelineAssetAt(idx) as UniversalRenderPipelineAsset;
            if (orig == null) { log.AppendLine("(a) Nivel mobile sem URP asset. Pulado."); return null; }
            string pOrig = AssetDatabase.GetAssetPath(orig);
            UniversalRenderPipelineAsset copia;
            if (EhDaTool(pOrig))
            {
                copia = orig; // ja e a copia da tool (backup feito antes)
                log.AppendLine("(a) Ajustando a copia existente " + pOrig);
            }
            else
            {
                CriarPasta(PastaAssetsMobile);
                string pCopia = AssetDatabase.GenerateUniqueAssetPath(PastaAssetsMobile + "/" + NomeAssetCopia + ".asset");
                if (!AssetDatabase.CopyAsset(pOrig, pCopia)) throw new Exception("CopyAsset falhou: " + pOrig);
                man.criados.Add(pCopia);
                copia = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pCopia);
                log.AppendLine("(a) Copia criada: " + pCopia + " (de " + pOrig + ")");

                if (cfg.secB)
                {
                    var soC = new SerializedObject(copia);
                    var lista = soC.FindProperty("m_RendererDataList");
                    for (int i = 0; lista != null && i < lista.arraySize; i++)
                    {
                        var el = lista.GetArrayElementAtIndex(i);
                        var rd = el.objectReferenceValue as ScriptableRendererData;
                        if (rd == null) continue;
                        string pr = AssetDatabase.GetAssetPath(rd);
                        string pr2 = AssetDatabase.GenerateUniqueAssetPath(PastaAssetsMobile + "/" + NomeRendererCopia + (i > 0 ? "_" + i : "") + ".asset");
                        if (!AssetDatabase.CopyAsset(pr, pr2)) throw new Exception("CopyAsset falhou: " + pr);
                        man.criados.Add(pr2);
                        el.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(pr2);
                        log.AppendLine("(b) Renderer copiado: " + pr2 + " (de " + pr + ")");
                    }
                    soC.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var so = new SerializedObject(copia);
            int n = 0;
            foreach (var c in CamposUrp())
            {
                var p = so.FindProperty(c.campo);
                if (p == null) { log.AppendLine("  aviso: campo " + c.campo + " nao existe"); continue; }
                string antes = ValorProp(p);
                c.aplicar(p);
                if (ValorProp(p) != antes) n++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(copia);
            log.AppendLine("(a) " + n + " campos ajustados no URP asset mobile.");

            if (cfg.secB)
            {
                var lista2 = new SerializedObject(copia).FindProperty("m_RendererDataList");
                for (int i = 0; lista2 != null && i < lista2.arraySize; i++)
                {
                    var rd = lista2.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                    if (rd == null) continue;
                    if (!EhDaTool(AssetDatabase.GetAssetPath(rd))) { log.AppendLine("(b) renderer " + rd.name + " nao e copia da tool; nao mexi."); continue; }
                    var sr = new SerializedObject(rd);
                    if (cfg.forwardSimples) { var m = sr.FindProperty("m_RenderingMode"); if (m != null && m.intValue != 0) { m.intValue = 0; log.AppendLine("(b) " + rd.name + ": Forward"); } }
                    if (cfg.desligarSombraTransparente) { var t = sr.FindProperty("m_ShadowTransparentReceive"); if (t != null) t.boolValue = false; }
                    sr.ApplyModifiedPropertiesWithoutUndo();
                    if (cfg.desligarFeaturesPesadas)
                    {
                        foreach (var f in rd.rendererFeatures)
                        {
                            if (f == null || !f.isActive || !FeatureEhPesada(f)) continue;
                            f.SetActive(false);
                            EditorUtility.SetDirty(f);
                            log.AppendLine("(b) feature desligada na copia: " + f.name);
                        }
                    }
                    EditorUtility.SetDirty(rd);
                }
            }
            AssetDatabase.SaveAssets();
            return copia;
        }

        void AplicarQuality(UniversalRenderPipelineAsset copia, Manifesto man, StringBuilder log)
        {
            var qo = QualityObj();
            if (qo == null) { log.AppendLine("(a) QualitySettings nao encontrado."); return; }
            var so = new SerializedObject(qo);
            var niveis = so.FindProperty("m_QualitySettings");
            int idx;
            var nivel = AcharPorNome(niveis, cfg.nivelMobile, out idx);
            if (nivel == null) { log.AppendLine("(a) Nivel nao encontrado no QualitySettings."); return; }
            man.indiceNivelMobile = idx;
            var crp = nivel.FindPropertyRelative("customRenderPipeline");
            var lb = nivel.FindPropertyRelative("lodBias");
            if ((cfg.atribuirNoNivel || cfg.mudarLodBias) && !man.mudouQuality)
            {
                man.mudouQuality = true;
                man.rpNivelAntesGuid = crp != null && crp.objectReferenceValue != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(crp.objectReferenceValue)) : "";
                man.lodBiasAntes = lb != null ? lb.floatValue : 1f;
            }
            if (cfg.atribuirNoNivel && crp != null && crp.objectReferenceValue != copia)
            {
                crp.objectReferenceValue = copia;
                log.AppendLine("(a) Quality '" + cfg.nivelMobile + "' agora usa " + AssetDatabase.GetAssetPath(copia));
            }
            if (cfg.mudarLodBias && lb != null)
            {
                lb.floatValue = cfg.lodBias;
                log.AppendLine("(a) LOD Bias = " + cfg.lodBias.ToString("0.##", Inv));
            }
            if (cfg.webglPadraoMobile)
            {
                var sp = PlataformaPadrao(so, "WebGL");
                if (sp != null)
                {
                    if (sp.intValue != idx)
                    {
                        man.mudouWebglPadrao = true; man.webglPadraoAntes = sp.intValue;
                        sp.intValue = idx;
                        log.AppendLine("(a) Padrao WebGL = " + cfg.nivelMobile);
                    }
                }
                else log.AppendLine("(a) aviso: WebGL nao encontrado em m_PerPlatformDefaultQuality (troque em Project Settings > Quality)");
            }
            if (cfg.excluirOutrosNiveisDoWebGL)
            {
                for (int i = 0; i < niveis.arraySize; i++)
                {
                    var ex = niveis.GetArrayElementAtIndex(i).FindPropertyRelative("excludedTargetPlatforms");
                    if (ex == null) continue;
                    var antes = new List<string>();
                    for (int k = 0; k < ex.arraySize; k++) antes.Add(ex.GetArrayElementAtIndex(k).stringValue);
                    man.exclusoesAntes.Add(i + "|" + string.Join(",", antes.ToArray()));
                    man.mudouExclusoes = true;
                    bool tem = antes.Contains("WebGL");
                    if (i == idx && tem) RemoverString(ex, "WebGL");
                    else if (i != idx && !tem) { ex.InsertArrayElementAtIndex(ex.arraySize); ex.GetArrayElementAtIndex(ex.arraySize - 1).stringValue = "WebGL"; }
                }
                log.AppendLine("(a) Outros niveis excluidos do build WebGL");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            if (cfg.graphicsPadraoMobile && GraphicsSettings.defaultRenderPipeline != copia)
            {
                man.mudouGraphics = true;
                var gd = GraphicsSettings.defaultRenderPipeline;
                man.graphicsRpAntesGuid = gd != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(gd)) : "";
                GraphicsSettings.defaultRenderPipeline = copia;
                log.AppendLine("(a) Graphics Default Render Pipeline = copia mobile");
            }
        }

        static void RemoverString(SerializedProperty arr, string valor)
        {
            for (int k = arr.arraySize - 1; k >= 0; k--)
                if (arr.GetArrayElementAtIndex(k).stringValue == valor) arr.DeleteArrayElementAtIndex(k);
        }

        // ---------- (c)
        void AplicarC(Manifesto man, StringBuilder log)
        {
            var cfgR = AssetDatabase.LoadAssetAtPath<MobileRenderRuntimeConfig>(CaminhoConfigRuntime);
            if (cfgR == null)
            {
                CriarPasta("Assets/Resources");
                cfgR = CreateInstance<MobileRenderRuntimeConfig>();
                AssetDatabase.CreateAsset(cfgR, CaminhoConfigRuntime);
                man.criados.Add(CaminhoConfigRuntime);
                man.copiados.Remove(CaminhoConfigRuntime);
            }
            cfgR.ativo = true;
            cfgR.nivelMobile = cfg.nivelMobile;
            cfgR.somenteEmCelular = cfg.somenteEmCelular;
            cfgR.trocarNivelNoCelular = cfg.trocarNivelNoCelular;
            cfgR.desligarDepthOfField = cfg.desligarDOF;
            cfgR.desligarMotionBlur = cfg.desligarMotionBlur;
            cfgR.desligarChromaticAberration = cfg.desligarChromatic;
            cfgR.desligarFilmGrain = cfg.desligarFilmGrain;
            cfgR.desligarLensDistortion = cfg.desligarLensDistortion;
            cfgR.desligarPaniniProjection = cfg.desligarPanini;
            cfgR.desligarVignette = cfg.desligarVignette;
            cfgR.bloom = cfg.bloom;
            cfgR.antiAliasingCamera = cfg.aaCamera;
            cfgR.desligarPosNaCamera = cfg.desligarPosNaCamera;
            EditorUtility.SetDirty(cfgR);
            AssetDatabase.SaveAssets();
            log.AppendLine("(c) " + CaminhoConfigRuntime + " salvo (pos-processamento mobile em runtime)");
        }

        // ---------- (d)
        void AplicarD(List<CenaInfo> cenasMudar, StringBuilder log)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int i = 0; i < cenasMudar.Count; i++)
                {
                    var ci = cenasMudar[i];
                    EditorUtility.DisplayProgressBar("Render mobile - cenas", ci.path, (float)i / cenasMudar.Count);
                    var cena = EditorSceneManager.OpenScene(ci.path, OpenSceneMode.Single);
                    int n = AplicarNaCena(cena, log);
                    if (n > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(cena);
                        EditorSceneManager.SaveScene(cena);
                    }
                    log.AppendLine("(d) " + Path.GetFileNameWithoutExtension(ci.path) + ": " + n + " mudanca(s)");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (setup != null && setup.Length > 0)
                {
                    try { EditorSceneManager.RestoreSceneManagerSetup(setup); }
                    catch (Exception e) { Debug.LogWarning("[MobileRender] Nao consegui reabrir as cenas: " + e.Message); }
                }
            }
        }

        int AplicarNaCena(Scene cena, StringBuilder log)
        {
            int n = 0;
            var luzes = new List<Light>();
            var probes = new List<ReflectionProbe>();
            foreach (var r in cena.GetRootGameObjects())
            {
                luzes.AddRange(r.GetComponentsInChildren<Light>(true));
                probes.AddRange(r.GetComponentsInChildren<ReflectionProbe>(true));
            }
            var realtime = luzes.Where(l => l.lightmapBakeType != LightmapBakeType.Baked && l.enabled).ToList();
            Light principal = RenderSettings.sun != null && realtime.Contains(RenderSettings.sun) ? RenderSettings.sun
                : realtime.Where(l => l.type == LightType.Directional).OrderByDescending(l => l.intensity).FirstOrDefault();

            foreach (var l in realtime)
            {
                if (cfg.sombraSoNaLuzPrincipal && l != principal && l.shadows != LightShadows.None)
                { Undo.RecordObject(l, "Render mobile"); l.shadows = LightShadows.None; EditorUtility.SetDirty(l); n++; log.AppendLine("   sem sombra: " + l.name); }
                else if (cfg.sombraResolucaoBaixa && l.shadows != LightShadows.None && l.shadowResolution != LightShadowResolution.FromQualitySettings && l.shadowResolution != LightShadowResolution.Low)
                { Undo.RecordObject(l, "Render mobile"); l.shadowResolution = LightShadowResolution.Low; EditorUtility.SetDirty(l); n++; log.AppendLine("   sombra Low: " + l.name); }
            }
            if (cfg.limitarLuzes)
            {
                var extras = realtime.Where(l => l != principal && (l.type == LightType.Point || l.type == LightType.Spot))
                    .OrderByDescending(l => l.intensity * l.range).ToList();
                for (int k = cfg.maxLuzesExtras; k < extras.Count; k++)
                { Undo.RecordObject(extras[k], "Render mobile"); extras[k].enabled = false; EditorUtility.SetDirty(extras[k]); n++; log.AppendLine("   luz desligada: " + extras[k].name); }
            }
            foreach (var p in probes)
            {
                bool m = false;
                Undo.RecordObject(p, "Render mobile");
                if (cfg.probesParaBaked && p.mode == ReflectionProbeMode.Realtime) { p.mode = ReflectionProbeMode.Baked; m = true; }
                if (p.resolution > cfg.probeResMax) { p.resolution = cfg.probeResMax; m = true; }
                if (m) { EditorUtility.SetDirty(p); n++; log.AppendLine("   probe: " + p.name); }
            }
            if (cfg.realtimeGIOff)
            {
                LightingSettings ls;
                if (Lightmapping.TryGetLightingSettings(out ls) && ls != null && ls.realtimeGI)
                {
                    ls.realtimeGI = false; // o asset foi copiado para o backup antes (ver Aplicar)
                    EditorUtility.SetDirty(ls);
                    n++;
                    log.AppendLine("   Realtime GI desligado (" + AssetDatabase.GetAssetPath(ls) + ")");
                }
            }
            return n;
        }

        // ================================================================= RESTAURAR
        void Restaurar()
        {
            Directory.CreateDirectory(PastaBackups);
            string pasta = EditorUtility.OpenFolderPanel("Escolha a pasta Render_<data> do backup", PastaBackups, "");
            if (string.IsNullOrEmpty(pasta)) return;
            var man = CarregarManifesto(pasta);
            if (man == null)
            { EditorUtility.DisplayDialog("Restaurar backup", "Essa pasta nao tem manifesto.json. Escolha uma pasta Render_<data>.", "OK"); return; }
            if (!EditorUtility.DisplayDialog("Restaurar backup",
                string.Format("Restaurar o backup de {0}?\n\n- Quality/Graphics voltam como estavam\n- {1} arquivo(s) copiados de volta (cenas/assets)\n- {2} arquivo(s) criados pela tool serao apagados",
                    man.data, man.copiados.Count, man.criados.Count), "Restaurar", "Cancelar")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var log = new StringBuilder();
            try
            {
                // 1) Quality (antes de apagar a copia, para nao ficar referencia quebrada)
                var qo = QualityObj();
                if (qo != null && (man.mudouQuality || man.mudouWebglPadrao || man.mudouExclusoes))
                {
                    var so = new SerializedObject(qo);
                    var niveis = so.FindProperty("m_QualitySettings");
                    if (man.mudouQuality && niveis != null && man.indiceNivelMobile >= 0 && man.indiceNivelMobile < niveis.arraySize)
                    {
                        var nivel = niveis.GetArrayElementAtIndex(man.indiceNivelMobile);
                        var crp = nivel.FindPropertyRelative("customRenderPipeline");
                        if (crp != null)
                            crp.objectReferenceValue = string.IsNullOrEmpty(man.rpNivelAntesGuid) ? null
                                : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(man.rpNivelAntesGuid));
                        var lb = nivel.FindPropertyRelative("lodBias");
                        if (lb != null) lb.floatValue = man.lodBiasAntes;
                        log.AppendLine("Quality '" + man.nivelMobile + "' voltou ao URP asset e LOD Bias anteriores.");
                    }
                    if (man.mudouWebglPadrao)
                    {
                        var sp = PlataformaPadrao(so, "WebGL");
                        if (sp != null) { sp.intValue = man.webglPadraoAntes; log.AppendLine("Padrao WebGL voltou para o nivel " + man.webglPadraoAntes); }
                    }
                    if (man.mudouExclusoes && niveis != null)
                    {
                        foreach (var e in man.exclusoesAntes)
                        {
                            var partes = e.Split('|');
                            int i; if (!int.TryParse(partes[0], out i) || i >= niveis.arraySize) continue;
                            var ex = niveis.GetArrayElementAtIndex(i).FindPropertyRelative("excludedTargetPlatforms");
                            if (ex == null) continue;
                            ex.ClearArray();
                            foreach (var s in (partes.Length > 1 ? partes[1] : "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            { ex.InsertArrayElementAtIndex(ex.arraySize); ex.GetArrayElementAtIndex(ex.arraySize - 1).stringValue = s; }
                        }
                        log.AppendLine("Exclusoes de plataforma dos niveis restauradas.");
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                }
                // 2) Graphics
                if (man.mudouGraphics)
                {
                    GraphicsSettings.defaultRenderPipeline = string.IsNullOrEmpty(man.graphicsRpAntesGuid) ? null
                        : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(man.graphicsRpAntesGuid));
                    log.AppendLine("Graphics Default Render Pipeline restaurado.");
                }
                // 3) arquivos copiados de volta (so dentro de Assets/)
                var ativa = SceneManager.GetActiveScene().path;
                foreach (var rel in man.copiados)
                {
                    if (!rel.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                    var src = Path.Combine(pasta, rel);
                    var dst = Path.Combine(RaizProjeto, rel);
                    if (!File.Exists(src)) { log.AppendLine("faltando no backup: " + rel); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.Copy(src, dst, true);
                    if (File.Exists(src + ".meta")) File.Copy(src + ".meta", dst + ".meta", true);
                    log.AppendLine("restaurado: " + rel);
                }
                // 4) apaga so o que esta tool criou
                foreach (var rel in man.criados)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(rel) == null) continue;
                    if (AssetDatabase.DeleteAsset(rel)) log.AppendLine("apagado (criado pela tool): " + rel);
                }
                if (AssetDatabase.IsValidFolder(PastaAssetsMobile) && AssetDatabase.FindAssets("", new[] { PastaAssetsMobile }).Length == 0)
                    AssetDatabase.DeleteAsset(PastaAssetsMobile);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                if (!string.IsNullOrEmpty(ativa) && man.copiados.Contains(ativa))
                    EditorSceneManager.OpenScene(ativa, OpenSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogError("[MobileRender] Erro ao restaurar: " + e);
                log.AppendLine("ERRO: " + e.Message + "\nOs arquivos originais continuam em " + pasta);
            }
            Debug.Log("[MobileRender] Restaurar:\n" + log);
            EditorUtility.DisplayDialog("Restaurar backup", log.Length > 0 ? log.ToString() : "Nada para restaurar.", "OK");
            simulou = false;
        }
    }
}
#endif
