// Aplica as keys de audio do site (RemoteVoiceClip / RemoteAudioSource) na CENA ABERTA,
// procurando cada objeto pelo caminho na hierarquia. Nao salva a cena sozinho.
// Menu: Tools > Emuseu > Aplicar chaves de audio (cena aberta)
//       Tools > Emuseu > Conferir chaves de audio (so mostra, nao altera)
// Cenas atendidas (pelo nome da cena aberta): CenaSaoPaulo, CenaRiodeJaneiro, CenaArena,
// CenaBeloHorizonte, CenaBrasilia, CenaFortaleza, CenaPortoAlegre, CenaRecife, CenaSalvador.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AplicarChavesAudio
{
    sealed class Entrada
    {
        public string caminho;   // caminho do GameObject a partir da raiz da cena
        public string key;       // key do site (PT)
        public bool musica;      // true = RemoteAudioSource (audio fixo); false = RemoteVoiceClip (voz da Anfitria)
        public Entrada(string caminho, string key, bool musica) { this.caminho = caminho; this.key = key; this.musica = musica; }
    }

    // keyEn fica VAZIO de proposito: o audio EN dos textos e o audioEn da MESMA key
    // (gerado a partir do textEn). Vazio = o codigo tenta audioEn e depois key + "_en".
    static Dictionary<string, Entrada[]> Tabela()
    {
        var t = new Dictionary<string, Entrada[]>();

        t["CenaSaoPaulo"] = new[]
        {
            new Entrada("AnfitrianModel/StartAnfitrian_1",    "voz_anfitrian_sp_start", false),
            new Entrada("AnfitrianModel/StartAnfitrian_2_E1", "voz_anfitrian_sp_e1", false),
            new Entrada("AnfitrianModel/StartAnfitrian_3_E2", "voz_anfitrian_sp_e2", false),
            new Entrada("AnfitrianModel/StartAnfitrian_4_E3", "voz_anfitrian_sp_e3", false),
            new Entrada("AnfitrianModel/StartAnfitrian_5_E4", "voz_anfitrian_sp_e4", false),
            new Entrada("AnfitrianModel/StartAnfitrian_6_E5", "voz_anfitrian_sp_e5", false),
            new Entrada("AnfitrianModel/StartAnfitrian_7_E3", "voz_anfitrian_parabens", false),
            new Entrada("SFX", "music_ambiente", true),
        };

        const string rj = "PuzzleGeral/AnfitrianModel/";
        t["CenaRiodeJaneiro"] = new[]
        {
            new Entrada(rj + "StartAnfitrian_1",                  "voz_anfitrian_rj_start", false),
            new Entrada(rj + "StartAnfitrian_2_E1",               "voz_anfitrian_rj_e1_1", false),
            // StartAnfitrian_3_Desativado (painel dica): sem key hoje, de proposito nao incluido.
            new Entrada(rj + "StartAnfitrian_4_Procure abola",    "voz_anfitrian_bola", false),
            new Entrada(rj + "StartAnfitrian_5_E2",               "voz_anfitrian_rj_e2_2", false),
            new Entrada(rj + "StartAnfitrian_6_E3",               "voz_anfitrian_rj_e3_1", false),
            new Entrada(rj + "StartAnfitrian_7_E4",               "voz_anfitrian_rj_e4_1", false),
            new Entrada(rj + "StartAnfitrian_8_E4",               "voz_anfitrian_rj_e4_2", false),
            new Entrada(rj + "StartAnfitrian_9_E5",               "voz_anfitrian_rj_e5_1", false),
            new Entrada("SFX", "music_ambiente", true),
        };

        t["CenaArena"] = new[]
        {
            new Entrada("Anfitrian/StartAnfitrian_1", "voz_anfitrian_estadio_chegada", false),
            new Entrada("Anfitrian/StartAnfitrian_2", "voz_anfitrian_estadio_hub", false),
            new Entrada("CrowdsWin", "crowd_long", true),
        };

        // Demais estados: 6 triggers da Anfitria (Start, E1..E5) + musica (SFX).
        const string bh = "PuzzleGeral/AnfitrianModel/";
        t["CenaBeloHorizonte"] = new[]
        {
            new Entrada(bh + "Anfitria_Trigger_Start", "voz_anfitrian_bh_start", false),
            new Entrada(bh + "Anfitria_Trigger_E1", "voz_anfitrian_bh_e1", false),
            new Entrada(bh + "Anfitria_Trigger_E2", "voz_anfitrian_bh_e2", false),
            new Entrada(bh + "Anfitria_Trigger_E3", "voz_anfitrian_bh_e3", false),
            new Entrada(bh + "Anfitria_Trigger_E4", "voz_anfitrian_bh_e4", false),
            new Entrada(bh + "Anfitria_Trigger_E5", "voz_anfitrian_bh_e5", false),
            new Entrada("SFX", "music_ambiente", true),
        };

        const string df = "PuzzleGeral/AnfitrianModel/";
        t["CenaBrasilia"] = new[]
        {
            new Entrada(df + "Anfitria_Trigger_Start", "voz_anfitrian_df_start", false),
            new Entrada(df + "Anfitria_Trigger_E1", "voz_anfitrian_df_e1", false),
            new Entrada(df + "Anfitria_Trigger_E2", "voz_anfitrian_df_e2", false),
            new Entrada(df + "Anfitria_Trigger_E3", "voz_anfitrian_df_e3", false),
            new Entrada(df + "Anfitria_Trigger_E4", "voz_anfitrian_df_e4", false),
            new Entrada(df + "Anfitria_Trigger_E5", "voz_anfitrian_df_e5", false),
            new Entrada("SFX", "music_ambiente", true),
        };

        const string ce = "PuzzleGeral/AnfitrianModel/";
        t["CenaFortaleza"] = new[]
        {
            new Entrada(ce + "Anfitria_Trigger_Start", "voz_anfitrian_ce_start", false),
            new Entrada(ce + "Anfitria_Trigger_E1", "voz_anfitrian_ce_e1", false),
            new Entrada(ce + "Anfitria_Trigger_E2", "voz_anfitrian_ce_e2", false),
            new Entrada(ce + "Anfitria_Trigger_E3", "voz_anfitrian_ce_e3", false),
            new Entrada(ce + "Anfitria_Trigger_E4", "voz_anfitrian_ce_e4", false),
            new Entrada(ce + "Anfitria_Trigger_E5", "voz_anfitrian_ce_e5", false),
            new Entrada("SFX", "music_ambiente", true),
        };

        const string rs = "PuzzleGeral/AnfitrianModel/";
        t["CenaPortoAlegre"] = new[]
        {
            new Entrada(rs + "Anfitria_Trigger_Start", "voz_anfitrian_rs_start", false),
            new Entrada(rs + "Anfitria_Trigger_E1", "voz_anfitrian_rs_e1", false),
            new Entrada(rs + "Anfitria_Trigger_E2", "voz_anfitrian_rs_e2", false),
            new Entrada(rs + "Anfitria_Trigger_E3", "voz_anfitrian_rs_e3", false),
            new Entrada(rs + "Anfitria_Trigger_E4", "voz_anfitrian_rs_e4", false),
            new Entrada(rs + "Anfitria_Trigger_E5", "voz_anfitrian_rs_e5", false),
            new Entrada("SFX", "music_ambiente", true),
        };

        const string pe = "PuzzleGeral/AnfitrianModel/";
        t["CenaRecife"] = new[]
        {
            new Entrada(pe + "Anfitria_Trigger_Start", "voz_anfitrian_pe_start", false),
            new Entrada(pe + "Anfitria_Trigger_E1", "voz_anfitrian_pe_e1", false),
            new Entrada(pe + "Anfitria_Trigger_E2", "voz_anfitrian_pe_e2", false),
            new Entrada(pe + "Anfitria_Trigger_E3", "voz_anfitrian_pe_e3", false),
            new Entrada(pe + "Anfitria_Trigger_E4", "voz_anfitrian_pe_e4", false),
            new Entrada(pe + "Anfitria_Trigger_E5", "voz_anfitrian_pe_e5", false),
            new Entrada("SFX", "music_ambiente", true),
        };

        const string ba = "PuzzlesGeral/AnfitrianModel/";
        t["CenaSalvador"] = new[]
        {
            new Entrada(ba + "Anfitria_Trigger_Start", "voz_anfitrian_ba_start", false),
            new Entrada(ba + "Anfitria_Trigger_E1", "voz_anfitrian_ba_e1", false),
            new Entrada(ba + "Anfitria_Trigger_E2", "voz_anfitrian_ba_e2", false),
            new Entrada(ba + "Anfitria_Trigger_E3", "voz_anfitrian_ba_e3", false),
            new Entrada(ba + "Anfitria_Trigger_E4", "voz_anfitrian_ba_e4", false),
            new Entrada(ba + "Anfitria_Trigger_E5", "voz_anfitrian_ba_e5", false),
            new Entrada("SFX", "music_ambiente", true),
        };

        return t;
    }

    [MenuItem("Tools/Emuseu/Aplicar chaves de \u00e1udio")]
    static void Aplicar() { Executar(true); }

    [MenuItem("Tools/Emuseu/Conferir chaves de \u00e1udio (n\u00e3o altera)")]
    static void Conferir() { Executar(false); }

    static void Executar(bool aplicar)
    {
        Scene cena = SceneManager.GetActiveScene();
        Entrada[] lista;
        if (!Tabela().TryGetValue(cena.name, out lista))
        {
            EditorUtility.DisplayDialog("Chaves de audio",
                "A cena aberta (" + cena.name + ") nao esta na tabela.\nAbra uma cena de estado (SP, RJ, BH, DF, CE, RS, PE, BA) ou a CenaArena.", "OK");
            return;
        }

        int alterados = 0, iguais = 0, problemas = 0;
        foreach (Entrada e in lista)
        {
            GameObject go = Achar(cena, e.caminho);
            if (go == null) { Debug.LogWarning("[Chaves] NAO ACHEI o objeto: " + e.caminho); problemas++; continue; }
            bool mudou;
            bool ok = e.musica ? Musica(go, e, aplicar, out mudou) : Voz(go, e, aplicar, out mudou);
            if (!ok) problemas++; else if (mudou) alterados++; else iguais++;
        }

        if (aplicar && alterados > 0) EditorSceneManager.MarkSceneDirty(cena);
        string resumo = (aplicar ? "Aplicado" : "Conferido") + " em " + cena.name + ": " + alterados + (aplicar ? " alterados" : " a alterar")
            + ", " + iguais + " ja certos, " + problemas + " com problema." + (aplicar ? "\nLembre de salvar a cena (Ctrl+S)." : "");
        Debug.Log("[Chaves] " + resumo);
        EditorUtility.DisplayDialog("Chaves de audio", resumo, "OK");
    }

    static bool Voz(GameObject go, Entrada e, bool aplicar, out bool mudou)
    {
        mudou = false;
        if (go.GetComponent<CharacterAppearDissolve>() == null)
        {
            Debug.LogWarning("[Chaves] " + e.caminho + " nao tem CharacterAppearDissolve - ignorado.", go);
            return false;
        }
        RemoteVoiceClip c = go.GetComponent<RemoteVoiceClip>();
        if (c == null)
        {
            mudou = true;
            Debug.Log("[Chaves] " + e.caminho + " -> " + (aplicar ? "ADICIONADO" : "adicionaria") + " RemoteVoiceClip key=" + e.key);
            if (!aplicar) return true;
            c = Undo.AddComponent<RemoteVoiceClip>(go);
        }
        else if (c.key == e.key && string.IsNullOrEmpty(c.keyEn))
        {
            return true; // ja esta certo
        }
        else
        {
            mudou = true;
            Debug.Log("[Chaves] " + e.caminho + " -> " + (aplicar ? "ATUALIZADO" : "atualizaria") + " key '" + c.key + "' (keyEn '" + c.keyEn + "') -> '" + e.key + "'");
            if (!aplicar) return true;
            Undo.RecordObject(c, "Aplicar chave de audio");
        }
        c.key = e.key;
        c.keyEn = "";
        EditorUtility.SetDirty(c);
        return true;
    }

    static bool Musica(GameObject go, Entrada e, bool aplicar, out bool mudou)
    {
        mudou = false;
        if (go.GetComponent<AudioSource>() == null)
        {
            Debug.LogWarning("[Chaves] " + e.caminho + " nao tem AudioSource - ignorado.", go);
            return false;
        }
        RemoteAudioSource c = go.GetComponent<RemoteAudioSource>();
        if (c == null)
        {
            mudou = true;
            Debug.Log("[Chaves] " + e.caminho + " -> " + (aplicar ? "ADICIONADO" : "adicionaria") + " RemoteAudioSource key=" + e.key);
            if (!aplicar) return true;
            c = Undo.AddComponent<RemoteAudioSource>(go);
        }
        else if (c.key == e.key)
        {
            return true; // ja esta certo (keyEn/porIdioma existentes sao mantidos)
        }
        else
        {
            mudou = true;
            Debug.Log("[Chaves] " + e.caminho + " -> " + (aplicar ? "ATUALIZADO" : "atualizaria") + " key '" + c.key + "' -> '" + e.key + "'");
            if (!aplicar) return true;
            Undo.RecordObject(c, "Aplicar chave de audio");
        }
        c.key = e.key;
        c.porIdioma = false; // musica/torcida: o mesmo audio nos dois idiomas
        EditorUtility.SetDirty(c);
        return true;
    }

    // Transform.Find acha tambem objetos desativados (GameObject.Find nao acha).
    static GameObject Achar(Scene cena, string caminho)
    {
        string[] partes = caminho.Split('/');
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            if (raiz.name != partes[0]) continue;
            if (partes.Length == 1) return raiz;
            Transform t = raiz.transform.Find(string.Join("/", partes, 1, partes.Length - 1));
            if (t != null) return t.gameObject;
        }
        return null;
    }
}