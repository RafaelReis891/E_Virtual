// Gera BoxColliders finos no lugar do MeshCollider (mesh completo) do GameObject "Rails".
//
// Menu:  Tools > Emuseu > Gerar Colliders do Rail
//        Tools > Emuseu > Remover Colliders do Rail
//
// Como funciona (tudo calculado no Editor, em espaco de MUNDO):
//  1. Le os vertices/triangulos do mesh do Rails (MeshFilter.sharedMesh) e aplica localToWorld.
//  2. Projeta no plano XZ e rasteriza o "footprint" (celulas de 5 cm). Os balaustres e corrimaos
//     viram uma faixa continua com um fechamento morfologico (raio 0.4 m). Vaos reais da malha
//     (escadas, entradas) NAO sao fechados: so some o que estiver mais perto que 2x o raio.
//  3. Esqueletiza a faixa (Zhang-Suen), traca as cadeias de pixels, poda esporoes curtos e
//     simplifica com Douglas-Peucker (tolerancia 0.30 m; aumenta sozinha se passar de MaxSegments).
//  4. Cada segmento vira um BoxCollider fino (0.4 m) e alto (>= 3 m). A altura vem dos vertices do
//     proprio trecho (rampas funcionam). Os boxes ficam em filhos de um GameObject irmao do Rails
//     chamado "Rails_Colliders" (mesma layer / static flags do Rails).
//  5. O MeshCollider do Rails e DESABILITADO (nao removido). Tudo com Undo; cena marcada como suja.
//
// O nucleo (RailsOutlineCore) nao usa UnityEngine; a parte de Unity fica dentro de #if UNITY_EDITOR.
using System;
using System.Collections.Generic;

namespace Emuseu.EditorTools
{
    public struct RailBoxSpec
    {
        public double cx, cy, cz;   // centro em mundo
        public double sx, sy, sz;   // tamanho em mundo: sx = espessura, sy = altura, sz = comprimento (eixo local Z do box)
        public double yaw;          // graus, em torno de Y, em mundo (Unity: 0 = olhando para +Z)
        public double ax, az, bx, bz; // extremos do segmento (eixo central) em mundo
    }

    public class RailOutlineParams
    {
        public double Cell = 0.05;            // resolucao do raster (m)
        public double CloseRadius = 0.40;     // fecha o espaco entre balaustres (m)
        public double SpurLength = 0.60;      // esporoes de esqueleto menores que isso sao podados (m)
        public int MinChainCells = 6;         // cadeias menores sao ruido de juncao
        public double Tolerance = 0.30;       // Douglas-Peucker (m)
        public double MinSegmentLength = 0.15;
        public int MaxSegments = 100;         // se passar, a tolerancia sobe ate caber
        public double Thickness = 0.40;       // espessura do box (m)
        public double MinHeight = 3.0;        // altura minima do box (m)
        public double ExtraAboveRail = 2.0;   // quanto o box passa acima do topo do corrimao (m)
        public double BelowRail = 0.3;        // quanto o box desce abaixo da base do trecho (m)
        public double StripHalfWidth = 0.5;   // faixa em volta do segmento usada p/ medir altura (m)
    }

