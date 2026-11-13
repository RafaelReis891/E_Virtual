// MobileTexturasTool.cs
// Menu: Mobile > Tools > Texturas mobile...
// Ferramenta de Editor (somente Editor) para preparar as texturas do E_Virtual
// para uma build WebGL unica que atende PC e celular.
//
// - So mexe no override da plataforma "WebGL" de cada TextureImporter.
//   Nao altera Default/Standalone, sRGB, alpha, wrap, filtro etc.
// - Padrao (recomendado para PC + celular): limita o tamanho maximo (1024) e
//   deixa o formato Automatico. Assim a build DXT usa DXT no PC e a build ASTC
//   usa ASTC no celular. Se forcar ASTC no override, a documentacao do Unity
//   diz que o formato da textura ganha do formato padrao da build — o .data
//   de DXT tambem sairia em ASTC e o PC descompactaria em runtime (mais memoria).
// - Simular: gera relatorio (janela + CSV em <projeto>/MobileReports/).
// - Aplicar: faz backup de cada .meta que vai mudar em
//   <projeto>/_Backup_Deletados/Texturas_<data>/ (mesmo caminho relativo) e aplica.
// - Restaurar backup: copia os .meta de volta e reimporta.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EVirtualMobile.EditorTools
{
    public class MobileTexturasTool : EditorWindow
    {
        const string Plataforma = "WebGL";
        const string PrefsKey = "EVirtual.MobileTexturasTool.Regras.v2";
        const int TamanhoLote = 40;

        // ----------------------------------------------------------------- tipos
        public enum Categoria { UI, NormalMap, Lightmap, SkyboxHDR, Personagem, AmbienteGrande, Default, Ignorada }

        [Serializable]
        public class RegraCategoria
        {
            public Categoria categoria;
            public bool ativo = true;
            public TextureImporterFormat formato = TextureImporterFormat.ASTC_6x6;
            public int tamanhoMax = 1024;
            public bool manterFormato; // so reduz o tamanho, nao troca o formato
        }

        [Serializable]
        public class RegraCaminho
        {
            public string trecho = "";
            public Categoria categoria = Categoria.UI;
        }

        [Serializable]
        class Config
        {
            public bool somenteUsadasNoBuild = true;
            public bool ambienteGrande8x8;
            public int ambienteGrandeAPartirDe = 2048;
            public bool desligarMipmapUI;
            public bool exportarCsvAoSimular = true;
            // true = so limita tamanho e deixa formato Automatico (compativel com build DXT + ASTC).
            public bool formatoAutomaticoDual = true;
            public string ignorar = "_Backup_Deletados\nStadium Creator 2\n/Editor/\n/Editor Default Resources/\n/Gizmos/";
            public List<RegraCategoria> regras = new List<RegraCategoria>();
            public List<RegraCaminho> caminhos = new List<RegraCaminho>();
        }

        class Item
        {
            public string path;
            public string tipo;
            public Categoria cat;
            public int srcW, srcH;
            public bool alpha, hdr, cube, mip, uncompressed;
            public bool ovAtual;
            public int maxAtual;
            public TextureImporterFormat fmtAtual;
            public int maxNovo;
            public TextureImporterFormat fmtNovo;
            public bool muda;
            public string obs = "";
            public double mbPcAgora, mbCelAgora, mbCelDepois;
        }

        // ----------------------------------------------------------------- estado
        [SerializeField] Config cfg;
        List<Item> itens = new List<Item>();
        Vector2 scroll, scrollLista;
        bool mostrarSoQueMudam = true;
        bool foldRegras = true, foldCaminhos = true, foldOpcoes = true;
        string ultimoCsv = "";
        string ultimoBackup = "";

        static readonly TextureImporterFormat[] FormatosOpcoes =
        {
            TextureImporterFormat.ASTC_4x4, TextureImporterFormat.ASTC_5x5, TextureImporterFormat.ASTC_6x6,
            TextureImporterFormat.ASTC_8x8, TextureImporterFormat.ASTC_10x10, TextureImporterFormat.ASTC_HDR_6x6,
            TextureImporterFormat.ETC2_RGBA8, TextureImporterFormat.Automatic
        };
        static readonly string[] FormatosNomes =
        {
            "ASTC 4x4 (alta)", "ASTC 5x5", "ASTC 6x6 (padrao)", "ASTC 8x8 (leve)", "ASTC 10x10",
            "ASTC HDR 6x6", "ETC2 RGBA8", "Automatico"
        };
        static readonly int[] Tamanhos = { 32, 64, 128, 256, 512, 1024, 2048, 4096 };
        static readonly string[] TamanhosNomes = { "32", "64", "128", "256", "512", "1024", "2048", "4096" };

        static string RaizProjeto { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }
        static string PastaBackups { get { return Path.Combine(RaizProjeto, "_Backup_Deletados"); } }
        static string PastaRelatorios { get { return Path.Combine(RaizProjeto, "MobileReports"); } }

        [MenuItem("Mobile/Tools/Texturas mobile...")]
        static void Abrir()
        {
            var w = GetWindow<MobileTexturasTool>("Texturas mobile");
            w.minSize = new Vector2(620, 520);
            w.Show();
        }

        void OnEnable()
        {
            if (cfg == null || cfg.regras == null || cfg.regras.Count == 0) CarregarConfig();
        }

        // ----------------------------------------------------------------- config
        static Config ConfigPadrao()
        {
            var c = new Config();
            c.regras = new List<RegraCategoria>
            {
                new RegraCategoria { categoria = Categoria.UI,             formato = TextureImporterFormat.Automatic, tamanhoMax = 1024 },
                new RegraCategoria { categoria = Categoria.NormalMap,      formato = TextureImporterFormat.Automatic, tamanhoMax = 1024 },
                new RegraCategoria { categoria = Categoria.Lightmap,       formato = TextureImporterFormat.Automatic, tamanhoMax = 1024 },
                new RegraCategoria { categoria = Categoria.SkyboxHDR,      formato = TextureImporterFormat.Automatic, tamanhoMax = 1024, manterFormato = true },
                new RegraCategoria { categoria = Categoria.Personagem,     formato = TextureImporterFormat.Automatic, tamanhoMax = 1024 },
                new RegraCategoria { categoria = Categoria.AmbienteGrande, formato = TextureImporterFormat.Automatic, tamanhoMax = 1024 },
                new RegraCategoria { categoria = Categoria.Default,        formato = TextureImporterFormat.Automatic, tamanhoMax = 1024 },
            };
            c.formatoAutomaticoDual = true;
            c.caminhos = new List<RegraCaminho>
            {
                new RegraCaminho { trecho = "/UI/",       categoria = Categoria.UI },
                new RegraCaminho { trecho = "/HUD/",      categoria = Categoria.UI },
                new RegraCaminho { trecho = "Sprites",    categoria = Categoria.UI },
                new RegraCaminho { trecho = "Icons",      categoria = Categoria.UI },
                new RegraCaminho { trecho = "Character",  categoria = Categoria.Personagem },
                new RegraCaminho { trecho = "Anfitri",    categoria = Categoria.Personagem },
                new RegraCaminho { trecho = "Personagem", categoria = Categoria.Personagem },
            };
            return c;
        }

        void CarregarConfig()
        {
            cfg = null;
            var json = EditorPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { cfg = JsonUtility.FromJson<Config>(json); } catch { cfg = null; }
            }
            if (cfg == null || cfg.regras == null || cfg.regras.Count == 0) cfg = ConfigPadrao();
            // garante todas as categorias
            var padrao = ConfigPadrao();
            foreach (var r in padrao.regras)
                if (!cfg.regras.Any(x => x.categoria == r.categoria)) cfg.regras.Add(r);
            if (cfg.caminhos == null) cfg.caminhos = new List<RegraCaminho>();
        }

        void SalvarConfig()
        {
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(cfg));
        }

        RegraCategoria Regra(Categoria c)
        {
            return cfg.regras.FirstOrDefault(r => r.categoria == c);
        }

        // ----------------------------------------------------------------- GUI
        void OnGUI()
        {
            if (cfg == null) CarregarConfig();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.HelpBox(
                "So altera o override de WebGL das texturas (formato e tamanho maximo). " +
                "Default/Standalone ficam iguais. Nunca aumenta o tamanho: usa o menor entre o atual e o limite. " +
                "Padrao: formato Automatico, para a mesma entrega servir no PC (DXT) e no celular (ASTC). " +
                "O Unity nao gera os dois formatos numa textura so: o fluxo oficial e duas builds (DXT e ASTC) " +
                "e o carregador escolhe o arquivo .data. Sempre rode Simular antes de Aplicar. " +
                "Aplicar faz backup dos .meta em _Backup_Deletados.",
                MessageType.Info);

            DesenharInfoBuild();

            EditorGUI.BeginChangeCheck();

            foldOpcoes = EditorGUILayout.Foldout(foldOpcoes, "Escopo e opcoes", true);
            if (foldOpcoes)
            {
                EditorGUI.indentLevel++;
                int escopo = cfg.somenteUsadasNoBuild ? 0 : 1;
                escopo = EditorGUILayout.Popup("Quais texturas",
                    escopo, new[] { "Somente usadas nas cenas do Build + Resources (recomendado)", "Todas em Assets/" });
                cfg.somenteUsadasNoBuild = escopo == 0;
                cfg.formatoAutomaticoDual = EditorGUILayout.ToggleLeft(
                    "Formato Automatico (recomendado para PC + celular). So limita o tamanho; a build escolhe DXT ou ASTC.",
                    cfg.formatoAutomaticoDual);
                if (!cfg.formatoAutomaticoDual)
                    EditorGUILayout.HelpBox(
                        "Com um formato fixo (ex.: ASTC 6x6) no override WebGL, as duas builds usam esse formato. " +
                        "No PC o navegador em geral nao le ASTC e o Unity descompacta na hora (mais memoria e mais lento).",
                        MessageType.Warning);
                cfg.ambienteGrande8x8 = EditorGUILayout.ToggleLeft(
                    "Texturas de ambiente grandes usam a regra 'AmbienteGrande'", cfg.ambienteGrande8x8);
                if (cfg.ambienteGrande8x8)
                    cfg.ambienteGrandeAPartirDe = EditorGUILayout.IntPopup("  Grande a partir de (px)",
                        cfg.ambienteGrandeAPartirDe, TamanhosNomes, Tamanhos);
                cfg.desligarMipmapUI = EditorGUILayout.ToggleLeft(
                    "Desligar mipmaps em UI/sprites (ATENCAO: mipmap vale para todas as plataformas)", cfg.desligarMipmapUI);
                cfg.exportarCsvAoSimular = EditorGUILayout.ToggleLeft("Exportar CSV ao simular", cfg.exportarCsvAoSimular);
                EditorGUILayout.LabelField("Ignorar caminhos que contem (um por linha):");
                cfg.ignorar = EditorGUILayout.TextArea(cfg.ignorar, GUILayout.MinHeight(70));
                EditorGUI.indentLevel--;
            }

            foldRegras = EditorGUILayout.Foldout(foldRegras, "Regras por tipo (WebGL)", true);
            if (foldRegras)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Tipo", EditorStyles.boldLabel, GUILayout.Width(120));
                GUILayout.Label("Ativo", EditorStyles.boldLabel, GUILayout.Width(40));
                GUILayout.Label("Formato", EditorStyles.boldLabel, GUILayout.Width(150));
                GUILayout.Label("Max", EditorStyles.boldLabel, GUILayout.Width(70));
                GUILayout.Label("So reduzir tamanho", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
                foreach (var r in cfg.regras)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(r.categoria.ToString(), GUILayout.Width(120));
                    r.ativo = EditorGUILayout.Toggle(r.ativo, GUILayout.Width(40));
                    int idx = Array.IndexOf(FormatosOpcoes, r.formato);
                    if (idx < 0) idx = Array.IndexOf(FormatosOpcoes, TextureImporterFormat.Automatic);
                    using (new EditorGUI.DisabledScope(r.manterFormato || cfg.formatoAutomaticoDual))
                        idx = EditorGUILayout.Popup(idx, FormatosNomes, GUILayout.Width(150));
                    r.formato = FormatosOpcoes[idx];
                    r.tamanhoMax = EditorGUILayout.IntPopup(r.tamanhoMax, TamanhosNomes, Tamanhos, GUILayout.Width(70));
                    r.manterFormato = EditorGUILayout.Toggle(r.manterFormato, GUILayout.Width(20));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.HelpBox(
                    "UI = Sprite/GUI/Cursor ou pasta de UI. SkyboxHDR = .hdr/.exr ou Cubemap (padrao: so reduz o tamanho, " +
                    "para nao trocar um formato HDR). Lightmap = Lightmap/Shadowmask/Directional. " +
                    "Crunch fica desligado. Qualidade de compressao: Normal. " +
                    "Com formato Automatico, o bloco ASTC (4x4/6x6/8x8) segue a qualidade Default da textura na build ASTC; " +
                    "nao da para forcar 4x4 so na UI sem quebrar a build DXT do PC.",
                    MessageType.None);
            }

            foldCaminhos = EditorGUILayout.Foldout(foldCaminhos, "Regras por caminho (vale para texturas Default)", true);
            if (foldCaminhos)
            {
                EditorGUILayout.LabelField("Se o caminho contem o trecho, usa a categoria. '/UI/' = nome exato de pasta.", EditorStyles.miniLabel);
                int remover = -1;
                for (int i = 0; i < cfg.caminhos.Count; i++)
                {
                    var c = cfg.caminhos[i];
                    EditorGUILayout.BeginHorizontal();
                    c.trecho = EditorGUILayout.TextField(c.trecho);
                    c.categoria = (Categoria)EditorGUILayout.EnumPopup(c.categoria, GUILayout.Width(130));
                    if (GUILayout.Button("x", GUILayout.Width(22))) remover = i;
                    EditorGUILayout.EndHorizontal();
                }
                if (remover >= 0) cfg.caminhos.RemoveAt(remover);
                if (GUILayout.Button("+ Regra de caminho", GUILayout.Width(160))) cfg.caminhos.Add(new RegraCaminho());
            }

            if (EditorGUI.EndChangeCheck()) SalvarConfig();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Simular", GUILayout.Height(30))) Simular();
            using (new EditorGUI.DisabledScope(itens.Count == 0 || !itens.Any(i => i.muda)))
                if (GUILayout.Button("Aplicar", GUILayout.Height(30))) Aplicar();
            if (GUILayout.Button("Restaurar backup", GUILayout.Height(30))) Restaurar();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Regras padrao")) { if (EditorUtility.DisplayDialog("Regras padrao", "Voltar todas as regras para o padrao?", "Sim", "Nao")) { cfg = ConfigPadrao(); SalvarConfig(); } }
            if (GUILayout.Button("Abrir pasta de relatorios")) { Directory.CreateDirectory(PastaRelatorios); EditorUtility.RevealInFinder(PastaRelatorios); }
            if (GUILayout.Button("Abrir pasta de backups")) { Directory.CreateDirectory(PastaBackups); EditorUtility.RevealInFinder(PastaBackups); }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(ultimoCsv)) EditorGUILayout.LabelField("CSV: " + ultimoCsv, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(ultimoBackup)) EditorGUILayout.LabelField("Backup: " + ultimoBackup, EditorStyles.miniLabel);

            DesenharResultado();
            EditorGUILayout.EndScrollView();
        }

        void DesenharInfoBuild()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Plataforma ativa: " + EditorUserBuildSettings.activeBuildTarget +
                "   |   WebGL Texture Subtarget (Library): " + EditorUserBuildSettings.webGLBuildSubtarget, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "O subtarget da Library vale para uma build local. Para PC + celular o fluxo do Unity 6 e: " +
                "uma build com subtarget DXT e outra com ASTC, depois juntar os dois .data. " +
                "O formato padrao atual do projeto (Player Settings) nao e alterado por esta ferramenta.",
                MessageType.None);
        }

        void DesenharResultado()
        {
            if (itens.Count == 0) return;
            EditorGUILayout.Space();
            var mudam = itens.Where(i => i.muda).ToList();
            EditorGUILayout.LabelField("Resultado da simulacao", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format("Texturas analisadas: {0}   |   Vao mudar: {1}   |   Ja OK / ignoradas: {2}",
                itens.Count, mudam.Count, itens.Count - mudam.Count));
            EditorGUILayout.LabelField(string.Format(
                "Memoria GPU estimada:  PC hoje {0:0} MB   |   Celular hoje {1:0} MB   |   Celular depois {2:0} MB",
                itens.Sum(i => i.mbPcAgora), itens.Sum(i => i.mbCelAgora), itens.Sum(i => i.mbCelDepois)));
            foreach (var g in itens.GroupBy(i => i.cat).OrderBy(g => g.Key))
                EditorGUILayout.LabelField(string.Format("   {0}: {1} texturas, {2} mudam, celular {3:0} -> {4:0} MB",
                    g.Key, g.Count(), g.Count(i => i.muda), g.Sum(i => i.mbCelAgora), g.Sum(i => i.mbCelDepois)), EditorStyles.miniLabel);

            mostrarSoQueMudam = EditorGUILayout.ToggleLeft("Mostrar so as que mudam (top 300 por memoria no celular)", mostrarSoQueMudam);
            var lista = (mostrarSoQueMudam ? mudam : itens).OrderByDescending(i => i.mbCelAgora).Take(300).ToList();
            scrollLista = EditorGUILayout.BeginScrollView(scrollLista, GUILayout.MinHeight(220));
            foreach (var it in lista)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("o", GUILayout.Width(20)))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(it.path));
                GUILayout.Label(string.Format("{0,-10} {1}x{2}  {3} {4} -> {5} {6}  ({7:0.0} -> {8:0.0} MB)  {9}",
                    it.cat, it.srcW, it.srcH, it.maxAtual, NomeFmt(it.fmtAtual, it.ovAtual), it.maxNovo, NomeFmt(it.fmtNovo, true),
                    it.mbCelAgora, it.mbCelDepois, it.path), EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        // ----------------------------------------------------------------- scan
        bool Ignorado(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal)) return true;
            string p = "/" + path + "/";
            foreach (var linha in (cfg.ignorar ?? "").Split('\n'))
            {
                var t = linha.Trim();
                if (t.Length == 0) continue;
                if (p.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        HashSet<string> CaminhosUsadosNoBuild()
        {
            var raizes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(Path.Combine(RaizProjeto, s.path))) raizes.Add(s.path);

            var pastasResources = new List<string>();
            foreach (var dir in Directory.GetDirectories(Application.dataPath, "Resources", SearchOption.AllDirectories))
            {
                var rel = "Assets" + dir.Substring(Application.dataPath.Length).Replace('\\', '/');
                if (!Ignorado(rel + "/x")) pastasResources.Add(rel);
            }
            if (pastasResources.Count > 0)
                foreach (var guid in AssetDatabase.FindAssets("", pastasResources.ToArray()))
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (!AssetDatabase.IsValidFolder(p)) raizes.Add(p);
                }

            EditorUtility.DisplayProgressBar("Texturas mobile", "Lendo dependencias de " + raizes.Count + " cenas/Resources...", 0.1f);
            return new HashSet<string>(AssetDatabase.GetDependencies(raizes.Distinct().ToArray(), true));
        }

        Categoria Classificar(TextureImporter imp, string path, bool hdr, int srcW, int srcH)
        {
            switch (imp.textureType)
            {
                case TextureImporterType.Lightmap:
                case TextureImporterType.DirectionalLightmap:
                case TextureImporterType.Shadowmask:
                    return Categoria.Lightmap;
                case TextureImporterType.Sprite:
                case TextureImporterType.GUI:
                case TextureImporterType.Cursor:
                    return Categoria.UI;
                case TextureImporterType.NormalMap:
                    return Categoria.NormalMap;
                case TextureImporterType.SingleChannel:
                    return Categoria.Ignorada;
            }
            if (hdr || imp.textureShape == TextureImporterShape.TextureCube) return Categoria.SkyboxHDR;
            if (imp.textureShape != TextureImporterShape.Texture2D) return Categoria.Ignorada;

            string p = "/" + path + "/";
            foreach (var c in cfg.caminhos)
            {
                if (string.IsNullOrEmpty(c.trecho)) continue;
                if (p.IndexOf(c.trecho, StringComparison.OrdinalIgnoreCase) >= 0) return c.categoria;
            }
            if (cfg.ambienteGrande8x8 && Mathf.Max(srcW, srcH) >= cfg.ambienteGrandeAPartirDe) return Categoria.AmbienteGrande;
            return Categoria.Default;
        }

        void Simular()
        {
            itens.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("Texturas mobile", "Procurando texturas...", 0f);
                IEnumerable<string> candidatos;
                if (cfg.somenteUsadasNoBuild) candidatos = CaminhosUsadosNoBuild();
                else candidatos = AssetDatabase.FindAssets("t:Texture", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath);
                var lista = candidatos.Where(p => !Ignorado(p)).Distinct().OrderBy(p => p).ToList();

                for (int i = 0; i < lista.Count; i++)
                {
                    var path = lista[i];
                    if (i % 20 == 0 && EditorUtility.DisplayCancelableProgressBar("Texturas mobile - Simular", path, (float)i / Mathf.Max(1, lista.Count)))
                    {
                        Debug.LogWarning("[MobileTexturas] Simulacao cancelada.");
                        break;
                    }
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) continue;
                    itens.Add(Analisar(imp, path));
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            Debug.Log(string.Format("[MobileTexturas] Simulacao: {0} texturas, {1} mudam. Celular hoje ~{2:0} MB -> depois ~{3:0} MB.",
                itens.Count, itens.Count(i => i.muda), itens.Sum(i => i.mbCelAgora), itens.Sum(i => i.mbCelDepois)));
            if (cfg.exportarCsvAoSimular) ExportarCsv();
            Repaint();
        }

        Item Analisar(TextureImporter imp, string path)
        {
            var it = new Item { path = path };
            int w, h;
            imp.GetSourceTextureWidthAndHeight(out w, out h);
            it.srcW = w; it.srcH = h;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            it.hdr = ext == ".hdr" || ext == ".exr";
            it.cube = imp.textureShape == TextureImporterShape.TextureCube;
            it.mip = imp.mipmapEnabled;
            it.alpha = imp.DoesSourceTextureHaveAlpha() && imp.alphaSource != TextureImporterAlphaSource.None;
            it.tipo = imp.textureType + (it.cube ? "/Cube" : "");

            var def = imp.GetDefaultPlatformTextureSettings();
            var ws = imp.GetPlatformTextureSettings(Plataforma);
            it.ovAtual = ws.overridden;
            it.maxAtual = ws.overridden ? ws.maxTextureSize : def.maxTextureSize;
            it.fmtAtual = ws.overridden ? ws.format : TextureImporterFormat.Automatic;
            var compAtual = ws.overridden ? ws.textureCompression : def.textureCompression;
            it.uncompressed = compAtual == TextureImporterCompression.Uncompressed && it.fmtAtual == TextureImporterFormat.Automatic;

            it.cat = Classificar(imp, path, it.hdr, w, h);
            var regra = Regra(it.cat);

            it.maxNovo = it.maxAtual;
            it.fmtNovo = it.fmtAtual;
            if (it.cat == Categoria.Ignorada || regra == null)
            {
                it.obs = "ignorada (tipo)";
            }
            else if (!regra.ativo)
            {
                it.obs = "regra desligada";
            }
            else
            {
                it.maxNovo = Mathf.Min(it.maxAtual, regra.tamanhoMax); // nunca aumenta
                if (regra.manterFormato) it.fmtNovo = it.fmtAtual;
                else if (cfg.formatoAutomaticoDual) it.fmtNovo = TextureImporterFormat.Automatic;
                else it.fmtNovo = regra.formato;
                bool ui = it.cat == Categoria.UI;
                bool mudaMip = cfg.desligarMipmapUI && ui && imp.mipmapEnabled;
                it.muda = it.maxNovo != it.maxAtual || it.fmtNovo != it.fmtAtual ||
                          (ws.overridden && ws.crunchedCompression) || mudaMip;
                if (!it.muda) it.obs = "ja ok";
                else if (it.fmtNovo == it.fmtAtual) it.obs = "so reduz tamanho";
            }

            bool mipDepois = it.mip && !(cfg.desligarMipmapUI && it.cat == Categoria.UI && it.muda);
            it.mbPcAgora = MemoriaMB(it, it.maxAtual, BppPc(it, it.fmtAtual), it.mip);
            it.mbCelAgora = MemoriaMB(it, it.maxAtual, BppCelular(it, it.fmtAtual), it.mip);
            it.mbCelDepois = MemoriaMB(it, it.maxNovo, BppCelular(it, it.fmtNovo), mipDepois);
            return it;
        }

        // ----------------------------------------------------------------- memoria (estimativa)
        static double BppFormato(TextureImporterFormat f)
        {
            switch (f)
            {
                case TextureImporterFormat.ASTC_4x4: case TextureImporterFormat.ASTC_HDR_4x4: return 8;
                case TextureImporterFormat.ASTC_5x5: case TextureImporterFormat.ASTC_HDR_5x5: return 5.12;
                case TextureImporterFormat.ASTC_6x6: case TextureImporterFormat.ASTC_HDR_6x6: return 3.56;
                case TextureImporterFormat.ASTC_8x8: case TextureImporterFormat.ASTC_HDR_8x8: return 2;
                case TextureImporterFormat.ASTC_10x10: case TextureImporterFormat.ASTC_HDR_10x10: return 1.28;
                case TextureImporterFormat.ASTC_12x12: case TextureImporterFormat.ASTC_HDR_12x12: return 0.89;
                case TextureImporterFormat.DXT1: case TextureImporterFormat.BC4: case TextureImporterFormat.ETC2_RGB4: return 4;
                case TextureImporterFormat.DXT5: case TextureImporterFormat.BC5: case TextureImporterFormat.BC6H:
                case TextureImporterFormat.BC7: case TextureImporterFormat.ETC2_RGBA8: return 8;
                case TextureImporterFormat.RGBAHalf: return 64;
                case TextureImporterFormat.R8: case TextureImporterFormat.Alpha8: return 8;
                default: return 32;
            }
        }

        static bool CelularLe(TextureImporterFormat f)
        {
            string n = f.ToString();
            return n.StartsWith("ASTC") || n.StartsWith("ETC") || n.StartsWith("RGB") || n.StartsWith("ARGB") ||
                   n == "R8" || n == "Alpha8" || n == "R16" || n == "RGBAHalf";
        }

        // Formato que o WebGL desktop usa quando nao ha override (subtarget DXT do projeto).
        static TextureImporterFormat AutomaticoPc(Item it)
        {
            if (it.uncompressed) return it.hdr ? TextureImporterFormat.RGBAHalf : TextureImporterFormat.RGBA32;
            if (it.hdr) return TextureImporterFormat.BC6H;
            if (it.cat == Categoria.NormalMap) return TextureImporterFormat.DXT5;
            return it.alpha ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1;
        }

        static double BppPc(Item it, TextureImporterFormat f)
        {
            if (f == TextureImporterFormat.Automatic) f = AutomaticoPc(it);
            return BppFormato(f);
        }

        // Na build ASTC com formato Automatico, o Unity escolhe ASTC (qualidade Normal ~ 6x6).
        static TextureImporterFormat AutomaticoCelular(Item it)
        {
            if (it.uncompressed) return it.hdr ? TextureImporterFormat.RGBAHalf : TextureImporterFormat.RGBA32;
            if (it.hdr) return TextureImporterFormat.ASTC_HDR_6x6;
            return TextureImporterFormat.ASTC_6x6;
        }

        static double BppCelular(Item it, TextureImporterFormat f)
        {
            if (f == TextureImporterFormat.Automatic) f = AutomaticoCelular(it);
            if (CelularLe(f)) return BppFormato(f);
            return it.hdr ? 64 : 32; // celular nao le DXT/BC: Unity descompacta
        }

        static double MemoriaMB(Item it, int maxSize, double bpp, bool mip)
        {
            double w = Mathf.Max(1, it.srcW), h = Mathf.Max(1, it.srcH);
            double maior = Math.Max(w, h);
            if (maior > maxSize) { double f = maxSize / maior; w *= f; h *= f; }
            double pixels = w * h;
            if (it.cube) { double face = it.srcW >= it.srcH * 2 ? w / 4.0 : Math.Min(w, h); pixels = 6 * face * face; }
            double bytes = pixels * bpp / 8.0 * (mip ? 4.0 / 3.0 : 1.0);
            return bytes / (1024.0 * 1024.0);
        }

        static string NomeFmt(TextureImporterFormat f, bool ov)
        {
            if (!ov) return "(sem override)";
            return f == TextureImporterFormat.Automatic ? "Automatico" : f.ToString();
        }

        // ----------------------------------------------------------------- CSV
        void ExportarCsv()
        {
            Directory.CreateDirectory(PastaRelatorios);
            var arq = Path.Combine(PastaRelatorios, "texturas_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
            var ci = new CultureInfo("pt-BR");
            var sb = new StringBuilder();
            sb.AppendLine("Caminho;Tipo;Categoria;Largura;Altura;Alpha;Mipmap;OverrideWebGL;MaxAtual;FormatoAtual;MaxNovo;FormatoNovo;Muda;MB_PC_hoje;MB_Celular_hoje;MB_Celular_depois;Obs");
            foreach (var it in itens.OrderByDescending(i => i.mbCelAgora))
            {
                sb.AppendLine(string.Join(";", new[]
                {
                    it.path, it.tipo, it.cat.ToString(), it.srcW.ToString(), it.srcH.ToString(),
                    it.alpha ? "sim" : "nao", it.mip ? "sim" : "nao", it.ovAtual ? "sim" : "nao",
                    it.maxAtual.ToString(), NomeFmt(it.fmtAtual, it.ovAtual), it.maxNovo.ToString(), NomeFmt(it.fmtNovo, true),
                    it.muda ? "sim" : "nao",
                    it.mbPcAgora.ToString("0.00", ci), it.mbCelAgora.ToString("0.00", ci), it.mbCelDepois.ToString("0.00", ci), it.obs
                }));
            }
            sb.AppendLine(string.Format(ci, "TOTAL;;;;;;;;;;;;{0};{1:0.0};{2:0.0};{3:0.0};",
                itens.Count(i => i.muda), itens.Sum(i => i.mbPcAgora), itens.Sum(i => i.mbCelAgora), itens.Sum(i => i.mbCelDepois)));
            File.WriteAllText(arq, sb.ToString(), new UTF8Encoding(true));
            ultimoCsv = arq;
            Debug.Log("[MobileTexturas] CSV salvo em " + arq);
        }

        // ----------------------------------------------------------------- aplicar
        void Aplicar()
        {
            var mudam = itens.Where(i => i.muda).ToList();
            if (mudam.Count == 0) return;
            string avisoFmt = cfg.formatoAutomaticoDual
                ? "O formato ficara Automatico (a build DXT usa DXT, a build ASTC usa ASTC)."
                : "ATENCAO: formato fixo no override. Se for ASTC, a build de PC tambem leva ASTC e o computador descompacta.";
            if (!EditorUtility.DisplayDialog("Aplicar texturas mobile",
                string.Format("Vai alterar o override WebGL de {0} texturas.\n\n{1}\n\nAntes, os .meta vao para:\n_Backup_Deletados/Texturas_<data>/\n\n" +
                "Isso reimporta as texturas e pode demorar. Continuar?", mudam.Count, avisoFmt), "Aplicar", "Cancelar"))
                return;

            // 1) Backup de TODOS os .meta antes de qualquer mudanca
            string pastaBackup = Path.Combine(PastaBackups, "Texturas_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            if (pastaBackup.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Texturas mobile", "A pasta de backup ficaria dentro de Assets/. Cancelado.", "OK");
                return;
            }
            var lista = new StringBuilder();
            try
            {
                for (int i = 0; i < mudam.Count; i++)
                {
                    var it = mudam[i];
                    if (i % 25 == 0) EditorUtility.DisplayProgressBar("Texturas mobile - Backup", it.path, (float)i / mudam.Count);
                    string origem = Path.Combine(RaizProjeto, it.path + ".meta");
                    string destino = Path.Combine(pastaBackup, it.path + ".meta");
                    Directory.CreateDirectory(Path.GetDirectoryName(destino));
                    File.Copy(origem, destino, true);
                    lista.AppendLine(it.path);
                }
                File.WriteAllText(Path.Combine(pastaBackup, "LEIA-ME.txt"),
                    "Backup dos .meta antes da ferramenta Mobile > Tools > Texturas mobile.\n" +
                    "Para voltar: abra a ferramenta e clique em 'Restaurar backup', escolhendo esta pasta.\n\n" + lista, new UTF8Encoding(true));
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("[MobileTexturas] Falha no backup, nada foi alterado: " + e);
                EditorUtility.DisplayDialog("Texturas mobile", "Falha no backup. Nada foi alterado.\n" + e.Message, "OK");
                return;
            }
            ultimoBackup = pastaBackup;
            Debug.Log("[MobileTexturas] Backup de " + mudam.Count + " .meta em " + pastaBackup);

            // 2) Aplica em lotes
            int feitos = 0;
            bool cancelou = false;
            try
            {
                for (int inicio = 0; inicio < mudam.Count && !cancelou; inicio += TamanhoLote)
                {
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        for (int i = inicio; i < Mathf.Min(inicio + TamanhoLote, mudam.Count); i++)
                        {
                            var it = mudam[i];
                            if (EditorUtility.DisplayCancelableProgressBar("Texturas mobile - Aplicando",
                                string.Format("{0}/{1}  {2}", i + 1, mudam.Count, it.path), (float)i / mudam.Count))
                            { cancelou = true; break; }
                            if (AplicarItem(it)) feitos++;
                        }
                    }
                    finally { AssetDatabase.StopAssetEditing(); } // reimporta o lote
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.SaveAssets();
            string msg = string.Format("{0} de {1} texturas alteradas{2}.\nBackup: {3}", feitos, mudam.Count,
                cancelou ? " (cancelado no meio; o backup cobre todas)" : "", pastaBackup);
            Debug.Log("[MobileTexturas] " + msg.Replace("\n", " "));
            EditorUtility.DisplayDialog("Texturas mobile", msg, "OK");
            Simular();
        }

        bool AplicarItem(Item it)
        {
            var imp = AssetImporter.GetAtPath(it.path) as TextureImporter;
            if (imp == null) return false;
            var def = imp.GetDefaultPlatformTextureSettings();
            var ws = imp.GetPlatformTextureSettings(Plataforma);
            bool tinhaOverride = ws.overridden;
            ws.name = Plataforma;
            ws.overridden = true;
            ws.maxTextureSize = it.maxNovo;
            ws.format = it.fmtNovo;
            ws.crunchedCompression = false;
            ws.compressionQuality = 50; // Normal
            if (!tinhaOverride)
            {
                ws.resizeAlgorithm = def.resizeAlgorithm;
                ws.textureCompression = def.textureCompression == TextureImporterCompression.Uncompressed
                    ? TextureImporterCompression.Compressed : def.textureCompression;
            }
            else if (ws.textureCompression == TextureImporterCompression.Uncompressed)
                ws.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformTextureSettings(ws);
            if (cfg.desligarMipmapUI && it.cat == Categoria.UI && imp.mipmapEnabled) imp.mipmapEnabled = false;
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();
            return true;
        }

        // ----------------------------------------------------------------- restaurar
        void Restaurar()
        {
            Directory.CreateDirectory(PastaBackups);
            string pasta = EditorUtility.OpenFolderPanel("Escolha a pasta Texturas_<data> do backup", PastaBackups, "");
            if (string.IsNullOrEmpty(pasta)) return;
            pasta = Path.GetFullPath(pasta);
            if (!Directory.Exists(Path.Combine(pasta, "Assets")))
            {
                EditorUtility.DisplayDialog("Restaurar backup", "Essa pasta nao tem a subpasta 'Assets'. Escolha uma pasta Texturas_<data>.", "OK");
                return;
            }
            var metas = Directory.GetFiles(Path.Combine(pasta, "Assets"), "*.meta", SearchOption.AllDirectories);
            if (!EditorUtility.DisplayDialog("Restaurar backup",
                string.Format("Copiar {0} arquivos .meta de volta para o projeto e reimportar?\n\n{1}", metas.Length, pasta), "Restaurar", "Cancelar"))
                return;

            var caminhos = new List<string>();
            int pulados = 0;
            try
            {
                for (int i = 0; i < metas.Length; i++)
                {
                    string rel = metas[i].Substring(pasta.Length).TrimStart('\\', '/').Replace('\\', '/');
                    string assetRel = rel.Substring(0, rel.Length - ".meta".Length);
                    string destinoMeta = Path.Combine(RaizProjeto, rel);
                    if (i % 25 == 0) EditorUtility.DisplayProgressBar("Texturas mobile - Restaurando", assetRel, (float)i / Mathf.Max(1, metas.Length));
                    if (!File.Exists(Path.Combine(RaizProjeto, assetRel))) { pulados++; continue; }
                    File.Copy(metas[i], destinoMeta, true);
                    caminhos.Add(assetRel);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            try
            {
                for (int inicio = 0; inicio < caminhos.Count; inicio += TamanhoLote)
                {
                    EditorUtility.DisplayProgressBar("Texturas mobile - Reimportando", caminhos[inicio], (float)inicio / Mathf.Max(1, caminhos.Count));
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        for (int i = inicio; i < Mathf.Min(inicio + TamanhoLote, caminhos.Count); i++)
                            AssetDatabase.ImportAsset(caminhos[i], ImportAssetOptions.ForceUpdate);
                    }
                    finally { AssetDatabase.StopAssetEditing(); }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.Refresh();

            string msg = string.Format("{0} .meta restaurados e reimportados. {1} pulados (asset nao existe mais).", caminhos.Count, pulados);
            Debug.Log("[MobileTexturas] " + msg);
            EditorUtility.DisplayDialog("Restaurar backup", msg, "OK");
            itens.Clear();
        }
    }
}
#endif
