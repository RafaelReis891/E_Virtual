using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vegetacao.EditorTools
{
    /// <summary>Copia de seguranca para _Backup_Deletados (fora de Assets).</summary>
    public static class VegBackup
    {
        static string s_Stamp;

        public static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        public static string Dir
        {
            get
            {
                if (s_Stamp == null) s_Stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                return ProjectRoot + "/_Backup_Deletados/VegetacaoIndividual_" + s_Stamp;
            }
        }

        public static void CopyAsset(string assetPath, bool includeAssetFile)
        {
            string src = ProjectRoot + "/" + assetPath;
            string dst = Dir + "/" + assetPath;
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            if (includeAssetFile && File.Exists(src) && !File.Exists(dst)) File.Copy(src, dst);
            if (File.Exists(src + ".meta") && !File.Exists(dst + ".meta")) File.Copy(src + ".meta", dst + ".meta");
        }
    }

    /// <summary>Liga isReadable no importador do modelo enquanto durar o escopo e restaura depois.</summary>
    public sealed class VegReadableScope : IDisposable
    {
        readonly List<string> _paths = new List<string>();

        public VegReadableScope(IEnumerable<Mesh> meshes)
        {
            var seen = new HashSet<string>();
            foreach (var m in meshes)
            {
                if (m == null || m.isReadable) continue;
                string p = AssetDatabase.GetAssetPath(m);
                if (string.IsNullOrEmpty(p) || !seen.Add(p)) continue;
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null || imp.isReadable) continue;
                VegBackup.CopyAsset(p, false);
                imp.isReadable = true;
                imp.SaveAndReimport();
                _paths.Add(p);
            }
        }

        public void Dispose()
        {
            foreach (var p in _paths)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                imp.isReadable = false;
                imp.SaveAndReimport();
            }
            _paths.Clear();
        }
    }

    /// <summary>Todas as malhas de um objeto fundidas em arrays no espaco do mundo.</summary>
    public sealed class VegMerged
    {
        public float[] pos;
        public float[] nrm;
        public int[] tri;
        public int[] triSlot;
        public List<Material> slotMat = new List<Material>();

        public static VegMerged Build(GameObject source, out string err)
        {
            err = null;
            string assetPath = AssetDatabase.GetAssetPath(source);
            var firstMeshes = new List<Mesh>();
            foreach (var mf in source.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) firstMeshes.Add(mf.sharedMesh);
            if (firstMeshes.Count == 0) { err = "Sem MeshFilter em " + source.name; return null; }

            using (new VegReadableScope(firstMeshes))
            {
                // reobter referencias apos possivel reimportacao
                if (!string.IsNullOrEmpty(assetPath)) source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (source == null) { err = "Objeto fonte invalido apos reimportar."; return null; }

                var pos = new List<float>();
                var nrm = new List<float>();
                var tri = new List<int>();
                var triSlot = new List<int>();
                var res = new VegMerged();

                foreach (var mf in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = mf.sharedMesh;
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mesh == null || mr == null) continue;
                    if (!mesh.isReadable) { err = "Malha nao legivel: " + mesh.name; return null; }

                    Material[] mats = mr.sharedMaterials;
                    Matrix4x4 m = mf.transform.localToWorldMatrix;
                    Matrix4x4 nm = m.inverse.transpose;
                    bool flip = Vector3.Dot(Vector3.Cross(m.GetColumn(0), m.GetColumn(1)), m.GetColumn(2)) < 0f;

                    Vector3[] vs = mesh.vertices;
                    Vector3[] ns = mesh.normals;
                    bool hn = ns != null && ns.Length == vs.Length;
                    int baseV = pos.Count / 3;
                    for (int i = 0; i < vs.Length; i++)
                    {
                        Vector3 p = m.MultiplyPoint3x4(vs[i]);
                        pos.Add(p.x); pos.Add(p.y); pos.Add(p.z);
                        Vector3 n = hn ? nm.MultiplyVector(ns[i]).normalized : Vector3.up;
                        nrm.Add(n.x); nrm.Add(n.y); nrm.Add(n.z);
                    }
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        int[] idx = mesh.GetTriangles(s);
                        Material mat = (mats == null || mats.Length == 0) ? null : mats[Mathf.Min(s, mats.Length - 1)];
                        int slot = res.slotMat.Count;
                        res.slotMat.Add(mat);
                        for (int k = 0; k + 2 < idx.Length; k += 3)
                        {
                            int a = baseV + idx[k], b = baseV + idx[k + 1], c = baseV + idx[k + 2];
                            if (flip) { int t = b; b = c; c = t; }
                            tri.Add(a); tri.Add(b); tri.Add(c);
                            triSlot.Add(slot);
                        }
                    }
                }
                res.pos = pos.ToArray();
                res.nrm = nrm.ToArray();
                res.tri = tri.ToArray();
                res.triSlot = triSlot.ToArray();
                return res;
            }
        }

        public List<TreeRecord> Cluster(ClusterOptions o, out int[] triTree, out int orphans)
        {
            return VegetacaoClusterCore.Cluster(pos, tri, o, out triTree, out orphans);
        }

        /// <summary>Malha de uma unica arvore, com a origem em (pivot) = centro da base.</summary>
        public Mesh BuildTreeMesh(int[] triTree, int tree, Vector3 pivot, out Material[] materials)
        {
            var vmap = new Dictionary<int, int>();
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var subMats = new List<Material>();
            var subTris = new List<List<int>>();
            for (int t = 0; t < triTree.Length; t++)
            {
                if (triTree[t] != tree) continue;
                Material mat = slotMat[triSlot[t]];
                int si = subMats.IndexOf(mat);
                if (si < 0) { si = subMats.Count; subMats.Add(mat); subTris.Add(new List<int>()); }
                for (int k = 0; k < 3; k++)
                {
                    int v = tri[3 * t + k];
                    int nv;
                    if (!vmap.TryGetValue(v, out nv))
                    {
                        nv = verts.Count;
                        vmap[v] = nv;
                        verts.Add(new Vector3(pos[3 * v], pos[3 * v + 1], pos[3 * v + 2]) - pivot);
                        norms.Add(new Vector3(nrm[3 * v], nrm[3 * v + 1], nrm[3 * v + 2]));
                    }
                    subTris[si].Add(nv);
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.subMeshCount = subTris.Count;
            for (int i = 0; i < subTris.Count; i++) mesh.SetTriangles(subTris[i], i);
            mesh.RecalculateBounds();
            materials = subMats.ToArray();
            return mesh;
        }
    }

    public static class VegCsv
    {
        static int Col(string[] header, params string[] names)
        {
            foreach (var n in names)
                for (int i = 0; i < header.Length; i++)
                    if (string.Equals(header[i].Trim(), n, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        static float F(string[] f, int i, float def)
        {
            if (i < 0 || i >= f.Length) return def;
            float v;
            return float.TryParse(f[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def;
        }

        /// <summary>Colunas aceitas: unity_x|x, unity_y|y, unity_z|z, total_height_m|height|altura,
        /// crown_width_x_m|crown_w|copa, crown_depth_z_m|crown_d, type|tipo|class.</summary>
        public static List<TreeRecord> Parse(string text, string tipo, out string err)
        {
            err = null;
            var list = new List<TreeRecord>();
            if (string.IsNullOrEmpty(text)) { err = "CSV vazio"; return list; }
            var lines = text.Replace("\uFEFF", "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) { err = "CSV sem linhas"; return list; }
            var h = lines[0].Split(',');
            int ix = Col(h, "unity_x", "x"), iy = Col(h, "unity_y", "y"), iz = Col(h, "unity_z", "z");
            int ih = Col(h, "total_height_m", "height", "altura");
            int iw = Col(h, "crown_width_x_m", "crown_w", "copa");
            int id = Col(h, "crown_depth_z_m", "crown_d");
            int it = Col(h, "type", "tipo", "class");
            if (ix < 0 || iy < 0 || iz < 0) { err = "CSV sem colunas unity_x/unity_y/unity_z (ou x/y/z): " + lines[0]; return list; }
            for (int i = 1; i < lines.Length; i++)
            {
                var f = lines[i].Split(',');
                if (f.Length <= Math.Max(ix, Math.Max(iy, iz))) continue;
                string ty = (it >= 0 && it < f.Length) ? f[it].Trim() : "";
                if (!string.IsNullOrEmpty(tipo) && it >= 0 && !string.Equals(ty, tipo, StringComparison.OrdinalIgnoreCase)) continue;
                var r = new TreeRecord { X = F(f, ix, 0), Y = F(f, iy, 0), Z = F(f, iz, 0), Tipo = ty };
                r.Height = F(f, ih, 0);
                r.CrownW = F(f, iw, 0);
                r.CrownD = id >= 0 ? F(f, id, r.CrownW) : r.CrownW;
                list.Add(r);
            }
            return list;
        }
    }

    public static class VegScene
    {
        public static bool NameMatches(string goName, string target)
        {
            if (string.IsNullOrEmpty(target)) return false;
            return goName == target || goName.StartsWith(target + " (", StringComparison.Ordinal);
        }

        /// <summary>Busca (inclusive inativos) por nome exato ou com sufixo " (n)".</summary>
        public static List<GameObject> FindAll(string name, Transform under)
        {
            var res = new List<GameObject>();
            if (string.IsNullOrEmpty(name)) return res;
            if (under != null)
            {
                foreach (var t in under.GetComponentsInChildren<Transform>(true))
                    if (NameMatches(t.name, name)) res.Add(t.gameObject);
                return res;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                foreach (var root in sc.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (NameMatches(t.name, name)) res.Add(t.gameObject);
            }
            return res;
        }

        public static GameObject FindExact(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                foreach (var root in sc.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name == name) return t.gameObject;
            }
            return null;
        }
    }

    public struct VegNativeSize
    {
        public float height;
        public float crown;
        public int tris;
    }

    public static class VegPrefabTools
    {
        public static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Altura, largura media da copa (XZ) e triangulos do prefab (escala 1), a partir dos bounds das malhas.</summary>
        public static VegNativeSize Measure(GameObject prefab)
        {
            var res = new VegNativeSize();
            bool has = false;
            Vector3 mn = Vector3.zero, mx = Vector3.zero;
            Matrix4x4 w2l = prefab.transform.worldToLocalMatrix;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                Matrix4x4 m = w2l * mf.transform.localToWorldMatrix;
                Bounds b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(c);
                    if (!has) { mn = mx = p; has = true; }
                    else { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
                }
                for (int s = 0; s < mesh.subMeshCount; s++) res.tris += (int)(mesh.GetIndexCount(s) / 3u);
            }
            if (has)
            {
                res.height = mx.y - mn.y;
                res.crown = ((mx.x - mn.x) + (mx.z - mn.z)) * 0.5f;
            }
            return res;
        }

        static string CleanName(string nodeName)
        {
            var m = Regex.Match(nodeName, @"(Palm|Banana|Fern|Monstera)_B\d+", RegexOptions.IgnoreCase);
            string n = m.Success ? "Rio_" + m.Value : "Rio_" + Regex.Replace(nodeName, @"[^A-Za-z0-9_]", "_");
            return n;
        }

        static bool Excluded(string name, string[] excl)
        {
            if (excl == null) return false;
            foreach (var e in excl)
                if (!string.IsNullOrWhiteSpace(e) && name.IndexOf(e.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static void EnableInstancing(Material mat, HashSet<Material> done, StringBuilder log)
        {
            if (mat == null || !done.Add(mat)) return;
            string p = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(p) || !p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                log.AppendLine("  [material] " + mat.name + ": embutido no FBX, nao alterado");
                return;
            }
            if (mat.enableInstancing) { log.AppendLine("  [material] " + p + ": instancing ja estava ATIVO"); return; }
            VegBackup.CopyAsset(p, true);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            log.AppendLine("  [material] " + p + ": instancing DESATIVADO -> ATIVADO (backup feito)");
        }

        /// <summary>Um prefab por filho com malha do FBX (pivo na base), com GPU Instancing e LOD de culling opcionais.</summary>
        public static List<GameObject> GerarDeFilhosFbx(GameObject fbx, string outFolder, string[] excluir,
            bool instancing, bool lodCull, float lodScreen, StringBuilder log)
        {
            var list = new List<GameObject>();
            EnsureFolder(outFolder);
            var doneMats = new HashSet<Material>();
            foreach (var node in fbx.GetComponentsInChildren<Transform>(true))
            {
                if (node == fbx.transform) continue;
                var mf = node.GetComponent<MeshFilter>();
                var mr = node.GetComponent<MeshRenderer>();
                if (mf == null || mr == null || mf.sharedMesh == null) continue;
                if (Excluded(node.name, excluir)) { log.AppendLine("  (ignorado) " + node.name); continue; }

                string pname = CleanName(node.name);
                var root = new GameObject(pname);
                var child = new GameObject("Mesh");
                child.transform.SetParent(root.transform, false);
                Matrix4x4 rel = fbx.transform.worldToLocalMatrix * node.localToWorldMatrix;
                Vector3 sc = new Vector3(rel.GetColumn(0).magnitude, rel.GetColumn(1).magnitude, rel.GetColumn(2).magnitude);
                child.transform.localScale = sc;
                var cmf = child.AddComponent<MeshFilter>();
                cmf.sharedMesh = mf.sharedMesh;
                var cmr = child.AddComponent<MeshRenderer>();
                cmr.sharedMaterials = mr.sharedMaterials;
                cmr.shadowCastingMode = mr.shadowCastingMode;
                cmr.receiveShadows = mr.receiveShadows;
                float minY = mf.sharedMesh.bounds.min.y * sc.y;
                child.transform.localPosition = new Vector3(0f, -minY, 0f);

                if (instancing)
                    foreach (var mat in cmr.sharedMaterials) EnableInstancing(mat, doneMats, log);

                if (lodCull)
                {
                    var lg = root.AddComponent<LODGroup>();
                    lg.SetLODs(new[] { new LOD(lodScreen, new Renderer[] { cmr }) });
                    lg.RecalculateBounds();
                }

                string path = outFolder + "/" + pname + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                var ms = Measure(prefab);
                log.AppendLine("  prefab " + path + "  altura=" + ms.height.ToString("0.00", CultureInfo.InvariantCulture)
                    + " m, copa=" + ms.crown.ToString("0.00", CultureInfo.InvariantCulture) + " m, tris=" + ms.tris);
                list.Add(prefab);
            }
            AssetDatabase.SaveAssets();
            return list;
        }

        /// <summary>Extrai a arvore de altura mediana de uma malha mesclada como Mesh .asset + prefab (pivo no centro da base).</summary>
        public static GameObject GerarArvoreUnica(GameObject source, ClusterOptions opt, string nomeBase,
            string meshFolder, string prefabFolder, bool batchingStatic, StringBuilder log)
        {
            string err;
            var d = VegMerged.Build(source, out err);
            if (d == null) { log.AppendLine("  ERRO " + nomeBase + ": " + err); return null; }
            int[] triTree; int orphans;
            var trees = d.Cluster(opt, out triTree, out orphans);
            if (trees.Count == 0) { log.AppendLine("  ERRO " + nomeBase + ": nenhuma arvore encontrada"); return null; }

            var order = new List<int>();
            for (int i = 0; i < trees.Count; i++) order.Add(i);
            order.Sort((a, b) => trees[a].Height.CompareTo(trees[b].Height));
            int med = order[order.Count / 2];
            var rec = trees[med];

            Material[] mats;
            Mesh mesh = d.BuildTreeMesh(triTree, med, new Vector3(rec.X, rec.Y, rec.Z), out mats);
            mesh.name = nomeBase;
            EnsureFolder(meshFolder);
            EnsureFolder(prefabFolder);
            string meshPath = meshFolder + "/" + nomeBase + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                VegBackup.CopyAsset(meshPath, true);
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else AssetDatabase.CreateAsset(mesh, meshPath);

            var root = new GameObject(nomeBase);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = mats;
            if (batchingStatic) GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            string prefabPath = prefabFolder + "/" + nomeBase + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            var ms = Measure(prefab);
            log.AppendLine("  " + nomeBase + ": " + trees.Count + " arvores achadas (orfas=" + orphans + "); mediana h="
                + rec.Height.ToString("0.00", CultureInfo.InvariantCulture) + " m, tris/arvore=" + rec.TriCount
                + ", ilhas=" + rec.IslandCount + " -> " + meshPath + " + " + prefabPath
                + " (altura " + ms.height.ToString("0.00", CultureInfo.InvariantCulture) + ", copa " + ms.crown.ToString("0.00", CultureInfo.InvariantCulture) + ")");
            return prefab;
        }
    }
}
