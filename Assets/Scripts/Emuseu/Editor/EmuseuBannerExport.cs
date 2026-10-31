#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Tools > Emuseu > Exportar Texturas dos Banners (PNG)
// Para cada RemoteBanner das cenas abertas (inclusive inativos): descobre a chave e a textura que esta hoje no slot de
// material de imagem (o mesmo slot/propriedade que o RemoteBanner troca em runtime) e grava <chave>.png em
// <projeto>/Builds/BannerUploads (fora de Assets), mais lista.csv. Serve para o admin subir a imagem atual ao site.
// A textura pode ser comprimida/nao legivel: passa por RenderTexture (Blit) + ReadPixels, respeitando sRGB, max 2048.
public static class EmuseuBannerExport
{
    const int TamanhoMaximo = 2048;

    // Mesma logica do RemoteBanner.ResolverSlot (que e privado).
    static readonly string[] NomesConhecidos = { "M_TelaoCircular", "M_SimpleScreen", "Telao_02", "ElasNoJogo_Opaco" };
    // Ordem de preferencia da propriedade de textura: shadergraph do LED (_Albedo), URP (_BaseMap), legado, emissao.
    static readonly string[] PropsTextura = { "_Albedo", "_BaseMap", "_MainTex", "_EmissionMap" };

    sealed class Linha
    {
        public string chave, objeto, nomeTextura = "", material = "", propriedade = "", arquivo = "", status = "";
        public int largura, altura, expLargura, expAltura;
        public RemoteBanner banner;
    }

