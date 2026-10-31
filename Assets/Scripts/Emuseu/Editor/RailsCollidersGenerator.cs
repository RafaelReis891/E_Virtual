// Gera BoxColliders finos (outline simplificado do mesh) para a SELECAO atual, no lugar do MeshCollider completo.
// Antes era fixo no GameObject "Rails"; agora funciona com qualquer GameObject selecionado que tenha mesh.
//
// Menus:  Tools > Emuseu > Gerar Colliders na Selecao      (tambem: GameObject/Emuseu/... e botao direito na Hierarchy)
//         Tools > Emuseu > Remover Colliders da Selecao
//         Tools > Emuseu > Colliders Config                 (parametros, salvos em EditorPrefs; arquivo CollidersConfigWindow.cs)
//
// Como funciona (tudo calculado no Editor, em espaco de MUNDO), para cada GameObject selecionado:
//  1. Le os vertices/triangulos do MeshFilter (e dos MeshFilters filhos, se "Incluir filhos" estiver ligado) e aplica localToWorld.
//  2. Projeta no plano XZ e rasteriza o "footprint" (celulas de 5 cm). Fechamento morfologico (CloseRadius, padrao 0.4 m)
//     funde balaustres/corrimaos numa faixa continua; vaos maiores que 2x o raio NAO sao fechados.
//  3. Esqueletiza (Zhang-Suen), traca cadeias, poda esporoes e simplifica com Douglas-Peucker (Tolerance, padrao 0.30 m;
//     sobe sozinha se passar de MaxSegments).
//  4. Cada segmento vira um BoxCollider fino (Thickness) e alto (>= MinHeight). Altura vem dos vertices do trecho.
//     Os boxes ficam em filhos de um GameObject IRMAO do selecionado chamado "<Nome>_Colliders" (mesma layer/static flags).
//  5. O MeshCollider do selecionado (se houver) e DESABILITADO (nao removido). Tudo com Undo; cena marcada como suja.
//  Remover: apaga "<Nome>_Colliders" (selecionando o objeto OU o proprio _Colliders) e reabilita o MeshCollider.
//  Nao funciona em Play Mode.
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
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    public static class SelectionCollidersGenerator
    {
        public const string Suffix = "_Colliders";
        const string Titulo = "Colliders na Selecao";
        const long MaxCells = 80000000; // ~80 M celulas (grade de 5 cm): acima disso recusa por memoria

        // ------------------------------------------------------------ menus
        [MenuItem("Tools/Emuseu/Gerar Colliders na Selecao", false, 100)]
        static void MenuGerar() { GerarNaSelecao(); }
        [MenuItem("Tools/Emuseu/Gerar Colliders na Selecao", true)]
        static bool MenuGerarValidate() { return PodeGerar(); }

        [MenuItem("GameObject/Emuseu/Gerar Colliders na Selecao", false, 20)]
        static void CtxGerar() { GerarNaSelecao(); }
        [MenuItem("GameObject/Emuseu/Gerar Colliders na Selecao", true)]
        static bool CtxGerarValidate() { return PodeGerar(); }

        [MenuItem("Tools/Emuseu/Remover Colliders da Selecao", false, 101)]
        static void MenuRemover() { RemoverDaSelecao(); }
        [MenuItem("Tools/Emuseu/Remover Colliders da Selecao", true)]
        static bool MenuRemoverValidate() { return PodeRemover(); }

        [MenuItem("GameObject/Emuseu/Remover Colliders da Selecao", false, 21)]
        static void CtxRemover() { RemoverDaSelecao(); }
        [MenuItem("GameObject/Emuseu/Remover Colliders da Selecao", true)]
        static bool CtxRemoverValidate() { return PodeRemover(); }

        [MenuItem("Tools/Emuseu/Colliders Config", false, 102)]
        static void MenuConfig() { CollidersConfigWindow.Abrir(); }

        // ------------------------------------------------------------ selecao
        static Transform[] SelecaoBase()
        {
            var mode = SelectionMode.Editable | SelectionMode.ExcludePrefab;
            if (CollidersSettings.IncludeChildren) mode |= SelectionMode.TopLevel; // evita duplicar pai+filho
            return Selection.GetFiltered<Transform>(mode)
                .Where(t => t != null && t.gameObject.scene.IsValid() && !t.name.EndsWith(Suffix)).ToArray();
        }

        static List<MeshFilter> Filtros(Transform t)
        {
            var arr = CollidersSettings.IncludeChildren ? t.GetComponentsInChildren<MeshFilter>(false) : t.GetComponents<MeshFilter>();
            var l = new List<MeshFilter>();
            foreach (var mf in arr) if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0) l.Add(mf);
            return l;
        }

        static Transform[] Alvos() { return SelecaoBase().Where(t => Filtros(t).Count > 0).ToArray(); }

        public static bool PodeGerar() { return !Application.isPlaying && Alvos().Length > 0; }

        // ------------------------------------------------------------ gerar
        public static void GerarNaSelecao()
        {
            if (Application.isPlaying) { Erro("Saia do Play Mode antes de gerar os colliders."); return; }
            var alvos = Alvos();
            if (alvos.Length == 0) { Erro("Selecione ao menos um GameObject com MeshFilter (ou com filhos com mesh, se 'Incluir filhos' estiver ligado em Tools > Emuseu > Colliders Config)."); return; }

            var existentes = alvos.Select(t => FindSibling(t, t.name + Suffix)).Where(x => x != null).ToList();
            if (existentes.Count > 0)
            {
                string nomes = string.Join(", ", existentes.Select(x => "'" + x.name + "'").ToArray());
                if (!EditorUtility.DisplayDialog(Titulo, "Ja existe(m): " + nomes + ". Substituir?", "Substituir", "Cancelar")) return;
            }

            var prm = CollidersSettings.ToParams();
            var criados = new List<Object>();
            int totalBoxes = 0, desabilitados = 0;
            var relatorio = new System.Text.StringBuilder();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Gerar Colliders na Selecao");
            int group = Undo.GetCurrentGroup();
            try
            {
                for (int n = 0; n < alvos.Length; n++)
                {
                    var t = alvos[n];
                    string pfx = alvos.Length > 1 ? "(" + (n + 1) + "/" + alvos.Length + ") " + t.name + ": " : t.name + ": ";

                    // ---- junta os meshes em mundo ----
                    var vxl = new List<float>(); var vyl = new List<float>(); var vzl = new List<float>(); var tris = new List<int>();
                    foreach (var mf in Filtros(t))
                    {
                        var mesh = mf.sharedMesh;
                        Vector3[] lv; int[] tr;
                        try { lv = mesh.vertices; tr = mesh.triangles; }
                        catch (System.Exception e) { Debug.LogWarning("[Colliders] mesh ilegivel em '" + mf.name + "': " + e.Message); continue; }
                        var m = mf.transform.localToWorldMatrix; int off = vxl.Count;
                        for (int i = 0; i < lv.Length; i++) { var w = m.MultiplyPoint3x4(lv[i]); vxl.Add(w.x); vyl.Add(w.y); vzl.Add(w.z); }
                        for (int i = 0; i < tr.Length; i++) tris.Add(tr[i] + off);
                    }
                    if (vxl.Count == 0 || tris.Count < 3) { Debug.LogWarning("[Colliders] " + pfx + "sem triangulos, ignorado."); continue; }
                    var vx = vxl.ToArray(); var vy = vyl.ToArray(); var vz = vzl.ToArray();

                    // ---- guarda de memoria ----
                    float minX = vx.Min(), maxX = vx.Max(), minZ = vz.Min(), maxZ = vz.Max();
                    double mg = 2 * (prm.CloseRadius + 1.0);
                    long cells = (long)((maxX - minX + mg) / prm.Cell) * (long)((maxZ - minZ + mg) / prm.Cell);
                    if (cells > MaxCells) { Debug.LogWarning("[Colliders] " + pfx + "area grande demais (" + (maxX - minX).ToString("0") + "x" + (maxZ - minZ).ToString("0") + " m). Ignorado."); continue; }

                    string rep;
                    var boxes = RailsOutlineCore.Compute(vx, vy, vz, tris.ToArray(), prm, out rep,
                        (s, f) => EditorUtility.DisplayProgressBar(Titulo + " - " + t.name, s, f));
                    EditorUtility.ClearProgressBar();
                    if (boxes.Count == 0) { Debug.LogWarning("[Colliders] " + pfx + "nenhum segmento gerado. " + rep); continue; }

                    // ---- cria objetos ----
                    var ex = FindSibling(t, t.name + Suffix);
                    int sibIndex = t.GetSiblingIndex() + 1;
                    if (ex != null) Undo.DestroyObjectImmediate(ex.gameObject);

                    var go0 = t.gameObject;
                    var flags = GameObjectUtility.GetStaticEditorFlags(go0);
                    var root = new GameObject(t.name + Suffix);
                    Undo.RegisterCreatedObjectUndo(root, "Criar " + root.name);
                    if (t.parent != null) root.transform.SetParent(t.parent, false);
                    root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity; root.transform.localScale = Vector3.one;
                    root.layer = go0.layer;
                    try { root.tag = go0.tag; } catch { }
                    GameObjectUtility.SetStaticEditorFlags(root, flags);
                    root.transform.SetSiblingIndex(sibIndex);

                    for (int i = 0; i < boxes.Count; i++)
                    {
                        var b = boxes[i];
                        var go = new GameObject(t.name + "_Box_" + i.ToString("000"));
                        go.layer = go0.layer;
                        GameObjectUtility.SetStaticEditorFlags(go, flags);
                        go.transform.SetParent(root.transform, false);
                        go.transform.SetPositionAndRotation(new Vector3((float)b.cx, (float)b.cy, (float)b.cz), Quaternion.Euler(0f, (float)b.yaw, 0f));
                        var bc = go.AddComponent<BoxCollider>();
                        var ls = go.transform.lossyScale;   // compensa a escala dos pais
                        bc.center = Vector3.zero;
                        bc.size = new Vector3((float)b.sx / ls.x, (float)b.sy / ls.y, (float)b.sz / ls.z);
                        Undo.RegisterCreatedObjectUndo(go, "Criar box");
                    }
                    criados.Add(root);
                    totalBoxes += boxes.Count;

                    // ---- desabilita (nao remove) o MeshCollider do proprio objeto ----
                    var mc = t.GetComponent<MeshCollider>();
                    if (mc != null)
                    {
                        Undo.RecordObject(mc, "Desabilitar MeshCollider");
                        mc.enabled = false;
                        EditorUtility.SetDirty(mc);
                        if (PrefabUtility.IsPartOfPrefabInstance(mc)) PrefabUtility.RecordPrefabInstancePropertyModifications(mc);
                        desabilitados++;
                    }
                    else Debug.LogWarning("[Colliders] " + pfx + "sem MeshCollider proprio (nada a desabilitar).");

                    EditorSceneManager.MarkSceneDirty(go0.scene);
                    relatorio.AppendLine(pfx + boxes.Count + " boxes. " + rep);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Colliders] falhou: " + e);
            }
            finally { EditorUtility.ClearProgressBar(); }

            Undo.CollapseUndoOperations(group);
            if (criados.Count == 0) { Erro("Nenhum collider foi gerado. Veja o Console."); return; }
            Selection.objects = criados.ToArray();
            Debug.Log("[Colliders] " + totalBoxes + " BoxColliders em " + criados.Count + " objeto(s); " + desabilitados + " MeshCollider(s) desabilitado(s).\n" + relatorio);
            EditorUtility.DisplayDialog(Titulo, totalBoxes + " BoxColliders criados em " + criados.Count + " objeto(s).\n" +
                desabilitados + " MeshCollider(s) desabilitado(s).\nSalve a cena (Ctrl+S).", "OK");
        }

        // ------------------------------------------------------------ remover
        // Para cada selecionado: acha (colliders, dono). Selecionar o proprio '<Nome>_Colliders' tambem vale.
        static List<KeyValuePair<Transform, Transform>> AlvosRemover()
        {
            var res = new List<KeyValuePair<Transform, Transform>>();
            var vistos = new HashSet<Transform>();
            foreach (var t in Selection.GetFiltered<Transform>(SelectionMode.Editable | SelectionMode.ExcludePrefab))
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                Transform cols = null, dono = null;
                if (t.name.EndsWith(Suffix) && t.name.Length > Suffix.Length)
                {
                    cols = t; dono = FindSibling(t, t.name.Substring(0, t.name.Length - Suffix.Length));
                }
                else
                {
                    dono = t; cols = FindSibling(t, t.name + Suffix);
                }
                var mc = dono != null ? dono.GetComponent<MeshCollider>() : null;
                bool reabilita = mc != null && !mc.enabled;
                if (cols == null && !reabilita) continue;
                Transform key = cols != null ? cols : dono;
                if (!vistos.Add(key)) continue;
                res.Add(new KeyValuePair<Transform, Transform>(cols, dono));
            }
            return res;
        }

        public static bool PodeRemover() { return !Application.isPlaying && AlvosRemover().Count > 0; }

        public static void RemoverDaSelecao()
        {
            if (Application.isPlaying) { Erro("Saia do Play Mode antes de remover os colliders."); return; }
            var alvos = AlvosRemover();
            if (alvos.Count == 0) { Erro("Nada para remover na selecao: nao existe '<Nome>_Colliders' nem MeshCollider desabilitado."); return; }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Remover Colliders da Selecao");
            int group = Undo.GetCurrentGroup();
            int removidos = 0, reabilitados = 0;
            var donos = new List<Object>();
            foreach (var kv in alvos)
            {
                var cols = kv.Key; var dono = kv.Value;
                var scene = (cols != null ? cols.gameObject : dono.gameObject).scene;
                if (cols != null) { Undo.DestroyObjectImmediate(cols.gameObject); removidos++; }
                if (dono != null)
                {
                    var mc = dono.GetComponent<MeshCollider>();
                    if (mc != null && !mc.enabled)
                    {
                        Undo.RecordObject(mc, "Reabilitar MeshCollider");
                        mc.enabled = true;
                        EditorUtility.SetDirty(mc);
                        if (PrefabUtility.IsPartOfPrefabInstance(mc)) PrefabUtility.RecordPrefabInstancePropertyModifications(mc);
                        reabilitados++;
                    }
                    donos.Add(dono.gameObject);
                }
                EditorSceneManager.MarkSceneDirty(scene);
            }
            Undo.CollapseUndoOperations(group);
            if (donos.Count > 0) Selection.objects = donos.ToArray();
            Debug.Log("[Colliders] " + removidos + " grupo(s) '_Colliders' removido(s); " + reabilitados + " MeshCollider(s) reabilitado(s).");
        }

        // ------------------------------------------------------------ util
        static Transform FindSibling(Transform of, string name)
        {
            var parent = of.parent;
            if (parent != null) return parent.Find(name);
            foreach (var go in of.gameObject.scene.GetRootGameObjects()) if (go.name == name) return go.transform;
            return null;
        }

        static void Erro(string msg) { Debug.LogWarning("[Colliders] " + msg); EditorUtility.DisplayDialog(Titulo, msg, "OK"); }
    }
}
#endif
