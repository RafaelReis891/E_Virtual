using UnityEngine;

// Liga o idioma do site (EmuseuBridge) ao Idioma do jogo.
// O site manda OnPlayConfig (idioma inicial, payload.language) e OnContentLanguage (troca PT/EN).
// Os dois terminam em EmuseuBridge.PlayConfigApplied com payload.language = "ptbr" ou "eng".
// Aqui convertemos isso em Idioma.Definir(bool): troca a voz da Anfitria (CharacterAppearAnimator),
// os TextoIdioma dos puzzles, os FinalCardBoard e dispara Idioma.OnMudou.
// Nasce sozinho (RuntimeInitializeOnLoadMethod): nao precisa por em cena nem no Inspector.
public static class EmuseuIdiomaSync
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar()
    {
        // Assina de novo com seguranca (editor com Domain Reload desligado).
        EmuseuBridge.PlayConfigApplied -= Aplicar;
        EmuseuBridge.PlayConfigApplied += Aplicar;
    }

    static void Aplicar(EmuseuPayload payload)
    {
        if (payload == null || string.IsNullOrEmpty(payload.language)) return;
        string lang = payload.language.Trim().ToLowerInvariant();
        bool ingles = lang == "eng" || lang == "en" || lang.StartsWith("en-");
        if (Idioma.Ingles == ingles) return;   // Idioma.Definir tambem ignora, mas assim nao loga a toa
        Debug.Log("[EmuseuIdiomaSync] idioma do site '" + payload.language + "' -> Idioma " + (ingles ? "EN" : "PT"));
        Idioma.Definir(ingles);
    }
}