using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Emuseu > Alinhar fonte BFC (simulacao)  - so lista o que mudaria, nao grava.
// Tools > Emuseu > Alinhar fonte BFC              - aplica e salva.
//
// Nas 8 cenas oficiais dos estados, em cada FinalCardBoard (Prefab_FinalCard):
// reverte os overrides de cena de m_fontSize e m_fontSizeBase do titulo e da descricao
// e desliga o Auto Size (reverte o override, ou forca false). Resultado: titulo 8, descricao 5.
// Tambem confere o prefab Prefab_FinalCard e, se precisar, corrige so esses campos.
public static class AlinharFonteBFC
{
    const float TamanhoTitulo = 8f;
    const float TamanhoDescricao = 5f;
    const string PrefabPath = "Assets/Prefabs/Puzzles/Prefab_FinalCard.prefab";

    static readonly string[] CenasOficiais =
    {
        "Assets/Games/RioDeJaneiro/Default/CenaRiodeJaneiro.unity",
        "Assets/Games/SaoPaulo/CenaSaoPaulo.unity",
        "Assets/Games/Salvador/CenaSalvador.unity",
        "Assets/Games/BeloHorizonte/CenaBeloHorizonte.unity",
        "Assets/Games/Brasilia/CenaBrasilia.unity",
        "Assets/Games/Recife/CenaRecife.unity",
        "Assets/Games/Fortaleza/CenaFortaleza.unity",
        "Assets/Games/PortoAlegre/CenaPortoAlegre.unity"
    };

    [MenuItem("Tools/Emuseu/Alinhar fonte BFC (simulacao)", false, 220)]
    static void Simulacao()
    {
        Executar(true);
    }

    [MenuItem("Tools/Emuseu/Alinhar fonte BFC", false, 221)]
    static void Aplicar()
    {
        Executar(false);
    }