    [MenuItem("Tools/Emuseu/Exportar Texturas dos Banners (PNG)")]
    static void Exportar()
    {
        RemoteBanner[] todos = UnityEngine.Object.FindObjectsByType<RemoteBanner>(FindObjectsInactive.Include);
        var banners = new List<RemoteBanner>();
        foreach (RemoteBanner b in todos)
            if (b != null && !EditorUtility.IsPersistent(b.gameObject)) banners.Add(b);
        if (banners.Count == 0)
        {
            Debug.LogWarning("[EmuseuBannerExport] nenhum RemoteBanner nas cenas abertas.");
            EditorUtility.DisplayDialog("Exportar banners", "Nenhum RemoteBanner nas cenas abertas.", "OK");
            return;
        }
        banners.Sort((a, b) =>
        {
            int c = CompararNatural(a.Chave, b.Chave);
            return c != 0 ? c : string.CompareOrdinal(Caminho(a.gameObject), Caminho(b.gameObject));
        });

        string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "BannerUploads"));
        Directory.CreateDirectory(pasta);

        var linhas = new List<Linha>();
        var usadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int ok = 0, semTextura = 0, falhas = 0, duplicadas = 0;
        try
        {
            for (int i = 0; i < banners.Count; i++)
            {
                RemoteBanner b = banners[i];
                if (EditorUtility.DisplayCancelableProgressBar("Exportando banners", b.Chave + " (" + (i + 1) + "/" + banners.Count + ")", (float)i / banners.Count))
                {
                    Debug.LogWarning("[EmuseuBannerExport] cancelado pelo usuario em " + b.Chave);
                    break;
                }

                var l = new Linha { chave = b.Chave, objeto = Caminho(b.gameObject), banner = b };
                linhas.Add(l);

                Texture tex;
                string aviso;
                if (!AcharTextura(b, l, out tex, out aviso))
                {
                    l.status = "sem textura: " + aviso;
                    semTextura++;
                    Debug.LogWarning("[EmuseuBannerExport] " + l.chave + ": sem textura (" + aviso + ") em " + l.objeto, b.gameObject);
                    continue;
                }
                l.nomeTextura = tex.name;
                l.largura = tex.width;
                l.altura = tex.height;

                string nomeArquivo = NomeSeguro(l.chave) + ".png";
                if (!usadas.Add(nomeArquivo))
                {
                    l.status = "chave duplicada (nao exportada)";
                    duplicadas++;
                    Debug.LogWarning("[EmuseuBannerExport] chave duplicada '" + l.chave + "' em " + l.objeto + " - ignorada, ja exportada por outro objeto.", b.gameObject);
                    continue;
                }

                try
                {
                    byte[] png = CodificarPng(tex, out l.expLargura, out l.expAltura);
                    if (png == null) { l.status = "falha ao rasterizar"; falhas++; continue; }
                    string destino = Path.Combine(pasta, nomeArquivo);
                    File.WriteAllBytes(destino, png);
                    l.arquivo = nomeArquivo;
                    l.status = "ok";
                    ok++;
                }
                catch (Exception ex)
                {
                    l.status = "erro: " + ex.Message;
                    falhas++;
                    Debug.LogError("[EmuseuBannerExport] " + l.chave + ": " + ex.GetType().Name + ": " + ex.Message, b.gameObject);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string csv = Path.Combine(pasta, "lista.csv");
        EscreverCsv(csv, linhas);

        Debug.Log("[EmuseuBannerExport] " + ok + " PNG(s) exportado(s) de " + banners.Count + " banner(s) -> " + pasta +
                  "\n  sem textura: " + semTextura + ", chaves duplicadas: " + duplicadas + ", falhas: " + falhas + "\n  lista: " + csv);
        EditorUtility.RevealInFinder(csv); // abre a pasta com o lista.csv selecionado
    }

    // ---------- textura ----------

    static bool AcharTextura(RemoteBanner b, Linha l, out Texture tex, out string aviso)
    {
        tex = null;
        aviso = "";
        int slotManual = -1;
        var so = new SerializedObject(b);
        SerializedProperty sp = so.FindProperty("slot");
        if (sp != null) slotManual = sp.intValue;

        List<Renderer> renderers = ResolverRenderers(b);
        if (renderers.Count == 0) { aviso = "sem MeshRenderer"; return false; }

        foreach (Renderer r in renderers)
        {
            int idx = ResolverSlot(r, slotManual);
            if (idx < 0) { aviso = "nenhum slot de material em " + r.name; continue; }
            Material m = r.sharedMaterials[idx];
            if (m == null) { aviso = "material nulo no slot " + idx + " de " + r.name; continue; }
            foreach (string prop in PropsTextura)
            {
                if (!m.HasProperty(prop)) continue;
                Texture t = m.GetTexture(prop);
                if (t == null) continue;
                if (t.dimension != UnityEngine.Rendering.TextureDimension.Tex2D)
                {
                    aviso = "textura " + t.name + " nao e 2D (" + t.dimension + ")";
                    continue;
                }
                tex = t;
                l.material = m.name;
                l.propriedade = prop;
                return true;
            }
            aviso = "material '" + m.name + "' (slot " + idx + ", shader " + (m.shader != null ? m.shader.name : "?") + ") sem textura em " + string.Join("/", PropsTextura);
        }
        return false;
    }

    static List<Renderer> ResolverRenderers(RemoteBanner b)
    {
        var lista = new List<Renderer>();
        Renderer own = b.GetComponent<Renderer>();
        if (own != null) { lista.Add(own); return lista; }
        foreach (Renderer r in b.GetComponentsInChildren<Renderer>(true))
            if (r is MeshRenderer || r is SkinnedMeshRenderer) lista.Add(r);
        return lista;
    }

    // Igual ao RemoteBanner.ResolverSlot: manual; 1) "Banner" no nome; 2) telao/logo conhecidos; 3) primeiro nao M_LightGreen*.
    static int ResolverSlot(Renderer r, int slot)
    {
        Material[] mats = r.sharedMaterials;
        if (mats == null || mats.Length == 0) return -1;
        if (slot >= 0) return slot < mats.Length ? slot : -1;
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null && mats[i].name.IndexOf("Banner", StringComparison.OrdinalIgnoreCase) >= 0) return i;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) continue;
            foreach (string conhecido in NomesConhecidos)
                if (mats[i].name.IndexOf(conhecido, StringComparison.OrdinalIgnoreCase) >= 0) return i;
        }
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null && !mats[i].name.StartsWith("M_LightGreen", StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    // Blit -> RenderTexture -> ReadPixels -> PNG. Cor (sRGB): RT sRGB, o Blit decodifica ao amostrar e recodifica ao escrever,
    // entao os bytes saem iguais aos da imagem original. Dados lineares: RT linear, sem conversao.
    static byte[] CodificarPng(Texture tex, out int w, out int h)
    {
        int sw = tex.width, sh = tex.height;
        float escala = Mathf.Min(1f, TamanhoMaximo / (float)Mathf.Max(sw, sh));
        w = Mathf.Max(1, Mathf.RoundToInt(sw * escala));
        h = Mathf.Max(1, Mathf.RoundToInt(sh * escala));

        var t2 = tex as Texture2D;
        if (t2 != null && t2.streamingMipmaps)
        {
            t2.requestedMipmapLevel = 0;
            if (t2.loadedMipmapLevel > 0)
                Debug.LogWarning("[EmuseuBannerExport] '" + tex.name + "' usa mipmap streaming e so o mip " + t2.loadedMipmapLevel + " esta carregado; a exportacao pode sair borrada (desligue Streaming Mipmaps ou rode de novo).");
        }

        bool srgb = tex.isDataSRGB;
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32,
            srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
        RenderTexture anterior = RenderTexture.active;
        Texture2D saida = null;
        try
        {
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            saida = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
            saida.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            saida.Apply(false, false);
            return saida.EncodeToPNG();
        }
        finally
        {
            RenderTexture.active = anterior;
            RenderTexture.ReleaseTemporary(rt);
            if (saida != null) UnityEngine.Object.DestroyImmediate(saida);
            if (t2 != null && t2.streamingMipmaps) t2.ClearRequestedMipmapLevel();
        }
    }

    // ---------- csv / utilidades ----------

    static void EscreverCsv(string caminho, List<Linha> linhas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("key,object_path,texture_name,width,height,export_width,export_height,material,property,file,status");
        foreach (Linha l in linhas)
        {
            sb.Append(Csv(l.chave)).Append(',')
              .Append(Csv(l.objeto)).Append(',')
              .Append(Csv(l.nomeTextura)).Append(',')
              .Append(l.largura).Append(',')
              .Append(l.altura).Append(',')
              .Append(l.expLargura).Append(',')
              .Append(l.expAltura).Append(',')
              .Append(Csv(l.material)).Append(',')
              .Append(Csv(l.propriedade)).Append(',')
              .Append(Csv(l.arquivo)).Append(',')
              .Append(Csv(l.status)).AppendLine();
        }
        File.WriteAllText(caminho, sb.ToString(), new UTF8Encoding(true));
    }

    static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    static string NomeSeguro(string chave)
    {
        var sb = new StringBuilder();
        char[] invalidos = Path.GetInvalidFileNameChars();
        foreach (char c in chave) sb.Append(Array.IndexOf(invalidos, c) >= 0 ? '_' : c);
        return sb.Length == 0 ? "_" : sb.ToString();
    }

    static string Caminho(GameObject go)
    {
        string p = go.name;
        Transform t = go.transform.parent;
        while (t != null) { p = t.name + "/" + p; t = t.parent; }
        return go.scene.IsValid() ? go.scene.name + ":" + p : p;
    }

    // Ordem natural: banner_escadaria_2 antes de banner_escadaria_10.
    static int CompararNatural(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                string na = a.Substring(si, i - si).TrimStart('0');
                string nb = b.Substring(sj, j - sj).TrimStart('0');
                if (na.Length != nb.Length) return na.Length < nb.Length ? -1 : 1;
                int c = string.CompareOrdinal(na, nb);
                if (c != 0) return c;
            }
            else
            {
                if (a[i] != b[j]) return a[i] < b[j] ? -1 : 1;
                i++; j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
#endif