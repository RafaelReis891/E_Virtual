// MobileIphoneMemoriaTool.cs
// Menu: Mobile > Tools > iPhone Memoria > ...
// Ferramenta de Editor (somente Editor) para reduzir a memoria do WebGL no iPhone (Safari).
//
// Por que: o iPhone nao le DXT. Com o formato padrao do WebGL em DXT, toda textura em
// "Automatico" (ou sem override) sai em DXT e o Safari descompacta em RGBA32 (4-8x mais memoria).
// O Unity 6 WebGL so tem UM formato padrao por build (DXT, ETC2 ou ASTC).
//
// "2 - Aplicar" faz (idempotente, pode rodar de novo):
//  1) Player Settings > Web > Texture compression format = ASTC (o celular usa ASTC nativo;
//     o PC descompacta ASTC em software, como o iPhone faz hoje com DXT; PC tem memoria de sobra).
//     As texturas em Automatico continuam em Automatico: se um dia houver build dupla
//     (DXT para PC + ASTC para celular, subtarget no CiBuild), elas seguem cada build.
//  2) Overrides WebGL fixos em DXT/BC passam para Automatico (para seguirem o formato da build).
//  3) Audios importados > 1 MB em "Decompress On Load" ganham override WebGL "Compressed In Memory".
//     Streaming (ex.: CrowdsWin) e SFX pequenos ficam como estao.
// Gera relatorio no Console e em <projeto>/MobileReports/iPhoneMemoria_<data>.txt
// "Reverter formato padrao WebGL para DXT" volta so o item 1.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EVirtualMobile.EditorTools
{
    public static class MobileIphoneMemoriaTool
    {
        const string MenuRaiz = "Mobile/Tools/iPhone Memoria/";
        const string Plataforma = "WebGL";
        const int FmtASTC = 3;   // valor serializado em m_BuildTargetDefaultTextureCompressionFormat
        const int FmtDXTC = 5;
        const long AudioLimiteBytes = 1024L * 1024L;
        const int TamanhoLote = 40;

        static readonly string[] Ignorar =
        {
            "/_Backup_Deletados/", "/Stadium Creator 2/", "/Editor/", "/Editor Default Resources/",
            "/Gizmos/", "/TextMesh Pro/", "/MobileReports/", "/Capturas/"
        };

        [MenuItem(MenuRaiz + "1 - Relatorio (nao altera nada)", false, 300)]
        static void MenuRelatorio() { Executar(false); }

        [MenuItem(MenuRaiz + "2 - Aplicar ASTC + audio comprimido", false, 301)]
        static void MenuAplicar()
        {
            if (!EditorUtility.DisplayDialog("iPhone Memoria",
                "Vai:\n" +
                "- trocar o formato padrao de textura do WebGL para ASTC\n" +
                "- passar overrides WebGL fixos em DXT/BC para Automatico\n" +
                "- deixar audios > 1 MB em Compressed In Memory no WebGL\n\n" +
                "O Unity pode reimportar muitas texturas (demora). Continuar?", "Aplicar", "Cancelar"))
                return;
            Executar(true);
        }

        [MenuItem(MenuRaiz + "Reverter formato padrao WebGL para DXT", false, 320)]
        static void MenuReverterDxt()
        {
            var log = new StringBuilder();
            bool ok = DefinirFormato(FmtDXTC, log);
            log.AppendLine("Formato padrao WebGL agora: " + LerFormatos());
            Debug.Log("[iPhoneMemoria] " + log);
            EditorUtility.DisplayDialog("iPhone Memoria", (ok ? "Revertido.\n" : "Falhou.\n") + log, "OK");
        }

        // ------------------------------------------------------------------ principal
        static void Executar(bool aplicar)
        {
            var log = new StringBuilder();
            log.AppendLine("=== iPhone Memoria - " + (aplicar ? "APLICAR" : "RELATORIO") + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            // 1) formato padrao
            string antes = LerFormatos();
            log.AppendLine("Formato padrao WebGL (Player Settings) antes: " + antes);
            if (aplicar)
            {
                bool ok = DefinirFormato(FmtASTC, log);
                log.AppendLine("Formato padrao WebGL depois: " + LerFormatos() + (ok ? "" : "  <-- FALHOU, troque manualmente em Player Settings > Web > Other Settings"));
            }
            LogSubtarget(log, aplicar);

            // 2) texturas
            try { Texturas(aplicar, log); }
            catch (Exception ex) { log.AppendLine("ERRO texturas: " + ex); }
            finally { EditorUtility.ClearProgressBar(); }

            // 3) audio
            try { Audios(aplicar, log); }
            catch (Exception ex) { log.AppendLine("ERRO audio: " + ex); }
            finally { EditorUtility.ClearProgressBar(); }

            if (aplicar) AssetDatabase.SaveAssets();

            string arquivo = SalvarRelatorio(log.ToString());
            Debug.Log("[iPhoneMemoria] " + log + (arquivo != null ? "\nRelatorio: " + arquivo : ""));
            EditorUtility.DisplayDialog("iPhone Memoria", (aplicar ? "Aplicado. " : "Relatorio pronto. ") +
                "Detalhes no Console" + (arquivo != null ? " e em " + arquivo : "") + ".", "OK");
        }

        static bool Ignorado(string path)
        {
            string p = "/" + path.Replace('\\', '/');
            foreach (var s in Ignorar) if (p.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static bool EhAstc(TextureImporterFormat f) { return f.ToString().StartsWith("ASTC", StringComparison.Ordinal); }
        static bool EhDxt(TextureImporterFormat f)
        {
            string s = f.ToString();
            return s.StartsWith("DXT", StringComparison.Ordinal) || s.StartsWith("BC", StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------ texturas
        static void Texturas(bool aplicar, StringBuilder log)
        {
            var paths = AssetDatabase.FindAssets("t:Texture", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().Where(p => !Ignorado(p)).ToList();

            int total = 0, ovAstc = 0, ovAuto = 0, ovDxt = 0, ovOutro = 0, semOv = 0, semCompressao = 0, convertidas = 0;
            var outros = new List<string>();
            var mudar = new List<TextureImporter>();

            for (int i = 0; i < paths.Count; i++)
            {
                var ti = AssetImporter.GetAtPath(paths[i]) as TextureImporter;
                if (ti == null) continue;
                total++;
                if (i % 50 == 0) EditorUtility.DisplayProgressBar("iPhone Memoria - texturas", paths[i], (float)i / Mathf.Max(1, paths.Count));
                var ps = ti.GetPlatformTextureSettings(Plataforma);
                if (ps.overridden)
                {
                    if (ps.format == TextureImporterFormat.Automatic)
                    {
                        ovAuto++;
                        if (ps.textureCompression == TextureImporterCompression.Uncompressed) semCompressao++;
                    }
                    else if (EhAstc(ps.format)) ovAstc++;
                    else if (EhDxt(ps.format)) { ovDxt++; mudar.Add(ti); }
                    else { ovOutro++; if (outros.Count < 40) outros.Add(paths[i] + " (" + ps.format + ")"); }
                }
                else
                {
                    semOv++;
                    if (ti.textureCompression == TextureImporterCompression.Uncompressed) semCompressao++;
                }
            }

            if (aplicar && mudar.Count > 0)
            {
                for (int inicio = 0; inicio < mudar.Count; inicio += TamanhoLote)
                {
                    EditorUtility.DisplayProgressBar("iPhone Memoria - DXT -> Automatico", mudar[inicio].assetPath, (float)inicio / mudar.Count);
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        for (int i = inicio; i < Mathf.Min(inicio + TamanhoLote, mudar.Count); i++)
                        {
                            var ti = mudar[i];
                            var ps = ti.GetPlatformTextureSettings(Plataforma);
                            ps.format = TextureImporterFormat.Automatic;
                            ti.SetPlatformTextureSettings(ps);
                            ti.SaveAndReimport();
                            convertidas++;
                        }
                    }
                    finally { AssetDatabase.StopAssetEditing(); }
                }
            }

            int auto = ovAuto + semOv;
            log.AppendLine("--- Texturas (Assets, sem Stadium Creator 2/_Backup_Deletados/Editor/TextMesh Pro) ---");
            log.AppendLine("Total: " + total);
            log.AppendLine("Override WebGL ASTC fixo: " + ovAstc);
            log.AppendLine("Override WebGL Automatico: " + ovAuto);
            log.AppendLine("Sem override WebGL (segue o padrao): " + semOv);
            log.AppendLine("Override WebGL DXT/BC fixo: " + ovDxt + (aplicar ? " (convertidas para Automatico: " + convertidas + ")" : ""));
            log.AppendLine("Override WebGL outro formato (mantido): " + ovOutro);
            foreach (var o in outros) log.AppendLine("   " + o);
            log.AppendLine("Automatico/sem override marcadas 'None' (sem compressao, ficam RGBA): " + semCompressao);
            bool padraoAstc = LerFormatos().Contains("ASTC");
            log.AppendLine("Efeito na build com padrao " + (padraoAstc ? "ASTC" : "DXT") + ": ~" +
                (ovAstc + (padraoAstc ? auto - semCompressao + (aplicar ? convertidas : 0) : 0)) + " texturas em ASTC no WebGL, ~" +
                (padraoAstc ? (aplicar ? 0 : ovDxt) : auto - semCompressao + ovDxt) + " em DXT.");
        }

        // ------------------------------------------------------------------ audio
        static void Audios(bool aplicar, StringBuilder log)
        {
            var paths = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().Where(p => !Ignorado(p)).ToList();
            int total = 0, candidatos = 0, alterados = 0, jaOk = 0;
            log.AppendLine("--- Audio (WebGL) ---");
            foreach (var p in paths)
            {
                var ai = AssetImporter.GetAtPath(p) as AudioImporter;
                if (ai == null) continue;
                total++;
                long bytes = 0;
                try { bytes = new FileInfo(p).Length; } catch (Exception) { }
                bool temOv = ai.ContainsSampleSettingsOverride(Plataforma);
                var s = temOv ? ai.GetOverrideSampleSettings(Plataforma) : ai.defaultSampleSettings;
                if (bytes <= AudioLimiteBytes) continue;
                if (s.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    jaOk++;
                    log.AppendLine("   ok (" + s.loadType + ", " + (bytes / 1048576f).ToString("0.0") + " MB): " + p);
                    continue;
                }
                candidatos++;
                log.AppendLine("   " + (aplicar ? "-> CompressedInMemory" : "candidato") + " (" + (bytes / 1048576f).ToString("0.0") + " MB): " + p);
                if (aplicar)
                {
                    s.loadType = AudioClipLoadType.CompressedInMemory;
                    if (ai.SetOverrideSampleSettings(Plataforma, s)) { ai.SaveAndReimport(); alterados++; }
                    else log.AppendLine("   FALHOU SetOverrideSampleSettings: " + p);
                }
            }
            log.AppendLine("Clips: " + total + " | > 1 MB em Decompress On Load: " + candidatos +
                (aplicar ? " (alterados: " + alterados + ")" : "") + " | > 1 MB ja ok: " + jaOk);
        }

        // ------------------------------------------------------------------ Player Settings
        static SerializedObject PlayerSettingsSO()
        {
            var obj = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            return obj != null ? new SerializedObject(obj) : null;
        }

        static SerializedProperty AcharFormatsWebGL(SerializedObject so)
        {
            var lista = so.FindProperty("m_BuildTargetDefaultTextureCompressionFormat");
            if (lista == null || !lista.isArray) return null;
            for (int i = 0; i < lista.arraySize; i++)
            {
                var el = lista.GetArrayElementAtIndex(i);
                var bt = el.FindPropertyRelative("m_BuildTarget");
                if (bt != null && bt.propertyType == SerializedPropertyType.String && bt.stringValue == Plataforma)
                    return el.FindPropertyRelative("m_Formats");
            }
            return null;
        }

        static string NomeFormato(int v)
        {
            switch (v)
            {
                case 1: return "ETC";
                case 2: return "ETC2";
                case 3: return "ASTC";
                case 5: return "DXT";
                default: return "valor " + v;
            }
        }

        static string LerFormatos()
        {
            try
            {
                var so = PlayerSettingsSO();
                if (so == null) return "(nao encontrado)";
                var f = AcharFormatsWebGL(so);
                if (f == null || !f.isArray) return "(nao encontrado)";
                var nomes = new List<string>();
                for (int i = 0; i < f.arraySize; i++) nomes.Add(NomeFormato(f.GetArrayElementAtIndex(i).intValue));
                return nomes.Count == 0 ? "(vazio)" : string.Join(", ", nomes);
            }
            catch (Exception ex) { return "(erro: " + ex.Message + ")"; }
        }

        static bool DefinirFormato(int fmt, StringBuilder log)
        {
            try
            {
                var so = PlayerSettingsSO();
                var f = so != null ? AcharFormatsWebGL(so) : null;
                if (f != null && f.isArray)
                {
                    if (f.arraySize == 1 && f.GetArrayElementAtIndex(0).intValue == fmt)
                    {
                        log.AppendLine("Formato padrao WebGL ja estava em " + NomeFormato(fmt) + ".");
                        return true;
                    }
                    f.arraySize = 1;
                    f.GetArrayElementAtIndex(0).intValue = fmt;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(so.targetObject);
                    AssetDatabase.SaveAssets();
                    if (LerFormatos() == NomeFormato(fmt))
                    {
                        log.AppendLine("Formato padrao WebGL definido para " + NomeFormato(fmt) + " (SerializedObject).");
                        return true;
                    }
                }
            }
            catch (Exception ex) { log.AppendLine("SerializedObject falhou: " + ex.Message); }
            return DefinirPorReflexao(fmt, log);
        }

        static bool DefinirPorReflexao(int fmt, StringBuilder log)
        {
            var metodos = typeof(PlayerSettings).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "SetDefaultTextureCompressionFormat");
            foreach (var m in metodos)
            {
                var ps = m.GetParameters();
                if (ps.Length != 2 || ps[0].ParameterType != typeof(BuildTargetGroup)) continue;
                var t = ps[1].ParameterType;
                object val;
                if (t.IsEnum) val = Enum.ToObject(t, fmt);
                else if (t.IsArray && t.GetElementType() != null && t.GetElementType().IsEnum)
                {
                    var arr = Array.CreateInstance(t.GetElementType(), 1);
                    arr.SetValue(Enum.ToObject(t.GetElementType(), fmt), 0);
                    val = arr;
                }
                else continue;
                try
                {
                    m.Invoke(null, new object[] { BuildTargetGroup.WebGL, val });
                    AssetDatabase.SaveAssets();
                    log.AppendLine("Formato padrao WebGL definido para " + NomeFormato(fmt) + " (reflexao).");
                    return true;
                }
                catch (Exception ex) { log.AppendLine("Reflexao falhou: " + ex.Message); }
            }
            return false;
        }

        // Build Profiles > Web > Texture Compression (fica na Library, local). Se nao estiver em
        // "Use Player Settings", ele ganha do Player Settings nas builds locais.
        static void LogSubtarget(StringBuilder log, bool aplicar)
        {
            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("webGLBuildSubtarget", BindingFlags.Static | BindingFlags.Public);
                if (prop == null) { log.AppendLine("Build Profile Texture Compression: (nao disponivel)"); return; }
                object v = prop.GetValue(null, null);
                log.AppendLine("Build Profile Texture Compression (local): " + v);
                if (aplicar && v != null && v.ToString() != "Generic" && prop.CanWrite)
                {
                    prop.SetValue(null, Enum.Parse(prop.PropertyType, "Generic"), null);
                    log.AppendLine("  -> ajustado para Use Player Settings (Generic).");
                }
            }
            catch (Exception ex) { log.AppendLine("Build Profile Texture Compression: erro " + ex.Message); }
        }

        static string SalvarRelatorio(string texto)
        {
            try
            {
                string raiz = Path.GetDirectoryName(Application.dataPath);
                string pasta = Path.Combine(raiz, "MobileReports");
                Directory.CreateDirectory(pasta);
                string arq = Path.Combine(pasta, "iPhoneMemoria_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(arq, texto, new UTF8Encoding(false));
                return arq;
            }
            catch (Exception) { return null; }
        }
    }
}
#endif