    public static class RailsOutlineCore
    {
        public static List<RailBoxSpec> Compute(float[] vx, float[] vy, float[] vz, int[] tris,
            RailOutlineParams p, out string report, Action<string, float> progress = null)
        {
            if (progress == null) progress = (s, f) => { };
            var sb = new System.Text.StringBuilder();
            int nv = vx.Length;

            // ---------- 1. raster do footprint ----------
            progress("Rasterizando footprint", 0.05f);
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int i = 0; i < nv; i++)
            {
                if (vx[i] < minX) minX = vx[i]; if (vx[i] > maxX) maxX = vx[i];
                if (vz[i] < minZ) minZ = vz[i]; if (vz[i] > maxZ) maxZ = vz[i];
            }
            double margin = p.CloseRadius + 1.0;
            minX -= margin; minZ -= margin; maxX += margin; maxZ += margin;
            double cell = p.Cell;
            int W = (int)((maxX - minX) / cell) + 2, H = (int)((maxZ - minZ) / cell) + 2;
            var grid = new bool[W * H];
            int triCount = tris.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];
                double ax = vx[i0], az = vz[i0], bx = vx[i1], bz = vz[i1], cx = vx[i2], cz = vz[i2];
                double l = Math.Max(Math.Max(Hyp(ax - bx, az - bz), Hyp(bx - cx, bz - cz)), Hyp(cx - ax, cz - az));
                int n = (int)Math.Ceiling(l / (cell * 0.5)); if (n < 1) n = 1; if (n > 40) n = 40;
                for (int i = 0; i <= n; i++)
                    for (int j = 0; j <= n - i; j++)
                    {
                        double u = (double)i / n, v = (double)j / n, w = 1.0 - u - v;
                        int gx = (int)((ax * u + bx * v + cx * w - minX) / cell);
                        int gz = (int)((az * u + bz * v + cz * w - minZ) / cell);
                        grid[gz * W + gx] = true;
                    }
            }

            // ---------- 2. fechamento morfologico (disco) ----------
            progress("Fechamento morfologico", 0.25f);
            int r = (int)Math.Round(p.CloseRadius / cell);
            var disk = new List<int[]>();
            for (int dy = -r; dy <= r; dy++)
            {
                int hw = (int)Math.Floor(Math.Sqrt(r * r - dy * dy));
                for (int dx = -hw; dx <= hw; dx++) disk.Add(new[] { dx, dy });
            }
            var dil = new bool[W * H];
            var dilList = new List<int>();
            for (int idx = 0; idx < grid.Length; idx++)
            {
                if (!grid[idx]) continue;
                int x = idx % W, y = idx / W;
                foreach (var o in disk)
                {
                    int nx = x + o[0], ny = y + o[1];
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    int k = ny * W + nx;
                    if (!dil[k]) { dil[k] = true; dilList.Add(k); }
                }
            }
            var closed = new bool[W * H];
            foreach (int k in dilList)
            {
                int x = k % W, y = k / W;
                bool all = true;
                foreach (var o in disk)
                {
                    int nx = x + o[0], ny = y + o[1];
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H || !dil[ny * W + nx]) { all = false; break; }
                }
                if (all) closed[k] = true;
            }

            // ---------- 3. esqueleto ----------
            progress("Esqueletizando", 0.45f);
            Skeletonize(closed, W, H);
            RemoveRedundant(closed, W, H);

            // ---------- 4. cadeias ----------
            progress("Tracando cadeias", 0.65f);
            var chains = TraceChains(closed, W, H, (int)(p.SpurLength / cell), p.MinChainCells);

            // ---------- 5. simplificacao ----------
            progress("Simplificando", 0.75f);
            double tol = p.Tolerance;
            List<double[]> segs = null;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                segs = new List<double[]>();
                foreach (var ch in chains)
                {
                    var pts = new List<double[]>(ch.Count);
                    foreach (int k in ch) pts.Add(new[] { (k % W + 0.5) * cell + minX, (k / W + 0.5) * cell + minZ });
                    foreach (var s in Simplify(pts, tol, ch.Count > 2 && ch[0] == ch[ch.Count - 1]))
                        if (Hyp(s[2] - s[0], s[3] - s[1]) >= p.MinSegmentLength) segs.Add(s);
                }
                if (segs.Count <= p.MaxSegments) break;
                tol *= 1.25;
            }
            sb.AppendLine("triangulos=" + triCount + " vertices=" + nv + " grade=" + W + "x" + H +
                          " cadeias=" + chains.Count + " tolerancia_final=" + tol.ToString("0.00") + "m segmentos=" + segs.Count);

            // ---------- 6. boxes (altura via vertices do trecho) ----------
            progress("Montando boxes", 0.9f);
            var boxes = new List<RailBoxSpec>();
            foreach (var s in segs)
            {
                double dx = s[2] - s[0], dz = s[3] - s[1], L = Hyp(dx, dz);
                double ux = dx / L, uz = dz / L, nx2 = -uz, nz2 = ux;
                double y0 = double.MaxValue, y1 = double.MinValue;
                for (int i = 0; i < nv; i++)
                {
                    double rx = vx[i] - s[0], rz = vz[i] - s[1];
                    double al = rx * ux + rz * uz, pe = rx * nx2 + rz * nz2;
                    if (al > -p.StripHalfWidth && al < L + p.StripHalfWidth && Math.Abs(pe) < p.StripHalfWidth)
                    { if (vy[i] < y0) y0 = vy[i]; if (vy[i] > y1) y1 = vy[i]; }
                }
                if (y0 > y1) { y0 = y1 = 0; for (int i = 0; i < nv; i++) y0 += vy[i]; y0 /= nv; y1 = y0; }
                double bottom = y0 - p.BelowRail;
                double h = Math.Max(p.MinHeight, (y1 - y0) + p.ExtraAboveRail + p.BelowRail);
                boxes.Add(new RailBoxSpec
                {
                    cx = (s[0] + s[2]) / 2, cz = (s[1] + s[3]) / 2, cy = bottom + h / 2,
                    sx = p.Thickness, sy = h, sz = L + p.Thickness,
                    yaw = Math.Atan2(ux, uz) * 180.0 / Math.PI,
                    ax = s[0], az = s[1], bx = s[2], bz = s[3]
                });
            }
            report = sb.ToString();
            return boxes;
        }

        static double Hyp(double a, double b) { return Math.Sqrt(a * a + b * b); }

        // Zhang-Suen (in-place). true = pixel ligado.
        static void Skeletonize(bool[] g, int W, int H)
        {
            var cand = new List<int>();
            for (int i = 0; i < g.Length; i++) if (g[i]) cand.Add(i);
            var del = new List<int>();
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int pass = 0; pass < 2; pass++)
                {
                    del.Clear();
                    foreach (int k in cand)
                    {
                        if (!g[k]) continue;
                        int x = k % W, y = k / W;
                        if (x < 1 || y < 1 || x >= W - 1 || y >= H - 1) { del.Add(k); continue; }
                        bool p2 = g[k - W], p3 = g[k - W + 1], p4 = g[k + 1], p5 = g[k + W + 1],
                             p6 = g[k + W], p7 = g[k + W - 1], p8 = g[k - 1], p9 = g[k - W - 1];
                        int B = (p2 ? 1 : 0) + (p3 ? 1 : 0) + (p4 ? 1 : 0) + (p5 ? 1 : 0) + (p6 ? 1 : 0) + (p7 ? 1 : 0) + (p8 ? 1 : 0) + (p9 ? 1 : 0);
                        if (B < 2 || B > 6) continue;
                        int A = (!p2 && p3 ? 1 : 0) + (!p3 && p4 ? 1 : 0) + (!p4 && p5 ? 1 : 0) + (!p5 && p6 ? 1 : 0) +
                                (!p6 && p7 ? 1 : 0) + (!p7 && p8 ? 1 : 0) + (!p8 && p9 ? 1 : 0) + (!p9 && p2 ? 1 : 0);
                        if (A != 1) continue;
                        if (pass == 0) { if (p2 && p4 && p6) continue; if (p4 && p6 && p8) continue; }
                        else { if (p2 && p4 && p8) continue; if (p2 && p6 && p8) continue; }
                        del.Add(k);
                    }
                    if (del.Count > 0) changed = true;
                    foreach (int k in del) g[k] = false;
                }
                cand.RemoveAll(k => !g[k]);
            }
        }

        // Remove pixels "de quina" redundantes (escadinhas do Zhang-Suen) sem quebrar a conectividade-8:
        // so remove se todos os vizinhos continuam num unico componente 8-conexo entre si.
        static void RemoveRedundant(bool[] g, int W, int H)
        {
            var cand = new List<int>();
            for (int i = 0; i < g.Length; i++) if (g[i]) cand.Add(i);
            var nx = new int[8]; var ny = new int[8]; var par = new int[8];
            bool changed = true; int guard = 0;
            while (changed && guard++ < 20)
            {
                changed = false;
                foreach (int k in cand)
                {
                    if (!g[k]) continue;
                    int x = k % W, y = k / W;
                    if (x < 1 || y < 1 || x >= W - 1 || y >= H - 1) continue;
                    int n = 0;
                    for (int d = 0; d < 8; d++) if (g[(y + DY[d]) * W + (x + DX[d])]) { nx[n] = DX[d]; ny[n] = DY[d]; par[n] = n; n++; }
                    if (n < 2) continue;
                    int comps = n;
                    for (int a = 0; a < n; a++)
                        for (int b = a + 1; b < n; b++)
                            if (Math.Abs(nx[a] - nx[b]) <= 1 && Math.Abs(ny[a] - ny[b]) <= 1)
                            {
                                int ra = Find(par, a), rb = Find(par, b);
                                if (ra != rb) { par[ra] = rb; comps--; }
                            }
                    if (comps == 1) { g[k] = false; changed = true; }
                }
            }
        }
        static int Find(int[] p, int i) { while (p[i] != i) { p[i] = p[p[i]]; i = p[i]; } return i; }

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        static List<List<int>> TraceChains(bool[] g, int W, int H, int spurCells, int minChain)
        {
            var pts = new HashSet<int>();
            for (int i = 0; i < g.Length; i++) if (g[i]) pts.Add(i);
            Func<int, List<int>> nb = k =>
            {
                var l = new List<int>(8); int x = k % W, y = k / W;
                for (int d = 0; d < 8; d++) { int q = (y + DY[d]) * W + (x + DX[d]); if (pts.Contains(q)) l.Add(q); }
                return l;
            };
            // poda de esporoes (pontas ate uma juncao, mais curtas que spurCells)
            bool changed = true; int guard = 0;
            while (changed && guard++ < 10)
            {
                changed = false;
                var ends = new List<int>();
                foreach (int k in pts) if (nb(k).Count == 1) ends.Add(k);
                foreach (int e in ends)
                {
                    if (!pts.Contains(e)) continue;
                    var path = new List<int> { e }; int prev = -1, cur = e; bool junction = false;
                    while (path.Count <= spurCells + 1)
                    {
                        var ns = nb(cur); ns.RemoveAll(q => q == prev || path.Contains(q));
                        if (ns.Count == 0) break;
                        if (ns.Count > 1) { junction = true; break; }
                        prev = cur; cur = ns[0];
                        if (nb(cur).Count > 2) { junction = true; break; }
                        path.Add(cur);
                    }
                    if (junction && path.Count < spurCells) { foreach (int q in path) pts.Remove(q); changed = true; }
                }
            }
            var nodes = new HashSet<int>();
            foreach (int k in pts) if (nb(k).Count != 2) nodes.Add(k);
            var visited = new HashSet<int>();
            var edgeSeen = new HashSet<long>();
            var chains = new List<List<int>>();
            foreach (int nd in nodes)
                foreach (int q0 in nb(nd))
                {
                    if (edgeSeen.Contains((long)nd * 100000000L + q0)) continue;
                    var ch = new List<int> { nd, q0 }; int prev = nd, cur = q0;
                    while (!nodes.Contains(cur))
                    {
                        var ns = nb(cur); ns.RemoveAll(q => q == prev);
                        if (ns.Count == 0) break;
                        prev = cur; cur = ns[0]; ch.Add(cur);
                        if (cur == nd) break;
                    }
                    edgeSeen.Add((long)ch[0] * 100000000L + ch[1]);
                    edgeSeen.Add((long)ch[ch.Count - 1] * 100000000L + ch[ch.Count - 2]);
                    foreach (int k in ch) visited.Add(k);
                    chains.Add(ch);
                }
            // lacos fechados (sem no)
            foreach (int s in new List<int>(pts))
            {
                if (visited.Contains(s)) continue;
                var ch = new List<int> { s }; int prev = -1, cur = s; visited.Add(s);
                while (true)
                {
                    var ns = nb(cur); ns.RemoveAll(q => q == prev || (visited.Contains(q) && q != s));
                    if (ns.Count == 0) break;
                    int nxt = ns[0];
                    if (nxt == s) { if (ch.Count > 2) ch.Add(s); break; }
                    prev = cur; cur = nxt; ch.Add(cur); visited.Add(cur);
                }
                chains.Add(ch);
            }
            chains.RemoveAll(c => c.Count < minChain);
            return chains;
        }

        // Douglas-Peucker iterativo. Retorna segmentos {ax,az,bx,bz}. Lacos fechados sao tratados como cadeia aberta.
        static List<double[]> Simplify(List<double[]> pts, double eps, bool closedLoop)
        {
            var res = new List<double[]>();
            int n = pts.Count; if (n < 2) return res;
            var keep = new bool[n]; keep[0] = keep[n - 1] = true;
            var stack = new Stack<int[]>(); stack.Push(new[] { 0, n - 1 });
            while (stack.Count > 0)
            {
                var rg = stack.Pop(); int a = rg[0], b = rg[1]; if (b <= a + 1) continue;
                double ax = pts[a][0], az = pts[a][1], bx = pts[b][0], bz = pts[b][1];
                double dx = bx - ax, dz = bz - az, l = Hyp(dx, dz), best = -1; int bi = -1;
                for (int i = a + 1; i < b; i++)
                {
                    double d = l < 1e-9 ? Hyp(pts[i][0] - ax, pts[i][1] - az)
                                        : Math.Abs(dx * (pts[i][1] - az) - dz * (pts[i][0] - ax)) / l;
                    if (d > best) { best = d; bi = i; }
                }
                if (best > eps) { keep[bi] = true; stack.Push(new[] { a, bi }); stack.Push(new[] { bi, b }); }
            }
            int last = -1;
            for (int i = 0; i < n; i++)
            {
                if (!keep[i]) continue;
                if (last >= 0) res.Add(new[] { pts[last][0], pts[last][1], pts[i][0], pts[i][1] });
                last = i;
            }
            return res;
        }
    }
}

