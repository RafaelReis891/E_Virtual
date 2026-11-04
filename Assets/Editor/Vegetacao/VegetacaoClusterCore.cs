using System;
using System.Collections.Generic;

namespace Vegetacao.EditorTools
{
    /// <summary>Parametros da extracao de arvores de uma malha mesclada.</summary>
    public sealed class ClusterOptions
    {
        /// <summary>Ilha "fina" (tronco) tem area XZ ate esta fracao da ilha que fica sobre ela (copa/folha).</summary>
        public float ThinRatio = 0.5f;
        /// <summary>Tolerancia vertical (m) entre o topo do tronco e a base da copa.</summary>
        public float YTol = 1.0f;
        /// <summary>Distancia XZ maxima (m) entre o centro de uma copa/folha e o tronco ao qual ela pertence.</summary>
        public float MaxAssignDist = 15f;
        /// <summary>Faixa (m) acima do ponto mais baixo usada para achar o centro da base.</summary>
        public float BottomBand = 0.1f;
        /// <summary>Precisao (m) para soldar vertices coincidentes.</summary>
        public float WeldPrecision = 1e-4f;
        /// <summary>Diferenca minima (m) de altura de base para uma ilha ser considerada "abaixo" de outra.</summary>
        public float BelowEps = 0.05f;
    }

    /// <summary>Uma arvore/planta encontrada (ou lida de CSV). Posicao = centro da base.</summary>
    public sealed class TreeRecord
    {
        public float X, Y, Z;
        public float Height, CrownW, CrownD;
        public int TriCount, IslandCount;
        public string Tipo;
    }

    /// <summary>
    /// Algoritmo puro (sem UnityEngine): agrupa triangulos em ilhas (vertices soldados), identifica o
    /// "tronco" (ilha sem nenhuma ilha mais fina embaixo) e anexa copas/folhas ao tronco mais proximo.
    /// </summary>
    public static class VegetacaoClusterCore
    {
        static int Find(int[] p, int x)
        {
            while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; }
            return x;
        }

        static long Key(int a, int b) { return ((long)a << 32) | (uint)b; }

