using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EVirtual.EditorTools.Otimizacao
{
    public class OtimizacaoWindow : EditorWindow
    {
        const float AlturaLinha = 44f;

        OtimizacaoConfig cfg = new OtimizacaoConfig();
        OtimizacaoStats stats = new OtimizacaoStats();
        List<OtimizacaoIssue> issues = new List<OtimizacaoIssue>();
        static HashSet<string> ignorados = new HashSet<string>();
        List<OtimizacaoIssue> visiveis = new List<OtimizacaoIssue>();
        int aba;
        Vector2 scroll;
        bool analisado;
        bool cacheSujo = true;
        string[] nomesAbas;
        GUIStyle estiloSev, estiloQuebra;

        [MenuItem("Tools/Otimizacao/Analisador de Cena")]
        public static void Abrir()
        {
            var w = GetWindow<OtimizacaoWindow>("Analisador de Cena");
            w.minSize = new Vector2(820, 400);
            w.Show();
        }

        void OnEnable()
        {
            cfg.Carregar();
        }

        string[] NomesAbas()
        {
            var nomes = Enum.GetNames(typeof(Categoria));
            var arr = new string[nomes.Length + 1];
            for (int i = 0; i < nomes.Length; i++)
            {
                int n = 0;
                foreach (var it in issues) if ((int)it.categoria == i && !ignorados.Contains(it.chave)) n++;
                arr[i] = nomes[i] + " (" + n + ")";
            }
            arr[nomes.Length] = "Config";
            return arr;
        }

        void Analisar()
        {
            try
            {
                stats = new OtimizacaoStats();
                issues = OtimizacaoChecks.Analisar(cfg, stats);
                analisado = true;
                if (cfg.salvarRelatorio) SalvarRelatorio();
            }
            catch (Exception e)
            {
                Debug.LogError("[Otimizacao] Falha na analise: " + e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            cacheSujo = true;
            nomesAbas = null;
            Repaint();
        }

        void SalvarRelatorio()
        {
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(dir);
                string arq = Path.Combine(dir, "Otimizacao.txt");
                var sb = new StringBuilder();
                sb.AppendLine("Analisador de Cena - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                for (int s = 0; s < EditorSceneManager.sceneCount; s++)
                    sb.AppendLine("Cena: " + EditorSceneManager.GetSceneAt(s).path);
                sb.AppendLine(Resumo());
                sb.AppendLine();
                foreach (Categoria c in Enum.GetValues(typeof(Categoria)))
                {
                    sb.AppendLine("== " + c + " ==");
                    foreach (var i in issues)
                    {
                        if (i.categoria != c) continue;
                        sb.AppendLine("[" + i.severidade + "]" + (i.PodeCorrigir ? "[fix]" : "[info]") + " " + i.nome + " | " + i.problema + " | " + i.sugestao);
                    }
                    sb.AppendLine();
                }
                File.WriteAllText(arq, sb.ToString());
                Debug.Log("[Otimizacao] Relatorio salvo em " + arq);
            }
            catch (Exception e) { Debug.LogWarning("[Otimizacao] Nao foi possivel salvar relatorio: " + e.Message); }
        }

        string Resumo()
        {
            return "Triangulos (ativos): " + stats.triangulosCena.ToString("N0") +
                "  |  Renderers: " + stats.renderers +
                "  |  Materiais unicos: " + stats.materiaisUnicos +
                "  |  Luzes realtime: " + stats.luzesRealtime +
                "  |  Texturas: " + stats.texturasAnalisadas +
                "  |  Malhas: " + stats.malhasAnalisadas +
                "  |  Audios: " + stats.audiosAnalisados +
                "  |  Problemas: " + issues.Count +
                (OtimizacaoChecks.erros > 0 ? "  |  Erros de analise: " + OtimizacaoChecks.erros + " (ver Console)" : "");
        }

        void Estilos()
        {
            if (estiloSev == null)
            {
                estiloSev = new GUIStyle(EditorStyles.boldLabel);
                estiloSev.alignment = TextAnchor.MiddleCenter;
            }
            if (estiloQuebra == null)
            {
                estiloQuebra = new GUIStyle(EditorStyles.miniLabel);
                estiloQuebra.wordWrap = false;
                estiloQuebra.clipping = TextClipping.Clip;
            }
        }

        static Color CorSev(Severidade s)
        {
            switch (s)
            {
                case Severidade.Alto: return new Color(1f, 0.35f, 0.3f);
                case Severidade.Medio: return new Color(1f, 0.75f, 0.2f);
                default: return new Color(0.5f, 0.8f, 1f);
            }
        }

        void OnGUI()
        {
            Estilos();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Analisar", EditorStyles.toolbarButton, GUILayout.Width(90))) { EditorApplication.delayCall += Analisar; }
            bool sr = GUILayout.Toggle(cfg.salvarRelatorio, "Salvar relatorio em Logs/Otimizacao.txt", EditorStyles.toolbarButton);
            if (sr != cfg.salvarRelatorio) { cfg.salvarRelatorio = sr; cfg.Salvar(); }
            if (analisado && GUILayout.Button("Salvar agora", EditorStyles.toolbarButton, GUILayout.Width(90))) SalvarRelatorio();
            if (ignorados.Count > 0 && GUILayout.Button("Mostrar ignorados (" + ignorados.Count + ")", EditorStyles.toolbarButton)) { ignorados.Clear(); cacheSujo = true; nomesAbas = null; }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (analisado) EditorGUILayout.HelpBox(Resumo(), MessageType.None);
            else EditorGUILayout.HelpBox("Clique em 'Analisar' para examinar a(s) cena(s) aberta(s).", MessageType.Info);

            if (nomesAbas == null) nomesAbas = NomesAbas();
            int nova = GUILayout.SelectionGrid(aba, nomesAbas, 5);
            if (nova != aba) { aba = nova; scroll = Vector2.zero; cacheSujo = true; }

            if (aba == nomesAbas.Length - 1) { DesenharConfig(); return; }
            DesenharLista((Categoria)aba);
        }

        void AtualizarVisiveis(Categoria c)
        {
            if (!cacheSujo) return;
            visiveis.Clear();
            foreach (var i in issues)
                if (i.categoria == c && !ignorados.Contains(i.chave)) visiveis.Add(i);
            visiveis.Sort((a, b) => a.severidade.CompareTo(b.severidade));
            cacheSujo = false;
        }

        void DesenharLista(Categoria c)
        {
            AtualizarVisiveis(c);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int marcados = 0; foreach (var i in visiveis) if (i.marcado) marcados++;
            if (GUILayout.Button(marcados == visiveis.Count && visiveis.Count > 0 ? "Desmarcar todos" : "Marcar todos", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                bool v = !(marcados == visiveis.Count && visiveis.Count > 0);
                foreach (var i in visiveis) i.marcado = v;
            }
            GUI.enabled = marcados > 0;
            if (GUILayout.Button("Corrigir marcados (" + marcados + ")", EditorStyles.toolbarButton, GUILayout.Width(150)))
            {
                var l = new List<OtimizacaoIssue>();
                foreach (var i in visiveis) if (i.marcado && i.PodeCorrigir) l.Add(i);
                Agendar(l);
            }
            GUI.enabled = true;
            if (GUILayout.Button("Corrigir todos", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                var l = new List<OtimizacaoIssue>();
                int excl = 0;
                foreach (var i in visiveis) { if (!i.PodeCorrigir) continue; if (i.incluirEmTodos) l.Add(i); else excl++; }
                string msg = "Aplicar " + l.Count + " correcoes nesta aba?" + (excl > 0 ? "\n(" + excl + " itens arriscados nao incluidos; use 'Corrigir marcados' para eles)" : "");
                if (l.Count > 0 && EditorUtility.DisplayDialog("Corrigir todos", msg, "Sim", "Cancelar")) Agendar(l);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(visiveis.Count + " itens", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (c == Categoria.Luzes) EditorGUILayout.HelpBox("Mudar luzes/probes para Baked exige rodar o bake de iluminacao manualmente (Window > Rendering > Lighting).", MessageType.None);
            if (c == Categoria.Audio) EditorGUILayout.HelpBox("Obs: no WebGL o audio e decodificado pelo navegador; Load Type tem efeito limitado.", MessageType.None);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            float altVista = position.height;
            int primeiro = Mathf.Max(0, (int)(scroll.y / AlturaLinha) - 1);
            int qtd = (int)(altVista / AlturaLinha) + 3;
            int ultimo = Mathf.Min(visiveis.Count, primeiro + qtd);
            GUILayout.Space(primeiro * AlturaLinha);
            OtimizacaoIssue acaoIgnorar = null;
            for (int k = primeiro; k < ultimo; k++)
            {
                var i = visiveis[k];
                if (DesenharLinha(i)) acaoIgnorar = i;
            }
            GUILayout.Space(Mathf.Max(0, visiveis.Count - ultimo) * AlturaLinha);
            EditorGUILayout.EndScrollView();

            if (acaoIgnorar != null) { ignorados.Add(acaoIgnorar.chave); cacheSujo = true; nomesAbas = null; Repaint(); }
        }

        // returns true if "Ignorar" clicked
        bool DesenharLinha(OtimizacaoIssue i)
        {
            bool ignorar = false;
            Rect r = EditorGUILayout.BeginHorizontal(GUILayout.Height(AlturaLinha));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0, 0, 0, 0.2f));
            i.marcado = GUILayout.Toggle(i.marcado, GUIContent.none, GUILayout.Width(18), GUILayout.Height(AlturaLinha - 4));
            var old = GUI.contentColor;
            GUI.contentColor = CorSev(i.severidade);
            GUILayout.Label(i.severidade.ToString(), estiloSev, GUILayout.Width(50), GUILayout.Height(AlturaLinha - 4));
            GUI.contentColor = old;
            EditorGUILayout.BeginVertical();
            GUILayout.Label(new GUIContent(i.nome, i.nome), EditorStyles.boldLabel);
            GUILayout.Label(new GUIContent(i.problema + "  ->  " + i.sugestao, i.problema + "\n" + i.sugestao), estiloQuebra);
            EditorGUILayout.EndVertical();
            if (GUILayout.Button("Selecionar", GUILayout.Width(75), GUILayout.Height(20)))
            {
                if (i.alvo != null) { Selection.activeObject = i.alvo; EditorGUIUtility.PingObject(i.alvo); }
            }
            GUI.enabled = i.PodeCorrigir;
            if (GUILayout.Button(i.PodeCorrigir ? "Corrigir" : "Info", GUILayout.Width(60), GUILayout.Height(20)))
                Agendar(new List<OtimizacaoIssue> { i });
            GUI.enabled = true;
            if (GUILayout.Button("Ignorar", GUILayout.Width(60), GUILayout.Height(20))) ignorar = true;
            EditorGUILayout.EndHorizontal();
            return ignorar;
        }

        void Agendar(List<OtimizacaoIssue> l)
        {
            if (l == null || l.Count == 0) return;
            EditorApplication.delayCall += () => Corrigir(l);
        }

        void Corrigir(List<OtimizacaoIssue> l)
        {
            var cena = new List<OtimizacaoIssue>();
            var porAsset = new Dictionary<string, List<OtimizacaoIssue>>();
            foreach (var i in l)
            {
                if (i.fixCena != null) cena.Add(i);
                else if (i.fixImporter != null && !string.IsNullOrEmpty(i.assetPath))
                {
                    List<OtimizacaoIssue> g;
                    if (!porAsset.TryGetValue(i.assetPath, out g)) { g = new List<OtimizacaoIssue>(); porAsset[i.assetPath] = g; }
                    g.Add(i);
                }
            }
            int ok = 0, falhas = 0;
            try
            {
                if (cena.Count > 0)
                {
                    Undo.IncrementCurrentGroup();
                    int grupo = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Otimizacao: corrigir " + cena.Count);
                    for (int k = 0; k < cena.Count; k++)
                    {
                        if (k % 10 == 0 && EditorUtility.DisplayCancelableProgressBar("Corrigindo", cena[k].nome, (float)k / cena.Count)) break;
                        try { cena[k].fixCena(); ok++; }
                        catch (Exception e) { falhas++; Debug.LogWarning("[Otimizacao] Falha ao corrigir " + cena[k].nome + ": " + e.Message); }
                    }
                    Undo.CollapseUndoOperations(grupo);
                    EditorSceneManager.MarkAllScenesDirty();
                }
                if (porAsset.Count > 0)
                {
                    int n = 0;
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        foreach (var kv in porAsset)
                        {
                            n++;
                            EditorUtility.DisplayProgressBar("Reimportando", kv.Key, (float)n / porAsset.Count);
                            try
                            {
                                var imp = AssetImporter.GetAtPath(kv.Key);
                                if (imp == null) { falhas += kv.Value.Count; continue; }
                                foreach (var i in kv.Value)
                                {
                                    try { i.fixImporter(imp); ok++; }
                                    catch (Exception e) { falhas++; Debug.LogWarning("[Otimizacao] Falha em " + kv.Key + ": " + e.Message); }
                                }
                                EditorUtility.SetDirty(imp);
                                imp.SaveAndReimport();
                            }
                            catch (Exception e) { falhas++; Debug.LogWarning("[Otimizacao] Falha ao reimportar " + kv.Key + ": " + e.Message); }
                        }
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                    }
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log("[Otimizacao] Correcoes aplicadas: " + ok + (falhas > 0 ? ", falhas: " + falhas : ""));
            Analisar();
        }

        void DesenharConfig()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Texturas", EditorStyles.boldLabel);
            cfg.maxTextura = EditorGUILayout.IntPopup("Tamanho maximo", cfg.maxTextura,
                new[] { "256", "512", "1024", "2048", "4096" }, new[] { 256, 512, 1024, 2048, 4096 });
            cfg.webglFormato = EditorGUILayout.Popup("Formato WebGL", cfg.webglFormato,
                new[] { "ASTC 6x6 (mobile)", "DXT1/DXT5 (desktop)", "DXT Crunched (menor download)" });
            EditorGUILayout.LabelField("Malhas / Fisica", EditorStyles.boldLabel);
            cfg.trisLod = EditorGUILayout.IntField("Triangulos p/ exigir LOD", cfg.trisLod);
            cfg.cullPercent = EditorGUILayout.Slider("Cull LOD (% da tela)", cfg.cullPercent, 0.1f, 20f);
            cfg.trisMeshCollider = EditorGUILayout.IntField("Triangulos MeshCollider", cfg.trisMeshCollider);
            EditorGUILayout.LabelField("Luzes / Render", EditorStyles.boldLabel);
            cfg.maxLuzesRealtime = EditorGUILayout.IntField("Max luzes realtime", cfg.maxLuzesRealtime);
            cfg.tamanhoSombraMin = EditorGUILayout.FloatField("Tamanho min. p/ sombra (m)", cfg.tamanhoSombraMin);
            cfg.instancingMinUsos = EditorGUILayout.IntField("Usos min. p/ instancing", cfg.instancingMinUsos);
            EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
            cfg.audioLongo = EditorGUILayout.FloatField("Clip longo (s)", cfg.audioLongo);
            cfg.audioCurto = EditorGUILayout.FloatField("Clip curto (s)", cfg.audioCurto);
            cfg.audioSfxMono = EditorGUILayout.FloatField("SFX mono ate (s)", cfg.audioSfxMono);
            EditorGUILayout.LabelField("UI", EditorStyles.boldLabel);
            cfg.canvasMaxGraphics = EditorGUILayout.IntField("Max graphics por Canvas", cfg.canvasMaxGraphics);
            if (EditorGUI.EndChangeCheck()) cfg.Salvar();
            EditorGUILayout.Space();
            if (GUILayout.Button("Restaurar padroes", GUILayout.Width(150))) { cfg.Padrao(); cfg.Salvar(); }
            EditorGUILayout.HelpBox("Configuracoes salvas em EditorPrefs. Rode 'Analisar' novamente para aplicar.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }
}