#if UNITY_EDITOR
namespace Emuseu.EditorTools
{
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    public static class RailsCollidersGenerator
    {
        const string RailsName = "Rails";
        const string CollidersName = "Rails_Colliders";

        [MenuItem("Tools/Emuseu/Gerar Colliders do Rail")]
        static void Gerar()
        {
            var rails = FindRails();
            if (rails == null) return;
            var mf = rails.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Erro("O GameObject 'Rails' nao tem MeshFilter/mesh."); return; }
            if (Application.isPlaying) { Erro("Saia do Play Mode antes de gerar os colliders."); return; }

            var parent = rails.transform.parent;
            var existente = FindSibling(rails.transform, CollidersName);
            if (existente != null)
            {
                if (!EditorUtility.DisplayDialog("Rails_Colliders", "Ja existe '" + CollidersName + "'. Substituir?", "Substituir", "Cancelar")) return;
            }

            try
            {
                // ---- le o mesh em mundo ----
                var mesh = mf.sharedMesh;
                var lv = mesh.vertices; var tr = mesh.triangles;
                var m = rails.transform.localToWorldMatrix;
                var vx = new float[lv.Length]; var vy = new float[lv.Length]; var vz = new float[lv.Length];
                for (int i = 0; i < lv.Length; i++) { var w = m.MultiplyPoint3x4(lv[i]); vx[i] = w.x; vy[i] = w.y; vz[i] = w.z; }

                string report;
                var boxes = RailsOutlineCore.Compute(vx, vy, vz, tr, new RailOutlineParams(), out report,
                    (s, f) => EditorUtility.DisplayProgressBar("Colliders do Rail", s, f));
                EditorUtility.ClearProgressBar();
                if (boxes.Count == 0) { Erro("Nenhum segmento foi gerado.\n" + report); return; }

                // ---- cria os objetos (Undo) ----
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Gerar Colliders do Rail");
                int group = Undo.GetCurrentGroup();
                if (existente != null) Undo.DestroyObjectImmediate(existente.gameObject);

                var root = new GameObject(CollidersName);
                Undo.RegisterCreatedObjectUndo(root, "Criar " + CollidersName);
                if (parent != null) root.transform.SetParent(parent, false);
                root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity; root.transform.localScale = Vector3.one;
                root.layer = rails.gameObject.layer;
                try { root.tag = rails.gameObject.tag; } catch { }
                GameObjectUtility.SetStaticEditorFlags(root, GameObjectUtility.GetStaticEditorFlags(rails.gameObject));
                root.transform.SetSiblingIndex(rails.transform.GetSiblingIndex() + 1);

                for (int i = 0; i < boxes.Count; i++)
                {
                    var b = boxes[i];
                    var go = new GameObject("Rail_Box_" + i.ToString("000"));
                    go.layer = rails.gameObject.layer;
                    GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(rails.gameObject));
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(new Vector3((float)b.cx, (float)b.cy, (float)b.cz), Quaternion.Euler(0f, (float)b.yaw, 0f));
                    var bc = go.AddComponent<BoxCollider>();
                    var ls = go.transform.lossyScale;   // compensa a escala dos pais (ex.: Envirovment 0.6)
                    bc.center = Vector3.zero;
                    bc.size = new Vector3((float)b.sx / ls.x, (float)b.sy / ls.y, (float)b.sz / ls.z);
                    Undo.RegisterCreatedObjectUndo(go, "Criar box");
                }

                // ---- desabilita (nao remove) o MeshCollider ----
                var mc = rails.GetComponent<MeshCollider>();
                if (mc != null)
                {
                    Undo.RecordObject(mc, "Desabilitar MeshCollider do Rails");
                    mc.enabled = false;
                    EditorUtility.SetDirty(mc);
                    if (PrefabUtility.IsPartOfPrefabInstance(mc)) PrefabUtility.RecordPrefabInstancePropertyModifications(mc);
                }
                else Debug.LogWarning("[RailsColliders] 'Rails' nao tem MeshCollider (nada a desabilitar).");

                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(rails.gameObject.scene);
                Selection.activeGameObject = root;
                Debug.Log("[RailsColliders] " + boxes.Count + " BoxColliders criados em '" + CollidersName + "'. " + report +
                          (mc != null ? " MeshCollider do Rails desabilitado." : ""));
                EditorUtility.DisplayDialog("Colliders do Rail", boxes.Count + " BoxColliders criados.\nMeshCollider do Rails desabilitado.\nSalve a cena (Ctrl+S).", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("[RailsColliders] falhou: " + e);
            }
        }