        public static List<TreeRecord> Cluster(float[] pos, int[] tri, ClusterOptions o, out int[] triTree, out int orphans)
        {
            int V = pos.Length / 3, T = tri.Length / 3;
            orphans = 0;
            triTree = new int[T];
            var result = new List<TreeRecord>();
            if (T == 0) return result;

            // 1) soldar vertices
            var weld = new int[V];
            var dict = new Dictionary<(int, int, int), int>(V);
            double inv = 1.0 / o.WeldPrecision;
            for (int i = 0; i < V; i++)
            {
                var k = ((int)Math.Round(pos[3 * i] * inv), (int)Math.Round(pos[3 * i + 1] * inv), (int)Math.Round(pos[3 * i + 2] * inv));
                int id;
                if (!dict.TryGetValue(k, out id)) { id = dict.Count; dict[k] = id; }
                weld[i] = id;
            }
            int W = dict.Count;
            var par = new int[W];
            for (int i = 0; i < W; i++) par[i] = i;
            for (int t = 0; t < T; t++)
            {
                int a = Find(par, weld[tri[3 * t]]);
                int b = Find(par, weld[tri[3 * t + 1]]);
                int c = Find(par, weld[tri[3 * t + 2]]);
                if (a != b) par[b] = a;
                a = Find(par, a);
                if (c != a) par[c] = a;
            }

            // 2) ilhas
            var islandOfRoot = new int[W];
            for (int i = 0; i < W; i++) islandOfRoot[i] = -1;
            var triIsland = new int[T];
            int N = 0;
            for (int t = 0; t < T; t++)
            {
                int r = Find(par, weld[tri[3 * t]]);
                if (islandOfRoot[r] < 0) islandOfRoot[r] = N++;
                triIsland[t] = islandOfRoot[r];
            }

            var minX = new float[N]; var minY = new float[N]; var minZ = new float[N];
            var maxX = new float[N]; var maxY = new float[N]; var maxZ = new float[N];
            var sumX = new double[N]; var sumZ = new double[N]; var cnt = new int[N]; var triCnt = new int[N];
            for (int i = 0; i < N; i++)
            {
                minX[i] = minY[i] = minZ[i] = float.MaxValue;
                maxX[i] = maxY[i] = maxZ[i] = float.MinValue;
            }
            for (int t = 0; t < T; t++)
            {
                int isl = triIsland[t];
                triCnt[isl]++;
                for (int k = 0; k < 3; k++)
                {
                    int v = tri[3 * t + k];
                    float x = pos[3 * v], y = pos[3 * v + 1], z = pos[3 * v + 2];
                    if (x < minX[isl]) minX[isl] = x; if (x > maxX[isl]) maxX[isl] = x;
                    if (y < minY[isl]) minY[isl] = y; if (y > maxY[isl]) maxY[isl] = y;
                    if (z < minZ[isl]) minZ[isl] = z; if (z > maxZ[isl]) maxZ[isl] = z;
                    sumX[isl] += x; sumZ[isl] += z; cnt[isl]++;
                }
            }
            var cx = new float[N]; var cz = new float[N]; var area = new float[N];
            for (int i = 0; i < N; i++)
            {
                cx[i] = (float)(sumX[i] / cnt[i]);
                cz[i] = (float)(sumZ[i] / cnt[i]);
                area[i] = (maxX[i] - minX[i]) * (maxZ[i] - minZ[i]);
            }

            // 3) grade espacial dos centros das ilhas
            const float cs = 4f;
            var grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < N; i++)
            {
                long key = Key((int)Math.Floor(cx[i] / cs), (int)Math.Floor(cz[i] / cs));
                List<int> l;
                if (!grid.TryGetValue(key, out l)) { l = new List<int>(); grid[key] = l; }
                l.Add(i);
            }

            // 4) ancoras (troncos): nao ha ilha mais fina logo abaixo
            var isAnchor = new bool[N];
            for (int a = 0; a < N; a++)
            {
                isAnchor[a] = true;
                float x0 = minX[a] - 0.05f, x1 = maxX[a] + 0.05f, z0 = minZ[a] - 0.05f, z1 = maxZ[a] + 0.05f;
                int gx0 = (int)Math.Floor(x0 / cs), gx1 = (int)Math.Floor(x1 / cs);
                int gz0 = (int)Math.Floor(z0 / cs), gz1 = (int)Math.Floor(z1 / cs);
                for (int gx = gx0; gx <= gx1 && isAnchor[a]; gx++)
                    for (int gz = gz0; gz <= gz1 && isAnchor[a]; gz++)
                    {
                        List<int> l;
                        if (!grid.TryGetValue(Key(gx, gz), out l)) continue;
                        for (int q = 0; q < l.Count; q++)
                        {
                            int b = l[q];
                            if (b == a) continue;
                            if (cx[b] < x0 || cx[b] > x1 || cz[b] < z0 || cz[b] > z1) continue;
                            if (!(minY[b] < minY[a] - o.BelowEps)) continue;
                            if (maxY[b] < minY[a] - o.YTol) continue;
                            if (area[b] > area[a] * o.ThinRatio) continue;
                            isAnchor[a] = false;
                            break;
                        }
                    }
            }

