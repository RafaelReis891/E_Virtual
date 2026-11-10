using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vegetacao.EditorTools
{
    public static class VegetacaoRunner
    {
        sealed class ClassData
        {
            public VegClasse c;
            public int index;
            public List<TreeRecord> recs;
            public List<GameObject> prefabs;
            public VegNativeSize[] nat;
        }

        static string F(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        static ClusterOptions Opt(VegetacaoPreset p, VegClasse c)
        {
            return new ClusterOptions { ThinRatio = p.extrRazaoFino, YTol = p.extrTolY, MaxAssignDist = p.extrDistMax, MinTris = c != null ? Math.Max(0, c.extrMinTriangulos) : 0 };
        }

        static List<TreeRecord> GetRecords(VegetacaoPreset p, VegClasse c, Transform parent, StringBuilder log, out string err)
        {
            err = null;
            if (c.origem == VegOrigem.Csv)
            {
                if (c.csv == null) { err = "CSV nao definido"; return null; }
                return VegCsv.Parse(c.csv.text, c.csvTipo, out err);
            }

            GameObject src = null;
            string srcDesc = null;
            if (!string.IsNullOrEmpty(c.objetoCena))
            {
                var found = VegScene.FindAll(c.objetoCena, parent);
                if (found.Count == 0 && parent != null) found = VegScene.FindAll(c.objetoCena, null);
                if (found.Count > 0) { src = found[0]; srcDesc = "objeto de cena '" + src.name + "'"; }
            }
            if (src == null && c.fbxFonte != null) { src = c.fbxFonte; srcDesc = "asset '" + c.fbxFonte.name + "'"; }
            if (src == null) { err = "Fonte nao encontrada (objeto de cena '" + c.objetoCena + "' / FBX vazio)"; return null; }

            var d = VegMerged.Build(src, out err);
            if (d == null) return null;
            int[] triTree; int orphans;
            var recs = d.Cluster(Opt(p, c), out triTree, out orphans);
            log.AppendLine("  [" + c.nome + "] extraidas " + recs.Count + " de " + srcDesc + (orphans > 0 ? " (AVISO: " + orphans + " ilhas orfas viraram arvores proprias)" : ""));
            return recs;
        }

        static bool Prepare(VegetacaoPreset p, Transform parent, StringBuilder log, out List<ClassData> data, out string err)
        {
            err = null;
            data = new List<ClassData>();
            for (int i = 0; i < p.classes.Count; i++)
            {
                var c = p.classes[i];
                if (c == null || !c.ativa) continue;
                var prefabs = new List<GameObject>();
                foreach (var pf in c.prefabs) if (pf != null) prefabs.Add(pf);
                if (prefabs.Count == 0) { err = "Classe '" + c.nome + "' sem prefabs."; return false; }
                string e;
                var recs = GetRecords(p, c, parent, log, out e);
                if (recs == null) { err = "Classe '" + c.nome + "': " + e; return false; }
                var cd = new ClassData { c = c, index = i, recs = recs, prefabs = prefabs, nat = new VegNativeSize[prefabs.Count] };
                for (int k = 0; k < prefabs.Count; k++) cd.nat[k] = VegPrefabTools.Measure(prefabs[k]);
                data.Add(cd);
            }
            if (data.Count == 0) { err = "Nenhuma classe ativa."; return false; }
            return true;
        }

        static Transform ResolveParent(VegetacaoPreset p, Transform parentOverride)
        {
            if (parentOverride != null) return parentOverride;
            if (string.IsNullOrEmpty(p.paiNome)) return null;
            var go = VegScene.FindExact(p.paiNome);
            return go != null ? go.transform : null;
        }

        /// <summary>Somente conta e estima triangulos; nao altera a cena.</summary>
        public static string Contar(VegetacaoPreset p, Transform parentOverride)
        {
            var log = new StringBuilder();
            Transform parent = ResolveParent(p, parentOverride);
            List<ClassData> data; string err;
            if (!Prepare(p, parent, log, out data, out err)) return "ERRO: " + err;
            long total = 0; int count = 0;
            foreach (var cd in data)
            {
                double avg = 0;
                for (int k = 0; k < cd.prefabs.Count; k++) avg += cd.nat[k].tris;
                avg /= cd.prefabs.Count;
                long t = (long)(avg * cd.recs.Count);
                total += t; count += cd.recs.Count;
                log.AppendLine("  " + cd.c.nome + ": " + cd.recs.Count + " instancias x ~" + (int)avg + " tris = ~" + t);
            }
            log.AppendLine("TOTAL: " + count + " instancias, ~" + total + " triangulos (estimativa).");
            return log.ToString();
        }

        public static string Substituir(VegetacaoPreset p, Transform parentOverride)
        {
            var log = new StringBuilder();
            Transform parent = ResolveParent(p, parentOverride);
            if (parent == null) log.AppendLine("AVISO: objeto pai '" + p.paiNome + "' nao encontrado; raiz criada na raiz da cena.");

            List<ClassData> data; string err;
            if (!Prepare(p, parent, log, out data, out err)) return "ERRO: " + err;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Substituir Vegetacao");
            int group = Undo.GetCurrentGroup();

            RemoverRaizes(p, parent);

            var root = new GameObject(p.nomeRaiz);
            Undo.RegisterCreatedObjectUndo(root, "Criar " + p.nomeRaiz);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            long totalTris = 0; int totalCount = 0;
            foreach (var cd in data)
            {
                var c = cd.c;
                var g = new GameObject(string.IsNullOrEmpty(c.nome) ? ("Classe" + cd.index) : c.nome);
                Undo.RegisterCreatedObjectUndo(g, "Criar grupo");
                g.transform.SetParent(root.transform, false);

                var rng = new System.Random(unchecked(p.seed * 31 + (cd.index + 1) * 7919));
                for (int i = 0; i < cd.recs.Count; i++)
                {
                    var r = cd.recs[i];
                    int pick = rng.Next(cd.prefabs.Count);
                    float rotY = c.rotacaoAleatoria ? (float)(rng.NextDouble() * 360.0) : 0f;
                    if (!c.rotacaoAleatoria) rng.NextDouble();

                    var prefab = cd.prefabs[pick];
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g.transform);
                    Undo.RegisterCreatedObjectUndo(inst, "Instanciar");
                    inst.name = g.name + "_" + i;
                    inst.transform.position = new Vector3(r.X, r.Y, r.Z);
                    inst.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
                    inst.transform.localScale = EscalaPara(c, r, cd.nat[pick]);
                    if (c.batchingStatic)
                        foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                    totalTris += cd.nat[pick].tris;
                    totalCount++;
                }
                log.AppendLine("  " + g.name + ": " + cd.recs.Count + " instancias");
            }

            if (p.desativarOriginais)
            {
                foreach (var cd in data)
                {
                    if (string.IsNullOrEmpty(cd.c.objetoCena)) continue;
                    var found = VegScene.FindAll(cd.c.objetoCena, parent);
                    if (found.Count == 0 && parent != null) found = VegScene.FindAll(cd.c.objetoCena, null);
                    foreach (var go in found)
                    {
                        if (go.transform.IsChildOf(root.transform)) continue;
                        Undo.RecordObject(go, "Desativar original");
                        go.SetActive(false);
                        log.AppendLine("  desativado: " + go.name);
                    }
                }
            }

            Undo.CollapseUndoOperations(group);
            MarkDirty();
            log.AppendLine("OK: " + totalCount + " instancias, ~" + totalTris + " triangulos. Ctrl+Z desfaz; ou use Desfazer Substituicao.");
            return log.ToString();
        }

        static Vector3 EscalaPara(VegClasse c, TreeRecord r, VegNativeSize nat)
        {
            float mult = c.multiplicador <= 0f ? 1f : c.multiplicador;
            float mn = Mathf.Max(0.0001f, c.escalaMin), mx = Mathf.Max(mn, c.escalaMax);
            float crown = (r.CrownW + r.CrownD) * 0.5f;
            float sy = 1f, sxz = 1f;
            switch (c.escala)
            {
                case VegEscala.Altura:
                    if (r.Height > 0f && nat.height > 0f) sy = sxz = Mathf.Clamp(r.Height / nat.height * mult, mn, mx);
                    break;
                case VegEscala.Copa:
                    if (crown > 0f && nat.crown > 0f) sy = sxz = Mathf.Clamp(crown / nat.crown * mult, mn, mx);
                    break;
                case VegEscala.AlturaECopa:
                    if (r.Height > 0f && nat.height > 0f) sy = Mathf.Clamp(r.Height / nat.height * mult, mn, mx);
                    if (crown > 0f && nat.crown > 0f) sxz = Mathf.Clamp(crown / nat.crown * mult, mn, mx);
                    else sxz = sy;
                    break;
                default:
                    sy = sxz = mult;
                    break;
            }
            return new Vector3(sxz, sy, sxz);
        }

        static int RemoverRaizes(VegetacaoPreset p, Transform parent)
        {
            var found = VegScene.FindAll(p.nomeRaiz, null);
            int n = 0;
            // somente raizes exatas (nome igual) para nao apagar objetos homonimos com sufixo
            foreach (var go in found)
            {
                if (go == null || go.name != p.nomeRaiz) continue;
                Undo.DestroyObjectImmediate(go);
                n++;
            }
            return n;
        }

        public static string Desfazer(VegetacaoPreset p, Transform parentOverride)
        {
            var log = new StringBuilder();
            Transform parent = ResolveParent(p, parentOverride);
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Desfazer Substituicao Vegetacao");
            int group = Undo.GetCurrentGroup();

            int n = RemoverRaizes(p, parent);
            int re = 0;
            foreach (var c in p.classes)
            {
                if (c == null || !c.ativa || string.IsNullOrEmpty(c.objetoCena)) continue;
                var found = VegScene.FindAll(c.objetoCena, parent);
                if (found.Count == 0 && parent != null) found = VegScene.FindAll(c.objetoCena, null);
                foreach (var go in found)
                {
                    if (go.activeSelf) continue;
                    Undo.RecordObject(go, "Reativar original");
                    go.SetActive(true);
                    re++;
                    log.AppendLine("  reativado: " + go.name);
                }
            }
            Undo.CollapseUndoOperations(group);
            MarkDirty();
            log.AppendLine("Raizes removidas: " + n + "; originais reativados: " + re);
            return log.ToString();
        }

        static void MarkDirty()
        {
            var sc = SceneManager.GetActiveScene();
            if (sc.IsValid()) EditorSceneManager.MarkSceneDirty(sc);
        }
    }
}
