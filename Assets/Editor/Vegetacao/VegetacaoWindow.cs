using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vegetacao.EditorTools
{
    public class VegetacaoWindow : EditorWindow
    {
        const string PrefKey = "Vegetacao.PresetGuid";

        VegetacaoPreset _preset;
        Transform _pai;
        Editor _editor;
        Vector2 _scroll;
        string _msg = "";
        bool _ferramentas;
        GameObject _fbxPlantas;
        string _pastaPrefabs = "Assets/Vegetacao_Individual/Prefabs";
        string _excluir = "Circle";
        bool _instancing = true;
        bool _lod = true;
        float _lodTela = 0.03f;
        GameObject _fbxUnica;
        string _pastaMalha = "Assets/Vegetacao_Individual/Meshes";
        string _nomeUnica = "Arvore_Unica";

        [MenuItem("Tools/Vegetacao/Substituir Vegetacao")]
        public static void Abrir()
        {
            var w = GetWindow<VegetacaoWindow>("Substituir Vegetacao");
            w.minSize = new Vector2(430, 420);
            w.Show();
        }

        [MenuItem("Tools/Vegetacao/Substituir Arvores BH")]
        public static void SubstituirBH()
        {
            var p = AssetDatabase.LoadAssetAtPath<VegetacaoPreset>(VegetacaoPresetBH.PresetPath);
            if (p == null)
            {
                EditorUtility.DisplayDialog("Substituir Arvores BH", "Preset nao encontrado em " + VegetacaoPresetBH.PresetPath +
                    ".\nRode antes: Tools/Vegetacao/BH/Preparar Assets e Presets BH", "OK");
                return;
            }
            SalvarPreset(p);
            Executar(p, null);
        }

        [MenuItem("Tools/Vegetacao/Desfazer Substituicao")]
        public static void DesfazerUltimo()
        {
            var p = PresetSalvo();
            if (p == null) p = AssetDatabase.LoadAssetAtPath<VegetacaoPreset>(VegetacaoPresetBH.PresetPath);
            if (p == null) { EditorUtility.DisplayDialog("Desfazer Substituicao", "Nenhum preset conhecido.", "OK"); return; }
            Debug.Log("[Vegetacao] " + VegetacaoRunner.Desfazer(p, null));
        }

        static VegetacaoPreset PresetSalvo()
        {
            string guid = EditorPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VegetacaoPreset>(path);
        }

        static void SalvarPreset(VegetacaoPreset p)
        {
            if (p == null) return;
            string path = AssetDatabase.GetAssetPath(p);
            if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString(PrefKey, AssetDatabase.AssetPathToGUID(path));
        }

        static string Executar(VegetacaoPreset p, Transform pai)
        {
            string r = VegetacaoRunner.Substituir(p, pai);
            Debug.Log("[Vegetacao] " + r);
            return r;
        }

        void OnEnable()
        {
            if (_preset == null) _preset = PresetSalvo();
        }

        void OnDisable()
        {
            if (_editor != null) DestroyImmediate(_editor);
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "1) Escolha ou crie um preset.  2) Para cada classe: origem das posicoes (malha mesclada do FBX/objeto de cena, ou CSV), " +
                "lista de prefabs, modo de escala, rotacao aleatoria.  3) Abra a cena de destino e clique Substituir. " +
                "Ctrl+Z ou Desfazer reverte (os objetos originais sao apenas desativados).", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _preset = (VegetacaoPreset)EditorGUILayout.ObjectField("Preset", _preset, typeof(VegetacaoPreset), false);
            if (EditorGUI.EndChangeCheck()) { SalvarPreset(_preset); RebuildEditor(); }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Novo preset vazio")) NovoPreset();
                if (GUILayout.Button("Carregar preset BH"))
                {
                    var p = AssetDatabase.LoadAssetAtPath<VegetacaoPreset>(VegetacaoPresetBH.PresetPath);
                    if (p == null) _msg = "Preset BH nao existe. Rode Tools/Vegetacao/BH/Preparar Assets e Presets BH.";
                    else { _preset = p; SalvarPreset(p); RebuildEditor(); }
                }
            }

            _pai = (Transform)EditorGUILayout.ObjectField(new GUIContent("Pai (opcional)", "Sobrescreve o nome do pai do preset. Deve ser um objeto da cena."), _pai, typeof(Transform), true);

            EditorGUILayout.Space();
            if (_preset != null)
            {
                if (_editor == null) RebuildEditor();
                if (_editor != null) _editor.OnInspectorGUI();

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Contar (sem alterar)", GUILayout.Height(28)))
                        _msg = VegetacaoRunner.Contar(_preset, _pai);
                    if (GUILayout.Button("Substituir", GUILayout.Height(28)))
                        _msg = Executar(_preset, _pai);
                    if (GUILayout.Button("Desfazer", GUILayout.Height(28)))
                        _msg = VegetacaoRunner.Desfazer(_preset, _pai);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Nenhum preset selecionado.", MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_msg))
                EditorGUILayout.TextArea(_msg, EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();
            _ferramentas = EditorGUILayout.Foldout(_ferramentas, "Ferramentas de assets (cidades novas)", true);
            if (_ferramentas) DrawFerramentas();

            EditorGUILayout.EndScrollView();
        }

        void DrawFerramentas()
        {
            EditorGUILayout.LabelField("Prefabs por planta (FBX com varios objetos, pivo na base)", EditorStyles.boldLabel);
            _fbxPlantas = (GameObject)EditorGUILayout.ObjectField("FBX do pacote", _fbxPlantas, typeof(GameObject), false);
            _pastaPrefabs = EditorGUILayout.TextField("Pasta de saida", _pastaPrefabs);
            _excluir = EditorGUILayout.TextField("Ignorar (virgula)", _excluir);
            _instancing = EditorGUILayout.Toggle("GPU Instancing nos materiais (.mat)", _instancing);
            _lod = EditorGUILayout.Toggle("LODGroup com cull", _lod);
            if (_lod) _lodTela = EditorGUILayout.Slider("Cull (altura de tela)", _lodTela, 0.005f, 0.2f);
            using (new EditorGUI.DisabledScope(_fbxPlantas == null))
            {
                if (GUILayout.Button("Gerar prefabs"))
                {
                    var log = new StringBuilder("== Gerar prefabs ==\n");
                    VegPrefabTools.GerarDeFilhosFbx(_fbxPlantas, _pastaPrefabs, _excluir.Split(','), _instancing, _lod, _lodTela, log);
                    log.AppendLine("Backup (se algum .mat mudou): " + VegBackup.Dir);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    _msg = log.ToString();
                    Debug.Log(_msg);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Arvore unica de uma malha mesclada (mediana, pivo na base)", EditorStyles.boldLabel);
            _fbxUnica = (GameObject)EditorGUILayout.ObjectField("FBX mesclado", _fbxUnica, typeof(GameObject), false);
            _nomeUnica = EditorGUILayout.TextField("Nome", _nomeUnica);
            _pastaMalha = EditorGUILayout.TextField("Pasta (malha e prefab)", _pastaMalha);
            using (new EditorGUI.DisabledScope(_fbxUnica == null || string.IsNullOrEmpty(_nomeUnica)))
            {
                if (GUILayout.Button("Gerar arvore unica"))
                {
                    var log = new StringBuilder("== Arvore unica ==\n");
                    var opt = _preset != null
                        ? new ClusterOptions { ThinRatio = _preset.extrRazaoFino, YTol = _preset.extrTolY, MaxAssignDist = _preset.extrDistMax }
                        : new ClusterOptions();
                    VegPrefabTools.GerarArvoreUnica(_fbxUnica, opt, _nomeUnica, _pastaMalha, _pastaMalha, false, log);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    _msg = log.ToString();
                    Debug.Log(_msg);
                }
            }
        }

        void NovoPreset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Novo preset de vegetacao", "PresetVegetacao", "asset", "Onde salvar o preset?");
            if (string.IsNullOrEmpty(path)) return;
            var p = CreateInstance<VegetacaoPreset>();
            p.classes.Add(new VegClasse { nome = "Arvores" });
            AssetDatabase.CreateAsset(p, path);
            AssetDatabase.SaveAssets();
            _preset = p;
            SalvarPreset(p);
            RebuildEditor();
        }

        void RebuildEditor()
        {
            if (_editor != null) DestroyImmediate(_editor);
            _editor = _preset != null ? Editor.CreateEditor(_preset) : null;
        }
    }
}