            // 5) grade de ancoras e atribuicao das demais ilhas
            float acs = Math.Max(1f, o.MaxAssignDist);
            var agrid = new Dictionary<long, List<int>>();
            for (int i = 0; i < N; i++)
            {
                if (!isAnchor[i]) continue;
                long key = Key((int)Math.Floor(cx[i] / acs), (int)Math.Floor(cz[i] / acs));
                List<int> l;
                if (!agrid.TryGetValue(key, out l)) { l = new List<int>(); agrid[key] = l; }
                l.Add(i);
            }
            var anchorOf = new int[N];
            for (int i = 0; i < N; i++) anchorOf[i] = isAnchor[i] ? i : -1;
            float maxD2 = o.MaxAssignDist * o.MaxAssignDist;
            for (int i = 0; i < N; i++)
            {
                if (isAnchor[i]) continue;
                int gx = (int)Math.Floor(cx[i] / acs), gz = (int)Math.Floor(cz[i] / acs);
                int best = -1; float bestD = float.MaxValue;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        List<int> l;
                        if (!agrid.TryGetValue(Key(gx + dx, gz + dz), out l)) continue;
                        for (int q = 0; q < l.Count; q++)
                        {
                            int b = l[q];
                            if (!(minY[b] < minY[i] - o.BelowEps)) continue;
                            float ddx = cx[b] - cx[i], ddz = cz[b] - cz[i];
                            float d2 = ddx * ddx + ddz * ddz;
                            if (d2 < bestD) { bestD = d2; best = b; }
                        }
                    }
                if (best >= 0 && bestD <= maxD2) anchorOf[i] = best;
                else { isAnchor[i] = true; anchorOf[i] = i; orphans++; }
            }

            // 6) centro da base de cada ancora
            var bx0 = new float[N]; var bx1 = new float[N]; var bz0 = new float[N]; var bz1 = new float[N];
            for (int i = 0; i < N; i++) { bx0[i] = bz0[i] = float.MaxValue; bx1[i] = bz1[i] = float.MinValue; }
            for (int t = 0; t < T; t++)
            {
                int isl = triIsland[t];
                if (!isAnchor[isl]) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = tri[3 * t + k];
                    if (pos[3 * v + 1] > minY[isl] + o.BottomBand) continue;
                    float x = pos[3 * v], z = pos[3 * v + 2];
                    if (x < bx0[isl]) bx0[isl] = x; if (x > bx1[isl]) bx1[isl] = x;
                    if (z < bz0[isl]) bz0[isl] = z; if (z > bz1[isl]) bz1[isl] = z;
                }
            }

            // 7) montar arvores
            var treeOfAnchor = new int[N];
            for (int i = 0; i < N; i++) treeOfAnchor[i] = -1;
            for (int i = 0; i < N; i++)
            {
                if (!isAnchor[i]) continue;
                treeOfAnchor[i] = result.Count;
                result.Add(new TreeRecord
                {
                    X = (bx0[i] + bx1[i]) * 0.5f,
                    Y = minY[i],
                    Z = (bz0[i] + bz1[i]) * 0.5f
                });
            }
            int M = result.Count;
            var tx0 = new float[M]; var tx1 = new float[M]; var tz0 = new float[M]; var tz1 = new float[M]; var ty1 = new float[M];
            for (int i = 0; i < M; i++) { tx0[i] = tz0[i] = float.MaxValue; tx1[i] = tz1[i] = ty1[i] = float.MinValue; }
            for (int i = 0; i < N; i++)
            {
                int tr = treeOfAnchor[anchorOf[i]];
                var rec = result[tr];
                rec.IslandCount++;
                rec.TriCount += triCnt[i];
                if (minX[i] < tx0[tr]) tx0[tr] = minX[i]; if (maxX[i] > tx1[tr]) tx1[tr] = maxX[i];
                if (minZ[i] < tz0[tr]) tz0[tr] = minZ[i]; if (maxZ[i] > tz1[tr]) tz1[tr] = maxZ[i];
                if (maxY[i] > ty1[tr]) ty1[tr] = maxY[i];
            }
            for (int i = 0; i < M; i++)
            {
                var rec = result[i];
                rec.Height = ty1[i] - rec.Y;
                rec.CrownW = tx1[i] - tx0[i];
                rec.CrownD = tz1[i] - tz0[i];
            }
            for (int t = 0; t < T; t++) triTree[t] = treeOfAnchor[anchorOf[triIsland[t]]];
            return result;
        }
    }
}