    static void Executar(bool simulacao)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Alinhar fonte BFC", "Saia do Play Mode antes.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!simulacao)
        {
            if (!EditorUtility.DisplayDialog(
                "Alinhar fonte BFC",
                "Vai abrir e salvar as 8 cenas oficiais dos estados. Se o prefab nao estiver com titulo 8, descricao 5 e Auto Size desligado, tambem corrige so esses campos no Prefab_FinalCard.\n\nOs overrides de Font Size do titulo e da descricao voltam para o prefab (8 e 5) e o Auto Size e desligado.\n\nContinuar?",
                "Aplicar",
                "Cancelar"))
                return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Alinhar fonte BFC", "Prefab nao encontrado:\n" + PrefabPath, "OK");
            return;
        }

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        var relatorio = new StringBuilder();
        relatorio.AppendLine("[AlinharFonteBFC] " + (simulacao ? "SIMULACAO (nada foi gravado)" : "APLICADO"));
        int boards = 0, mudariam = 0, erros = 0;

        try
        {
            EditorUtility.DisplayProgressBar("Alinhar fonte BFC", "Prefab_FinalCard", 0f);
            mudariam += ProcessarPrefab(simulacao, relatorio, ref erros);

            for (int i = 0; i < CenasOficiais.Length; i++)
            {
                string path = CenasOficiais[i];
                EditorUtility.DisplayProgressBar("Alinhar fonte BFC", path, (i + 1f) / (CenasOficiais.Length + 1f));
                if (!File.Exists(path))
                {
                    relatorio.AppendLine();
                    relatorio.AppendLine("CENA AUSENTE: " + path);
                    erros++;
                    continue;
                }

                Scene cena = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var lista = new List<FinalCardBoard>();
                foreach (GameObject r in cena.GetRootGameObjects())
                    lista.AddRange(r.GetComponentsInChildren<FinalCardBoard>(true));

                relatorio.AppendLine();
                relatorio.AppendLine(Path.GetFileName(path) + " - " + lista.Count + " Board(s)");

                int mudouCena = 0;
                foreach (FinalCardBoard board in lista)
                {
                    boards++;
                    int n = ProcessarBoard(board, simulacao, relatorio, ref erros);
                    mudariam += n;
                    mudouCena += n;
                }

                if (!simulacao && mudouCena > 0)
                {
                    EditorSceneManager.MarkSceneDirty(cena);
                    if (!EditorSceneManager.SaveScene(cena))
                    {
                        relatorio.AppendLine("  ERRO: nao conseguiu salvar " + path);
                        erros++;
                    }
                    else
                        relatorio.AppendLine("  Cena salva.");
                }
                else if (simulacao)
                    relatorio.AppendLine(mudouCena > 0
                        ? "  (simulacao) " + mudouCena + " board(s) mudariam."
                        : "  Nenhuma mudanca.");
                else
                    relatorio.AppendLine("  Nenhuma mudanca; cena nao foi salva.");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (setup != null && setup.Length > 0)
            {
                try { EditorSceneManager.RestoreSceneManagerSetup(setup); }
                catch (Exception e) { Debug.LogWarning("[AlinharFonteBFC] Nao restaurou as cenas abertas: " + e.Message); }
            }
        }

        relatorio.AppendLine();
        relatorio.AppendLine("Total: " + boards + " board(s) nas cenas, " + mudariam + (simulacao ? " mudariam." : " alterados.") + (erros > 0 ? " Erros: " + erros + "." : ""));
        string texto = relatorio.ToString();
        Debug.Log("[AlinharFonteBFC]\n" + texto);
        EditorUtility.DisplayDialog("Alinhar fonte BFC", texto, "OK");
    }

    static int ProcessarPrefab(bool simulacao, StringBuilder relatorio, ref int erros)
    {
        relatorio.AppendLine("Prefab: " + PrefabPath);
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var board = go != null ? go.GetComponent<FinalCardBoard>() : null;
        if (board == null && go != null) board = go.GetComponentInChildren<FinalCardBoard>(true);
        if (board == null)
        {
            relatorio.AppendLine("  ERRO: FinalCardBoard nao encontrado no prefab.");
            erros++;
            return 0;
        }

        var detalhes = new List<string>();
        bool precisa = PrecisaAjuste(board.titulo, TamanhoTitulo, "titulo", detalhes)
                    | PrecisaAjuste(board.descricao, TamanhoDescricao, "descricao", detalhes);

        if (!precisa)
        {
            relatorio.AppendLine("  Ja esta com titulo 8, descricao 5, Auto Size desligado.");
            return 0;
        }

        foreach (string d in detalhes) relatorio.AppendLine("  " + d);
        if (simulacao) return 1;

        GameObject conteudo = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var b = conteudo.GetComponent<FinalCardBoard>();
            if (b == null) b = conteudo.GetComponentInChildren<FinalCardBoard>(true);
            if (b == null)
            {
                relatorio.AppendLine("  ERRO: FinalCardBoard nao encontrado ao abrir o prefab para gravar.");
                erros++;
                return 0;
            }
            AplicarNosTextos(b.titulo, TamanhoTitulo, false);
            AplicarNosTextos(b.descricao, TamanhoDescricao, false);
            PrefabUtility.SaveAsPrefabAsset(conteudo, PrefabPath);
            relatorio.AppendLine("  Prefab salvo.");
            return 1;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(conteudo);
        }
    }

    static int ProcessarBoard(FinalCardBoard board, bool simulacao, StringBuilder relatorio, ref int erros)
    {
        if (board == null) return 0;
        string nome = board.gameObject.name;
        string id = !string.IsNullOrEmpty(board.idInicial) ? board.idInicial : board.IdAtual;
        string rotulo = nome + (string.IsNullOrEmpty(id) ? "" : " (" + id + ")");

        var detalhes = new List<string>();
        bool precisa = PrecisaAjuste(board.titulo, TamanhoTitulo, "titulo", detalhes)
                    | PrecisaAjuste(board.descricao, TamanhoDescricao, "descricao", detalhes);

        if (board.titulo == null)
        {
            relatorio.AppendLine("  " + rotulo + " - ERRO: titulo nao atribuido.");
            erros++;
        }
        if (board.descricao == null)
        {
            relatorio.AppendLine("  " + rotulo + " - ERRO: descricao nao atribuida.");
            erros++;
        }

        if (!precisa)
        {
            relatorio.AppendLine("  " + rotulo + " - ja ok (titulo 8, descricao 5, Auto Size off).");
            return 0;
        }

        if (simulacao)
        {
            relatorio.AppendLine("  " + rotulo + " - mudaria: " + string.Join("; ", detalhes));
            return 1;
        }

        bool okT = AplicarNosTextos(board.titulo, TamanhoTitulo, true);
        bool okD = AplicarNosTextos(board.descricao, TamanhoDescricao, true);
        if ((board.titulo != null && !okT) || (board.descricao != null && !okD))
        {
            relatorio.AppendLine("  " + rotulo + " - ERRO: depois do ajuste nao ficou 8/5 com Auto Size off.");
            erros++;
        }
        else
            relatorio.AppendLine("  " + rotulo + " - ajustado: " + string.Join("; ", detalhes));
        return 1;
    }

    static bool PrecisaAjuste(TMP_Text tmp, float tamanhoAlvo, string campo, List<string> detalhes)
    {
        if (tmp == null) return false;
        var so = new SerializedObject(tmp);
        var pSize = so.FindProperty("m_fontSize");
        var pBase = so.FindProperty("m_fontSizeBase");
        var pAuto = so.FindProperty("m_enableAutoSizing");

        float size = pSize != null ? pSize.floatValue : tmp.fontSize;
        float b = pBase != null ? pBase.floatValue : size;
        bool auto = pAuto != null ? pAuto.boolValue : tmp.enableAutoSizing;
        bool ovSize = pSize != null && pSize.prefabOverride;
        bool ovBase = pBase != null && pBase.prefabOverride;
        bool ovAuto = pAuto != null && pAuto.prefabOverride;

        bool precisa = ovSize || ovBase || ovAuto || auto || !Aprox(size, tamanhoAlvo) || !Aprox(b, tamanhoAlvo);
        if (!precisa) return false;

        var bits = new List<string>();
        if (!Aprox(size, tamanhoAlvo) || ovSize)
            bits.Add("Font Size " + Num(size) + " -> " + Num(tamanhoAlvo) + (ovSize ? " (revert override)" : ""));
        if (!Aprox(b, tamanhoAlvo) || ovBase)
            bits.Add("fontSizeBase " + Num(b) + " -> " + Num(tamanhoAlvo) + (ovBase ? " (revert override)" : ""));
        if (auto || ovAuto)
            bits.Add("Auto Size " + (auto ? "on" : "off") + " -> off" + (ovAuto ? " (revert override)" : ""));
        detalhes.Add(campo + ": " + string.Join(", ", bits));
        return true;
    }

    // reverterOverrides: true nas instancias da cena; false no asset do prefab (so grava os valores).
    static bool AplicarNosTextos(TMP_Text tmp, float tamanhoAlvo, bool reverterOverrides)
    {
        if (tmp == null) return true;

        if (reverterOverrides && PrefabUtility.IsPartOfPrefabInstance(tmp))
        {
            var so = new SerializedObject(tmp);
            ReverterSeOverride(so.FindProperty("m_fontSize"));
            ReverterSeOverride(so.FindProperty("m_fontSizeBase"));
            ReverterSeOverride(so.FindProperty("m_enableAutoSizing"));
        }

        var so2 = new SerializedObject(tmp);
        var pSize = so2.FindProperty("m_fontSize");
        var pBase = so2.FindProperty("m_fontSizeBase");
        var pAuto = so2.FindProperty("m_enableAutoSizing");

        bool auto = pAuto != null ? pAuto.boolValue : tmp.enableAutoSizing;
        float size = pSize != null ? pSize.floatValue : tmp.fontSize;
        float b = pBase != null ? pBase.floatValue : size;

        if (!auto && Aprox(size, tamanhoAlvo) && Aprox(b, tamanhoAlvo))
            return true;

        if (pAuto != null) pAuto.boolValue = false;
        else tmp.enableAutoSizing = false;
        if (pSize != null) pSize.floatValue = tamanhoAlvo;
        if (pBase != null) pBase.floatValue = tamanhoAlvo;
        if (pSize == null && pBase == null) tmp.fontSize = tamanhoAlvo;
        so2.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tmp);

        so2.Update();
        auto = pAuto != null ? pAuto.boolValue : tmp.enableAutoSizing;
        size = pSize != null ? pSize.floatValue : tmp.fontSize;
        b = pBase != null ? pBase.floatValue : size;
        return !auto && Aprox(size, tamanhoAlvo) && Aprox(b, tamanhoAlvo);
    }

    static void ReverterSeOverride(SerializedProperty p)
    {
        if (p != null && p.prefabOverride)
            PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
    }

    static bool Aprox(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.01f;
    }

    static string Num(float v)
    {
        return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}