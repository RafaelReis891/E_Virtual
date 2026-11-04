using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vegetacao.EditorTools
{
    /// <summary>Gera os assets individuais de Belo Horizonte (arvores unicas + prefabs Rio) e os presets padrao.</summary>
    public static class VegetacaoPresetBH
    {
        public const string Pasta = "Assets/Games/BeloHorizonte/Vegetacao_Individual";
        public const string PastaMeshes = Pasta + "/Meshes";
        public const string PastaPrefabs = Pasta + "/Prefabs";
        public const string PastaRio = Pasta + "/Prefabs/Rio";
        public const string PresetPath = Pasta + "/Preset_BH.asset";
        public const string PresetLowPolyPath = Pasta + "/Preset_BH_ArbustosLowPoly.asset";

        const string FbxDir = "Assets/Games/Models/BeloHorizonte/";
        const string RioFbx = "Assets/Games/Models/Extras/tropical-plants-pack-m02p/MZRa_Pack_M02P.fbx";
        const string CsvPath = "Assets/Editor/Vegetacao/bh_tree_positions.csv";
        public const float LodCullTela = 0.03f;

        [MenuItem("Tools/Vegetacao/BH/Preparar Assets e Presets BH")]
        public static void Preparar()
        {
            if (!EditorUtility.DisplayDialog("Preparar assets BH",
                "Vai: (1) gerar uma arvore unica por classe a partir dos FBX de BH (liga isReadable temporariamente e restaura);\n" +
                "(2) gerar prefabs das plantas do pacote Rio (sem Circle) com GPU Instancing nos materiais (backup dos .mat em _Backup_Deletados);\n" +
                "(3) criar os presets Preset_BH e Preset_BH_ArbustosLowPoly.\n\nNenhuma cena eh alterada.", "Executar", "Cancelar"))
                return;

            var log = new StringBuilder();
            log.AppendLine("== Preparar assets BH ==");
            try
            {
                VegPrefabTools.EnsureFolder(PastaMeshes);
                VegPrefabTools.EnsureFolder(PastaPrefabs);
                VegPrefabTools.EnsureFolder(PastaRio);

                // 1) arvores unicas de BH
                var opt = new ClusterOptions();
                string[,] classes = {
                    { "ARVORES_GRANDES", "BH_Arvore_Grande" },
                    { "ARVORES_MEDIAS", "BH_Arvore_Media" },
                    { "ARBUSTOS", "BH_Arbusto" },
                    { "PALMEIRAS", "BH_Palmeira" } };
                var single = new Dictionary<string, GameObject>();
                log.AppendLine("-- Arvores unicas (mediana de cada classe) --");
                for (int i = 0; i < classes.GetLength(0); i++)
                {
                    EditorUtility.DisplayProgressBar("Vegetacao BH", classes[i, 1], i / 8f);
                    var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDir + "Pampulha_VEG_" + classes[i, 0] + ".fbx");
                    if (fbx == null) { log.AppendLine("  ERRO: FBX nao encontrado: " + classes[i, 0]); continue; }
                    single[classes[i, 0]] = VegPrefabTools.GerarArvoreUnica(fbx, opt, classes[i, 1], PastaMeshes, PastaPrefabs, false, log);
                }

                // 2) prefabs Rio
                EditorUtility.DisplayProgressBar("Vegetacao BH", "Prefabs Rio", 0.6f);
                log.AppendLine("-- Prefabs Rio (GPU Instancing + LOD cull " + (LodCullTela * 100f) + "%) --");
                var rioFbx = AssetDatabase.LoadAssetAtPath<GameObject>(RioFbx);
                var rio = new List<GameObject>();
                if (rioFbx == null) log.AppendLine("  ERRO: FBX Rio nao encontrado: " + RioFbx);
                else rio = VegPrefabTools.GerarDeFilhosFbx(rioFbx, PastaRio, new[] { "Circle" }, true, true, LodCullTela, log);

                // 3) presets
                EditorUtility.DisplayProgressBar("Vegetacao BH", "Presets", 0.9f);
                var palmas = new List<GameObject>();
                var arbRio = new List<GameObject>();
                string[] pequenos = { "Fern_B0512", "Fern_B0522", "Fern_B0532", "Banana_B091", "Banana_B092", "Monstera_B073", "Monstera_B074" };
                foreach (var pf in rio)
                {
                    string n = pf.name;
                    if (n.StartsWith("Rio_Palm_")) palmas.Add(pf);
                    foreach (var s in pequenos) if (n == "Rio_" + s) arbRio.Add(pf);
                }
                log.AppendLine("-- Presets --");
                log.AppendLine("  palmeiras Rio: " + palmas.Count + "; arbustos Rio: " + arbRio.Count);
                CriarPreset(PresetPath, palmas, arbRio, single, false, log);
                CriarPreset(PresetLowPolyPath, palmas, arbRio, single, true, log);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            log.AppendLine("Backup de materiais/importers em: " + VegBackup.Dir);
            Debug.Log(log.ToString());
            EditorUtility.DisplayDialog("Assets BH prontos", "Veja o Console para o relatorio completo.\nProximo: abra a cena e use Tools/Vegetacao/Substituir Arvores BH.", "OK");
        }

        static GameObject Get(Dictionary<string, GameObject> d, string k)
        {
            GameObject g;
            return d.TryGetValue(k, out g) ? g : null;
        }

        static VegClasse Classe(string nome, string objetoCena, string csvTipo, string fbxNome, TextAsset csv)
        {
            return new VegClasse
            {
                nome = nome,
                ativa = true,
                origem = VegOrigem.Csv,
                csv = csv,
                csvTipo = csvTipo,
                objetoCena = objetoCena,
                fbxFonte = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDir + fbxNome + ".fbx"),
                rotacaoAleatoria = true,
                multiplicador = 1f
            };
        }

        static void CriarPreset(string path, List<GameObject> palmas, List<GameObject> arbRio,
            Dictionary<string, GameObject> single, bool arbustoLowPoly, StringBuilder log)
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvPath);
            if (csv == null) log.AppendLine("  AVISO: CSV nao encontrado em " + CsvPath + " (preset ficara sem CSV)");

            var p = AssetDatabase.LoadAssetAtPath<VegetacaoPreset>(path);
            bool novo = p == null;
            if (novo) p = ScriptableObject.CreateInstance<VegetacaoPreset>();
            p.nomeRaiz = "Vegetacao_Individual";
            p.paiNome = "Vegetacao";
            p.seed = 12345;
            p.desativarOriginais = true;
            p.classes = new List<VegClasse>();

            var g = Classe("Arvores_Grandes", "Pampulha_VEG_ARVORES_GRANDES", "ARVORES_GRANDES", "Pampulha_VEG_ARVORES_GRANDES", csv);
            g.prefabs = Lista(Get(single, "ARVORES_GRANDES"));
            g.escala = VegEscala.AlturaECopa; g.escalaMin = 0.3f; g.escalaMax = 3f; g.batchingStatic = true;
            p.classes.Add(g);

            var m = Classe("Arvores_Medias", "Pampulha_VEG_ARVORES_MEDIAS", "ARVORES_MEDIAS", "Pampulha_VEG_ARVORES_MEDIAS", csv);
            m.prefabs = Lista(Get(single, "ARVORES_MEDIAS"));
            m.escala = VegEscala.AlturaECopa; m.escalaMin = 0.3f; m.escalaMax = 3f; m.batchingStatic = true;
            p.classes.Add(m);

            var a = Classe("Arbustos", "Pampulha_VEG_ARBUSTOS", "ARBUSTOS", "Pampulha_VEG_ARBUSTOS", csv);
            if (arbustoLowPoly)
            {
                a.prefabs = Lista(Get(single, "ARBUSTOS"));
                a.escala = VegEscala.AlturaECopa; a.escalaMin = 0.3f; a.escalaMax = 3f; a.batchingStatic = true;
            }
            else
            {
                a.prefabs = new List<GameObject>(arbRio);
                a.escala = VegEscala.Copa; a.escalaMin = 0.3f; a.escalaMax = 2.5f; a.batchingStatic = false;
            }
            p.classes.Add(a);

            var pa = Classe("Palmeiras", "Pampulha_VEG_PALMEIRAS", "PALMEIRAS", "Pampulha_VEG_PALMEIRAS", csv);
            pa.prefabs = new List<GameObject>(palmas);
            pa.escala = VegEscala.Altura; pa.escalaMin = 0.4f; pa.escalaMax = 3.2f; pa.batchingStatic = false;
            p.classes.Add(pa);

            if (novo) AssetDatabase.CreateAsset(p, path);
            else EditorUtility.SetDirty(p);
            log.AppendLine("  preset: " + path + (novo ? " (criado)" : " (atualizado)"));
        }

        static List<GameObject> Lista(GameObject g)
        {
            var l = new List<GameObject>();
            if (g != null) l.Add(g);
            return l;
        }
    }
}