        [MenuItem("Tools/Emuseu/Remover Colliders do Rail")]
        static void Remover()
        {
            var rails = FindRails();
            if (rails == null) return;
            var existente = FindSibling(rails.transform, CollidersName);
            var mc = rails.GetComponent<MeshCollider>();
            if (existente == null && (mc == null || mc.enabled)) { Erro("Nada para remover: nao existe '" + CollidersName + "' e o MeshCollider ja esta habilitado."); return; }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Remover Colliders do Rail");
            int group = Undo.GetCurrentGroup();
            if (existente != null) Undo.DestroyObjectImmediate(existente.gameObject);
            if (mc != null)
            {
                Undo.RecordObject(mc, "Reabilitar MeshCollider do Rails");
                mc.enabled = true;
                EditorUtility.SetDirty(mc);
                if (PrefabUtility.IsPartOfPrefabInstance(mc)) PrefabUtility.RecordPrefabInstancePropertyModifications(mc);
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(rails.gameObject.scene);
            Debug.Log("[RailsColliders] '" + CollidersName + "' removido e MeshCollider do Rails reabilitado.");
        }

        static GameObject FindRails()
        {
            GameObject best = null;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.name != RailsName) continue;
                var go = t.gameObject;
                if (EditorUtility.IsPersistent(go) || !go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (go.GetComponent<MeshFilter>() == null) continue;
                bool underCristo = t.parent != null && t.parent.name == "CristoBase";
                if (best == null || underCristo) best = go;
                if (underCristo) break;
            }
            if (best == null) Erro("Nao encontrei o GameObject 'Rails' na cena aberta (abra CenaRiodeJaneiro).");
            return best;
        }

        static Transform FindSibling(Transform of, string name)
        {
            var parent = of.parent;
            if (parent != null) return parent.Find(name);
            foreach (var go in of.gameObject.scene.GetRootGameObjects()) if (go.name == name) return go.transform;
            return null;
        }

        static void Erro(string msg) { Debug.LogWarning("[RailsColliders] " + msg); EditorUtility.DisplayDialog("Colliders do Rail", msg, "OK"); }
    }
}
#endif
