using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace EVirtual.EditorTools.Otimizacao
{
    public static class OtimizacaoChecks
    {
        static List<OtimizacaoIssue> lista;
        static OtimizacaoConfig cfg;
        static OtimizacaoStats stats;
        public static int erros;

        public static List<OtimizacaoIssue> Analisar(OtimizacaoConfig config, OtimizacaoStats st)
        {
            lista = new List<OtimizacaoIssue>();
            cfg = config;
            stats = st;
            erros = 0;
            try
            {
                Etapa("Texturas", 0.05f, ChecarTexturas);
                Etapa("Malhas", 0.20f, ChecarMalhas);
                Etapa("Static", 0.35f, ChecarStatic);
                Etapa("Luzes", 0.45f, ChecarLuzes);
                Etapa("Materiais", 0.55f, ChecarMateriais);
                Etapa("Audio", 0.65f, ChecarAudio);
                Etapa("Fisica", 0.75f, ChecarFisica);
                Etapa("UI", 0.85f, ChecarUI);
                Etapa("Limpeza", 0.95f, ChecarLimpeza);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return lista;
        }

        static void Etapa(string nome, float p, Action a)
        {
            EditorUtility.DisplayProgressBar("Analisador de Cena", "Analisando " + nome + "...", p);
            Safe(a);
        }

        static void Safe(Action a)
        {
            try { a(); }
            catch (Exception e) { erros++; Debug.LogWarning("[Otimizacao] Erro na analise: " + e.Message + "\n" + e.StackTrace); }
        }

        // ---------------- helpers ----------------

        static bool Valido(UnityEngine.Object o)
        {
            if (o == null) return false;
            GameObject go = null;
            if (o is GameObject) go = (GameObject)o;
            else if (o is Component) go = ((Component)o).gameObject;
            if (go == null) return false;
            if (!go.scene.IsValid() || !go.scene.isLoaded) return false;
            if ((go.hideFlags & (HideFlags.HideInHierarchy | HideFlags.DontSave)) != 0) return false;
            if (EditorUtility.IsPersistent(go)) return false;
            return true;
        }

        static List<T> Achar<T>() where T : Component
        {
            var res = new List<T>();
            T[] arr = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (arr == null) return res;
            foreach (var c in arr) if (Valido(c)) res.Add(c);
            return res;
        }

        public static string Caminho(Transform t)
        {
            if (t == null) return "(null)";
            string s = t.name;
            int guard = 0;
            while (t.parent != null && guard++ < 64) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        static OtimizacaoIssue Add(Categoria c, Severidade sev, UnityEngine.Object alvo, string nome, string codigo, string problema, string sugestao)
        {
            var i = new OtimizacaoIssue();
            i.categoria = c; i.severidade = sev; i.alvo = alvo; i.nome = nome;
            i.problema = problema; i.sugestao = sugestao;
            i.chave = c + "|" + codigo + "|" + nome;
            lista.Add(i);
            return i;
        }

        static string NomeGO(Component c) { return c == null ? "(null)" : Caminho(c.transform); }

        public static long Triangulos(Mesh m)
        {
            if (m == null) return 0;
            long t = 0;
            try
            {
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    if (m.GetTopology(s) == MeshTopology.Triangles) t += (long)m.GetIndexCount(s) / 3;
                }
            }
            catch { }
            return t;
        }

        static Mesh MeshDe(Renderer r)
        {
            if (r == null) return null;
            if (r is SkinnedMeshRenderer) return ((SkinnedMeshRenderer)r).sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        static bool EhAssetProjeto(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        // ---------------- 1. Texturas ----------------

        class UsoTex { public Texture tex; public bool uso3D; public bool usoUI; public UnityEngine.Object exemplo; }

        static bool FormatoNaoComprimido(TextureImporterFormat f)
        {
            switch (f)
            {
                case TextureImporterFormat.RGBA32:
                case TextureImporterFormat.ARGB32:
                case TextureImporterFormat.RGB24:
                case TextureImporterFormat.RGBA16:
                case TextureImporterFormat.ARGB16:
                case TextureImporterFormat.RGB16:
                case TextureImporterFormat.Alpha8:
                case TextureImporterFormat.R8:
                case TextureImporterFormat.R16:
                case TextureImporterFormat.RGBAHalf:
                case TextureImporterFormat.RGBAFloat:
                    return true;
            }
            return false;
        }

        public static TextureImporterFormat FormatoWebGL(int modo, bool alpha)
        {
            switch (modo)
            {
                case 1: return alpha ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1;
                case 2: return alpha ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched;
                default: return TextureImporterFormat.ASTC_6x6;
            }
        }

        static void RegistrarTex(Dictionary<string, UsoTex> d, Texture t, bool ui, UnityEngine.Object ex)
        {
            if (t == null) return;
            string p = AssetDatabase.GetAssetPath(t);
            if (!EhAssetProjeto(p)) return;
            UsoTex u;
            if (!d.TryGetValue(p, out u)) { u = new UsoTex(); u.tex = t; u.exemplo = ex; d[p] = u; }
            if (ui) u.usoUI = true; else u.uso3D = true;
        }

        static void ChecarTexturas()
        {
            var usos = new Dictionary<string, UsoTex>();
            foreach (var r in Achar<Renderer>())
            {
                Safe(() =>
                {
                    bool ui = r.GetComponentInParent<Canvas>(true) != null;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null || m.shader == null) continue;
                        foreach (int id in m.GetTexturePropertyNameIDs())
                        {
                            Texture t = null;
                            try { t = m.GetTexture(id); } catch { }
                            RegistrarTex(usos, t, ui, r);
                        }
                    }
                });
            }
            foreach (var img in Achar<Image>())
                Safe(() => { if (img.sprite != null) RegistrarTex(usos, img.sprite.texture, true, img); });
            foreach (var raw in Achar<RawImage>())
                Safe(() => RegistrarTex(usos, raw.texture, true, raw));

            int n = 0;
            foreach (var kv in usos)
            {
                n++;
                if (n % 20 == 0) EditorUtility.DisplayProgressBar("Analisador de Cena", "Texturas " + n + "/" + usos.Count, 0.05f + 0.15f * n / Mathf.Max(1, usos.Count));
                string path = kv.Key;
                UsoTex u = kv.Value;
                Safe(() => ChecarTextura(path, u));
            }
            stats.texturasAnalisadas = usos.Count;
        }

        static void ChecarTextura(string path, UsoTex u)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            string nome = path;
            Texture t = u.tex;
            int w = t.width, h = t.height;
            int max = cfg.maxTextura;

            if (Mathf.Max(w, h) > max && ti.maxTextureSize > max)
            {
                var i = Add(Categoria.Texturas, Severidade.Alto, t, nome, "max", "Tamanho " + w + "x" + h + " (max import " + ti.maxTextureSize + ")", "Definir Max Size para " + max);
                i.assetPath = path; i.fixImporter = imp => { var x = imp as TextureImporter; if (x != null) x.maxTextureSize = max; };
            }

            TextureImporterPlatformSettings ps = ti.GetPlatformTextureSettings("WebGL");
            bool semOverride = ps == null || !ps.overridden;
            bool naoComp = ps != null && ps.overridden && FormatoNaoComprimido(ps.format);
            if (semOverride || naoComp)
            {
                int modo = cfg.webglFormato;
                bool alpha = false;
                try { alpha = ti.DoesSourceTextureHaveAlpha(); } catch { }
                var fmt = FormatoWebGL(modo, alpha);
                var i = Add(Categoria.Texturas, naoComp ? Severidade.Alto : Severidade.Medio, t, nome, "webgl",
                    naoComp ? "Override WebGL sem compressao (" + ps.format + ")" : "Sem override de plataforma WebGL",
                    "Criar override WebGL com formato " + fmt);
                i.assetPath = path;
                i.fixImporter = imp =>
                {
                    var x = imp as TextureImporter; if (x == null) return;
                    var s = x.GetPlatformTextureSettings("WebGL");
                    if (s == null) { s = new TextureImporterPlatformSettings(); s.name = "WebGL"; }
                    bool a = false; try { a = x.DoesSourceTextureHaveAlpha(); } catch { }
                    s.name = "WebGL";
                    s.overridden = true;
                    s.format = FormatoWebGL(modo, a);
                    s.maxTextureSize = Mathf.Min(x.maxTextureSize, max);
                    if (modo == 2) { s.crunchedCompression = true; s.compressionQuality = 50; }
                    x.SetPlatformTextureSettings(s);
                };
            }

            if (ti.textureCompression == TextureImporterCompression.Uncompressed)
            {
                var i = Add(Categoria.Texturas, Severidade.Medio, t, nome, "comp", "Compressao padrao = Nenhuma", "Definir compressao Normal");
                i.assetPath = path; i.fixImporter = imp => { var x = imp as TextureImporter; if (x != null) x.textureCompression = TextureImporterCompression.Compressed; };
            }

            if (u.uso3D && !u.usoUI && !ti.mipmapEnabled && ti.textureType != TextureImporterType.Sprite)
            {
                var i = Add(Categoria.Texturas, Severidade.Medio, t, nome, "mipon", "Mipmaps desligados em textura 3D", "Ativar mipmaps (melhora cache e aliasing)");
                i.assetPath = path; i.fixImporter = imp => { var x = imp as TextureImporter; if (x != null) x.mipmapEnabled = true; };
            }
            if (u.usoUI && !u.uso3D && ti.mipmapEnabled)
            {
                var i = Add(Categoria.Texturas, Severidade.Baixo, t, nome, "mipoff", "Mipmaps ligados em textura de UI", "Desativar mipmaps (economiza ~33% memoria)");
                i.assetPath = path; i.fixImporter = imp => { var x = imp as TextureImporter; if (x != null) x.mipmapEnabled = false; };
            }
            if (ti.isReadable)
            {
                var i = Add(Categoria.Texturas, Severidade.Medio, t, nome, "rw", "Read/Write ativado (dobra memoria)", "Desativar Read/Write (verifique se nenhum script le os pixels)");
                i.assetPath = path; i.fixImporter = imp => { var x = imp as TextureImporter; if (x != null) x.isReadable = false; };
            }
            if ((!Mathf.IsPowerOfTwo(w) || !Mathf.IsPowerOfTwo(h)) && ti.npotScale == TextureImporterNPOTScale.None && ti.textureType != TextureImporterType.Sprite)
            {
                Add(Categoria.Texturas, Severidade.Baixo, t, nome, "npot", "Dimensoes nao potencia de 2 (" + w + "x" + h + ")", "Algumas compressoes exigem POT/multiplo de 4; considere redimensionar (info)");
            }
        }

        // ---------------- 2. Malhas ----------------

        static void ChecarMalhas()
        {
            var meshes = new Dictionary<string, Mesh>();
            long total = 0;
            var renderers = Achar<Renderer>();
            stats.renderers = renderers.Count;
            foreach (var r in renderers)
            {
                Safe(() =>
                {
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) return;
                    Mesh m = MeshDe(r);
                    if (m == null) return;
                    long tris = Triangulos(m);
                    if (r.gameObject.activeInHierarchy && r.enabled) total += tris;
                    string p = AssetDatabase.GetAssetPath(m);
                    if (EhAssetProjeto(p) && !meshes.ContainsKey(p)) meshes[p] = m;

                    if (tris > cfg.trisLod && r.GetComponentInParent<LODGroup>(true) == null)
                    {
                        GameObject go = r.gameObject;
                        float cull = Mathf.Clamp(cfg.cullPercent, 0.1f, 50f) / 100f;
                        var i = Add(Categoria.Malhas, Severidade.Medio, go, NomeGO(r), "lod", tris + " triangulos sem LODGroup", "Adicionar LODGroup (cull em " + cfg.cullPercent + "% da tela; usa irmaos _LOD1/_LOD2 se existirem)");
                        i.fixCena = () => CriarLOD(go, cull);
                    }
                });
            }
            stats.triangulosCena = total;

            foreach (var kv in meshes)
            {
                string path = kv.Key; Mesh m = kv.Value;
                Safe(() =>
                {
                    var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (mi == null) return;
                    if (mi.isReadable)
                    {
                        var i = Add(Categoria.Malhas, Severidade.Medio, m, path, "rw", "Read/Write ativado no modelo", "Desativar Read/Write (risco: scripts que leem/alteram vertices em runtime)");
                        i.assetPath = path; i.fixImporter = imp => { var x = imp as ModelImporter; if (x != null) x.isReadable = false; };
                    }
                    if (mi.meshCompression == ModelImporterMeshCompression.Off)
                    {
                        var i = Add(Categoria.Malhas, Severidade.Baixo, m, path, "comp", "Compressao de malha desligada", "Definir Mesh Compression = Medium (reduz tamanho do build)");
                        i.assetPath = path; i.fixImporter = imp => { var x = imp as ModelImporter; if (x != null) x.meshCompression = ModelImporterMeshCompression.Medium; };
                    }
                });
            }
            stats.malhasAnalisadas = meshes.Count;
        }

        static Renderer[] RenderersDe(GameObject g)
        {
            return g == null ? new Renderer[0] : g.GetComponentsInChildren<Renderer>(true);
        }

        static void CriarLOD(GameObject go, float cull)
        {
            if (go == null) return;
            GameObject alvo = go;
            var niveis = new List<Renderer[]>();
            string nome = go.name;
            int idx = nome.IndexOf("_LOD0", StringComparison.OrdinalIgnoreCase);
            Transform parent = go.transform.parent;
            if (idx >= 0 && parent != null)
            {
                string bas = nome.Substring(0, idx);
                niveis.Add(RenderersDe(go));
                for (int k = 1; k <= 6; k++)
                {
                    Transform s = parent.Find(bas + "_LOD" + k);
                    if (s == null) break;
                    niveis.Add(RenderersDe(s.gameObject));
                }
                if (niveis.Count > 1 && parent.GetComponent<LODGroup>() == null) alvo = parent.gameObject;
                else { niveis.Clear(); }
            }
            if (niveis.Count == 0) niveis.Add(RenderersDe(go));

            LODGroup lg = alvo.GetComponent<LODGroup>();
            if (lg == null) lg = Undo.AddComponent<LODGroup>(alvo);
            else Undo.RecordObject(lg, "LODGroup");
            var lods = new LOD[niveis.Count];
            for (int k = 0; k < niveis.Count; k++)
            {
                float hgt = (k == niveis.Count - 1) ? cull : Mathf.Max(cull + 0.01f * (niveis.Count - k), 0.5f / (1 << k));
                lods[k] = new LOD(hgt, niveis[k]);
            }
            lg.SetLODs(lods);
            lg.RecalculateBounds();
            EditorUtility.SetDirty(lg);
        }

        // ---------------- 3. Static ----------------

        const StaticEditorFlags FlagsStatic = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic;

        static void ChecarStatic()
        {
            foreach (var mr in Achar<MeshRenderer>())
            {
                Safe(() =>
                {
                    GameObject go = mr.gameObject;
                    var flags = GameObjectUtility.GetStaticEditorFlags(go);
                    if ((flags & StaticEditorFlags.BatchingStatic) != 0) return;
                    if (go.GetComponentInParent<Rigidbody>(true) != null) return;
                    if (go.GetComponentInParent<Animator>(true) != null) return;
                    if (go.GetComponentInParent<Animation>(true) != null) return;
                    if (go.GetComponentInParent<ParticleSystem>(true) != null) return;
                    if (go.GetComponentInParent<Canvas>(true) != null) return;
                    if (go.GetComponent<MeshFilter>() == null) return;
                    bool temScript = false;
                    foreach (var mb in go.GetComponents<MonoBehaviour>())
                    {
                        if (mb == null) continue;
                        string ns = mb.GetType().Namespace ?? "";
                        if (ns.StartsWith("UnityEngine.Rendering", StringComparison.Ordinal) || ns.StartsWith("UnityEngine.ProBuilder", StringComparison.Ordinal)) continue;
                        temScript = true; break;
                    }
                    if (temScript)
                    {
                        Add(Categoria.Static, Severidade.Baixo, go, NomeGO(mr), "script", "Nao static, mas tem scripts", "Verifique se o objeto se move; se nao, marque como Static manualmente (info)");
                    }
                    else
                    {
                        var i = Add(Categoria.Static, Severidade.Medio, go, NomeGO(mr), "static", "Objeto parado sem flags Static", "Marcar Batching/Occluder/Occludee/ContributeGI/ReflectionProbe Static");
                        i.fixCena = () =>
                        {
                            if (go == null) return;
                            Undo.RecordObject(go, "Static flags");
                            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | FlagsStatic);
                        };
                    }
                });
            }
        }

        // ---------------- 4. Luzes ----------------

        static void ChecarLuzes()
        {
            var realtime = new List<Light>();
            foreach (var l in Achar<Light>())
            {
                Safe(() =>
                {
                    if (!l.enabled || !l.gameObject.activeInHierarchy) return;
                    if (l.lightmapBakeType != LightmapBakeType.Realtime) return;
                    realtime.Add(l);
                    if (l.shadows != LightShadows.None)
                    {
                        Light luz = l;
                        if (l.type != LightType.Directional)
                        {
                            var i = Add(Categoria.Luzes, Severidade.Alto, l, NomeGO(l), "bake", "Luz " + l.type + " realtime com sombras", "Mudar para Baked (fix explicito; depois rode o bake de iluminacao manualmente)");
                            i.fixCena = () => { if (luz == null) return; Undo.RecordObject(luz, "Bake luz"); luz.lightmapBakeType = LightmapBakeType.Baked; };
                            var j = Add(Categoria.Luzes, Severidade.Medio, l, NomeGO(l), "sombra", "Sombras em luz " + l.type + " (caro)", "Desativar sombras desta luz");
                            j.fixCena = () => { if (luz == null) return; Undo.RecordObject(luz, "Sem sombra"); luz.shadows = LightShadows.None; };
                        }
                        else
                        {
                            Add(Categoria.Luzes, Severidade.Baixo, l, NomeGO(l), "dir", "Luz direcional realtime com sombras", "Considere Mixed + bake, ou reduzir distancia/cascatas de sombra no URP Asset (info)");
                        }
                    }
                });
            }
            stats.luzesRealtime = realtime.Count;
            if (realtime.Count > cfg.maxLuzesRealtime)
                Add(Categoria.Luzes, Severidade.Alto, null, "(cena)", "qtd", realtime.Count + " luzes realtime ativas", "Reduza para <= " + cfg.maxLuzesRealtime + " ou faca bake (WebGL usa forward com limite por objeto)");

            foreach (var r in Achar<Renderer>())
            {
                Safe(() =>
                {
                    if (r.shadowCastingMode == ShadowCastingMode.Off) return;
                    if (!r.gameObject.activeInHierarchy || !r.enabled) return;
                    string motivo = null;
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) motivo = "Particulas/linha projetando sombra";
                    else if (r.GetComponentInParent<Canvas>(true) != null) motivo = "Renderer em Canvas projetando sombra";
                    else if (r.bounds.size.magnitude < cfg.tamanhoSombraMin) motivo = "Objeto pequeno (" + r.bounds.size.magnitude.ToString("0.00") + "m) projetando sombra";
                    if (motivo == null) return;
                    Renderer rr = r;
                    var i = Add(Categoria.Luzes, Severidade.Baixo, r, NomeGO(r), "cast", motivo, "Cast Shadows = Off");
                    i.fixCena = () => { if (rr == null) return; Undo.RecordObject(rr, "Sem sombra"); rr.shadowCastingMode = ShadowCastingMode.Off; };
                });
            }
        }

        // ---------------- 5. Materiais / Render ----------------

        static void ChecarMateriais()
        {
            var usos = new Dictionary<Material, int>();
            foreach (var r in Achar<Renderer>())
            {
                Safe(() =>
                {
                    var mats = r.sharedMaterials;
                    bool faltando = mats == null || mats.Length == 0;
                    if (mats != null) foreach (var m in mats)
                    {
                        if (m == null) { faltando = true; continue; }
                        int c; usos.TryGetValue(m, out c); usos[m] = c + 1;
                    }
                    if (faltando && !(r is ParticleSystemRenderer))
                        Add(Categoria.Materiais, Severidade.Alto, r, NomeGO(r), "missing", "Material ausente (slot vazio)", "Atribua um material valido");
                });
            }
            stats.materiaisUnicos = usos.Count;
            foreach (var kv in usos)
            {
                Material m = kv.Key; int n = kv.Value;
                Safe(() =>
                {
                    string nome = AssetDatabase.GetAssetPath(m);
                    if (string.IsNullOrEmpty(nome)) nome = m.name + " (instancia)";
                    if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported)
                    {
                        Add(Categoria.Materiais, Severidade.Alto, m, nome, "pink", "Shader ausente/erro (material rosa)", "Troque para um shader URP valido");
                        return;
                    }
                    if (n >= cfg.instancingMinUsos && !m.enableInstancing && EditorUtility.IsPersistent(m))
                    {
                        var i = Add(Categoria.Materiais, Severidade.Baixo, m, nome, "inst", "Usado por " + n + " renderers sem GPU Instancing", "Ativar GPU Instancing (obs: no URP o SRP Batcher tem prioridade)");
                        i.fixCena = () => { if (m == null) return; Undo.RecordObject(m, "Instancing"); m.enableInstancing = true; EditorUtility.SetDirty(m); };
                    }
                });
            }

            var cams = new List<Camera>();
            foreach (var c in Achar<Camera>())
                if (c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null) cams.Add(c);
            if (cams.Count > 1)
                for (int k = 1; k < cams.Count; k++)
                    Add(Categoria.Materiais, Severidade.Medio, cams[k], NomeGO(cams[k]), "cam", "Camera extra ativa (" + cams.Count + " no total)", "Desative cameras nao usadas ou use Camera Stack conscientemente (info)");

            foreach (var rp in Achar<ReflectionProbe>())
            {
                Safe(() =>
                {
                    if (rp.mode != ReflectionProbeMode.Realtime) return;
                    ReflectionProbe p = rp;
                    var i = Add(Categoria.Materiais, Severidade.Medio, rp, NomeGO(rp), "probe", "Reflection Probe realtime", "Mudar para Baked (depois faca bake manualmente)");
                    i.fixCena = () => { if (p == null) return; Undo.RecordObject(p, "Probe baked"); p.mode = ReflectionProbeMode.Baked; };
                });
            }

            Safe(ChecarVolumes);
            Safe(ChecarRendererFeatures);
        }

        static object Membro(object o, string nome)
        {
            if (o == null) return null;
            var t = o.GetType();
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            while (t != null)
            {
                var p = t.GetProperty(nome, bf);
                if (p != null && p.GetIndexParameters().Length == 0) { try { return p.GetValue(o, null); } catch { return null; } }
                var f = t.GetField(nome, bf);
                if (f != null) { try { return f.GetValue(o); } catch { return null; } }
                t = t.BaseType;
            }
            return null;
        }

        static Type AcharTipo(string nome)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = a.GetType(nome, false); if (t != null) return t; } catch { }
            }
            return null;
        }

        static void ChecarVolumes()
        {
            Type tv = AcharTipo("UnityEngine.Rendering.Volume");
            if (tv == null) return;
            var vols = UnityEngine.Object.FindObjectsByType(tv, FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var vo in vols)
            {
                var v = vo as Component;
                if (!Valido(v)) continue;
                Safe(() =>
                {
                    object prof = Membro(v, "sharedProfile");
                    var comps = Membro(prof, "components") as IEnumerable;
                    if (comps == null) return;
                    foreach (var c in comps)
                    {
                        if (c == null) continue;
                        object ativo = Membro(c, "active");
                        if (ativo is bool && !(bool)ativo) continue;
                        string tn = c.GetType().Name;
                        string msg = null; Severidade sev = Severidade.Baixo;
                        if (tn == "DepthOfField") { msg = "Depth Of Field ativo"; sev = Severidade.Medio; }
                        else if (tn == "MotionBlur") { msg = "Motion Blur ativo"; sev = Severidade.Medio; }
                        else if (tn == "Bloom")
                        {
                            object hq = Membro(Membro(c, "highQualityFiltering"), "value");
                            if (hq is bool && (bool)hq) { msg = "Bloom com High Quality Filtering"; sev = Severidade.Medio; }
                        }
                        else if (tn == "ChromaticAberration" || tn == "FilmGrain" || tn == "LensDistortion" || tn == "PaniniProjection") msg = tn + " ativo";
                        if (msg != null)
                            Add(Categoria.Materiais, sev, v, NomeGO(v), "vol" + tn, "Volume: " + msg, "Efeito caro em WebGL; considere desativar ou reduzir qualidade (info)");
                    }
                });
            }
        }

        static void ChecarRendererFeatures()
        {
            object rpa = GraphicsSettings.currentRenderPipeline;
            if (rpa == null) return;
            var lista2 = Membro(rpa, "m_RendererDataList") as IEnumerable;
            if (lista2 == null) return;
            foreach (var rd in lista2)
            {
                if (rd == null) continue;
                var feats = Membro(rd, "rendererFeatures") as IEnumerable;
                if (feats == null) continue;
                foreach (var f in feats)
                {
                    if (f == null) continue;
                    string tn = f.GetType().Name;
                    object ativo = Membro(f, "isActive");
                    if (ativo is bool && !(bool)ativo) continue;
                    if (tn.Contains("ScreenSpaceAmbientOcclusion") || tn.Contains("SSAO"))
                        Add(Categoria.Materiais, Severidade.Medio, rd as UnityEngine.Object, (rd as UnityEngine.Object) != null ? ((UnityEngine.Object)rd).name : "Renderer", "ssao", "Renderer Feature SSAO ativa", "SSAO e caro em WebGL; considere remover/desativar (info)");
                }
            }
        }

        // ---------------- 6. Audio ----------------

        static void ChecarAudio()
        {
            var clips = new Dictionary<string, AudioClip>();
            foreach (var s in Achar<AudioSource>())
            {
                if (s.clip == null) continue;
                string p = AssetDatabase.GetAssetPath(s.clip);
                if (EhAssetProjeto(p) && !clips.ContainsKey(p)) clips[p] = s.clip;
            }
            stats.audiosAnalisados = clips.Count;
            foreach (var kv in clips)
            {
                string path = kv.Key; AudioClip clip = kv.Value;
                Safe(() =>
                {
                    var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (ai == null) return;
                    float len = clip.length;
                    var ss = ai.defaultSampleSettings;
                    string dur = len.ToString("0.0") + "s";
                    if (len > cfg.audioLongo && ss.loadType != AudioClipLoadType.Streaming)
                    {
                        var i = Add(Categoria.Audio, Severidade.Medio, clip, path, "stream", "Clip longo (" + dur + ") com " + ss.loadType + (clip.preloadAudioData ? ", preload ligado" : ""), "Load Type = Streaming + Vorbis");
                        i.assetPath = path; i.fixImporter = imp =>
                        {
                            var x = imp as AudioImporter; if (x == null) return;
                            var s2 = x.defaultSampleSettings; s2.loadType = AudioClipLoadType.Streaming; s2.compressionFormat = AudioCompressionFormat.Vorbis; x.defaultSampleSettings = s2;
                        };
                    }
                    if (len < cfg.audioCurto && ss.loadType != AudioClipLoadType.DecompressOnLoad)
                    {
                        var i = Add(Categoria.Audio, Severidade.Baixo, clip, path, "decomp", "Clip curto (" + dur + ") com " + ss.loadType, "Load Type = Decompress On Load");
                        i.assetPath = path; i.fixImporter = imp =>
                        {
                            var x = imp as AudioImporter; if (x == null) return;
                            var s2 = x.defaultSampleSettings; s2.loadType = AudioClipLoadType.DecompressOnLoad; x.defaultSampleSettings = s2;
                        };
                    }
                    if (len < cfg.audioSfxMono && !ai.forceToMono && clip.channels > 1)
                    {
                        var i = Add(Categoria.Audio, Severidade.Baixo, clip, path, "mono", "SFX curto (" + dur + ") em estereo", "Ativar Force To Mono (metade da memoria)");
                        i.assetPath = path; i.fixImporter = imp => { var x = imp as AudioImporter; if (x != null) x.forceToMono = true; };
                    }
                });
            }
        }

        // ---------------- 7. Fisica ----------------

        static void ChecarFisica()
        {
            foreach (var mc in Achar<MeshCollider>())
            {
                Safe(() =>
                {
                    if (mc.convex) return;
                    MeshCollider col = mc;
                    long tris = Triangulos(mc.sharedMesh);
                    var rb = mc.GetComponentInParent<Rigidbody>(true);
                    if (rb != null && !rb.isKinematic)
                    {
                        var i = Add(Categoria.Fisica, Severidade.Alto, mc, NomeGO(mc), "rb", "MeshCollider nao convexo em Rigidbody dinamico (erro)", "Marcar Convex");
                        i.fixCena = () => { if (col == null) return; Undo.RecordObject(col, "Convex"); col.convex = true; };
                    }
                    else if (tris > cfg.trisMeshCollider)
                    {
                        var i = Add(Categoria.Fisica, Severidade.Medio, mc, NomeGO(mc), "box", "MeshCollider nao convexo com " + tris + " triangulos", "Substituir por BoxCollider ajustado aos bounds (perde precisao)");
                        i.fixCena = () => TrocarPorBox(col);
                    }
                });
            }
            foreach (var c in Achar<Collider>())
            {
                Safe(() =>
                {
                    if (!(c.transform is RectTransform)) return;
                    if (c.GetComponentInParent<Canvas>(true) == null) return;
                    Collider cc = c;
                    var i = Add(Categoria.Fisica, Severidade.Baixo, c, NomeGO(c), "ui", "Collider em objeto de UI", "Remover collider (UI usa GraphicRaycaster)");
                    i.fixCena = () => { if (cc != null) Undo.DestroyObjectImmediate(cc); };
                    i.incluirEmTodos = false;
                });
            }
        }

        static void TrocarPorBox(MeshCollider mc)
        {
            if (mc == null) return;
            GameObject go = mc.gameObject;
            Bounds b = mc.sharedMesh != null ? mc.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            bool trig = mc.isTrigger;
            var mat = mc.sharedMaterial;
            bool en = mc.enabled;
            var bc = Undo.AddComponent<BoxCollider>(go);
            if (bc == null) return;
            bc.center = b.center;
            bc.size = b.size;
            bc.isTrigger = trig;
            bc.sharedMaterial = mat;
            bc.enabled = en;
            Undo.DestroyObjectImmediate(mc);
        }

        // ---------------- 8. UI ----------------

        static bool TemHandler(Component g)
        {
            Transform t = g.transform;
            while (t != null)
            {
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                    if (mb is IEventSystemHandler) return true;
                if (t.GetComponent<Canvas>() != null && t.GetComponent<Canvas>().isRootCanvas) break;
                t = t.parent;
            }
            return false;
        }

        static void ChecarUI()
        {
            foreach (var c in Achar<Canvas>())
            {
                Safe(() =>
                {
                    Canvas cv = c;
                    bool raiz = c.transform.parent == null || c.transform.parent.GetComponentInParent<Canvas>(true) == null;
                    if (raiz)
                    {
                        int n = c.GetComponentsInChildren<Graphic>(true).Length;
                        if (n > cfg.canvasMaxGraphics)
                            Add(Categoria.UI, Severidade.Medio, c, NomeGO(c), "split", "Canvas com " + n + " graphics", "Divida em sub-Canvases (partes estaticas x dinamicas) para reduzir rebuilds (info)");
                    }
                    else
                    {
                        Add(Categoria.UI, Severidade.Baixo, c, NomeGO(c), "nested", "Canvas aninhado", "OK para isolar rebuilds; evite em excesso (info)");
                    }
                    if (c.pixelPerfect && raiz)
                    {
                        var i = Add(Categoria.UI, Severidade.Medio, c, NomeGO(c), "pp", "Pixel Perfect ligado", "Desligar Pixel Perfect (caro com layouts/animacoes)");
                        i.fixCena = () => { if (cv == null) return; Undo.RecordObject(cv, "Pixel Perfect"); cv.pixelPerfect = false; };
                    }
                });
            }
            foreach (var g in Achar<Graphic>())
            {
                Safe(() =>
                {
                    if (!g.raycastTarget) return;
                    if (TemHandler(g)) return;
                    Graphic gg = g;
                    var i = Add(Categoria.UI, Severidade.Baixo, g, NomeGO(g), "ray", "Raycast Target sem handler de evento", "Desligar Raycast Target (cuidado: paineis que bloqueiam cliques intencionalmente)");
                    i.fixCena = () => { if (gg == null) return; Undo.RecordObject(gg, "Raycast"); gg.raycastTarget = false; };
                    i.incluirEmTodos = false;
                });
            }
            foreach (var t in Achar<Text>())
            {
                Safe(() =>
                {
                    if (!t.resizeTextForBestFit) return;
                    Text tt = t;
                    var i = Add(Categoria.UI, Severidade.Medio, t, NomeGO(t), "bestfit", "Text legado com Best Fit", "Desligar Best Fit (ou migrar para TextMeshPro)");
                    i.fixCena = () => { if (tt == null) return; Undo.RecordObject(tt, "Best Fit"); tt.resizeTextForBestFit = false; };
                });
            }
        }

        // ---------------- 9. Limpeza ----------------

        static void ChecarLimpeza()
        {
            foreach (var tr in Achar<Transform>())
            {
                Safe(() =>
                {
                    GameObject go = tr.gameObject;
                    int miss = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (miss > 0)
                    {
                        var i = Add(Categoria.Limpeza, Severidade.Alto, go, Caminho(tr), "missing", miss + " script(s) ausente(s)", "Remover componentes com script ausente");
                        i.fixCena = () =>
                        {
                            if (go == null) return;
                            Undo.RegisterCompleteObjectUndo(go, "Remover scripts ausentes");
                            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                        };
                    }
                    if (tr.childCount == 0 && go.GetComponents<Component>().Length == 1 && !(tr is RectTransform) && !EhParteDeRig(tr))
                    {
                        var i = Add(Categoria.Limpeza, Severidade.Baixo, go, Caminho(tr), "vazio", "GameObject vazio (so Transform, sem filhos)", "Pode ser removido (cuidado: pode ser usado como ponto de referencia por scripts)");
                        i.incluirEmTodos = false;
                        if (!PrefabUtility.IsPartOfPrefabInstance(go))
                            i.fixCena = () => { if (go != null) Undo.DestroyObjectImmediate(go); };
                    }
                });
            }
            foreach (var r in Achar<Renderer>())
            {
                Safe(() =>
                {
                    if (r.gameObject.activeInHierarchy) return;
                    long tris = Triangulos(MeshDe(r));
                    if (tris > cfg.trisLod)
                        Add(Categoria.Limpeza, Severidade.Baixo, r, NomeGO(r), "inativo", "Objeto desativado com malha pesada (" + tris + " tris)", "Ainda entra no build/memoria; remova se nao for usado (info)");
                });
            }
            foreach (var s in Achar<AudioSource>())
            {
                Safe(() =>
                {
                    if (!s.playOnAwake || s.clip != null) return;
                    AudioSource ss = s;
                    var i = Add(Categoria.Limpeza, Severidade.Baixo, s, NomeGO(s), "awake", "AudioSource Play On Awake sem clip", "Desligar Play On Awake");
                    i.fixCena = () => { if (ss == null) return; Undo.RecordObject(ss, "PlayOnAwake"); ss.playOnAwake = false; };
                });
            }
        }
            // Ossos de rig (Animator / SkinnedMeshRenderer) nunca devem ser tratados como vazios
        static HashSet<Transform> _ossos;
        static int _ossosFrame = -1;
        static bool EhParteDeRig(Transform tr)
        {
            if (_ossos == null || _ossosFrame != Time.frameCount || _ossos.Count == 0)
            {
                _ossos = new HashSet<Transform>();
                _ossosFrame = Time.frameCount;
                foreach (var smr in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (smr == null) continue;
                    if (smr.rootBone != null) _ossos.Add(smr.rootBone);
                    var b = smr.bones;
                    if (b != null) foreach (var o in b) if (o != null) _ossos.Add(o);
                }
            }
            if (_ossos.Contains(tr)) return true;
            if (tr.GetComponentInParent<Animator>(true) != null) return true;
            string n = tr.name.ToLowerInvariant();
            if (n.EndsWith("_end") || n.EndsWith("end_end")) return true;
            for (var p = tr.parent; p != null; p = p.parent)
            {
                string pn = p.name.ToLowerInvariant();
                if (pn == "armature" || pn.Contains("hips") || pn.StartsWith("bip") || pn.Contains("mixamorig")) return true;
            }
            return false;
        }
}
}
