using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Idioma do jogo inteiro. PT e o padrao.
// Definido por: PlayerConfig, URL (?lang=en) ou SendMessage("Idioma", "SetLang", "en") vindo do site.
public static class Idioma
{
    public static bool Ingles { get; private set; }
    public static event Action<bool> OnMudou;
    static bool iniciado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar()
    {
        if (iniciado) return;
        iniciado = true;
        var cfg = PlayerConfig.Instancia;
        if (cfg != null) Ingles = cfg.Ingles;
        string url = Application.absoluteURL;
        if (!string.IsNullOrEmpty(url))
        {
            url = url.ToLower();
            if (url.Contains("lang=en")) Ingles = true;
            else if (url.Contains("lang=pt")) Ingles = false;
        }
        SceneManager.sceneLoaded += (s, m) => AplicarFinalCards();
        // Recebe mensagens do site (SendMessage("Idioma", "SetLang", "en"))
        var go = new GameObject("Idioma");
        go.AddComponent<IdiomaReceptor>();
        UnityEngine.Object.DontDestroyOnLoad(go);
    }

    public static void Definir(bool ingles)
    {
        if (Ingles == ingles) return;
        Ingles = ingles;
        var cfg = PlayerConfig.Instancia;
        if (cfg != null) cfg.idioma = ingles ? IdiomaJogador.EN : IdiomaJogador.PT;
        AplicarFinalCards();
        OnMudou?.Invoke(ingles);
        Debug.Log("[Idioma] " + (ingles ? "EN" : "PT"));
    }

    public static void Alternar() => Definir(!Ingles);

    static void AplicarFinalCards()
    {
        foreach (var b in UnityEngine.Object.FindObjectsByType<FinalCardBoard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            b.SetIngles(Ingles);
    }
}

// Para usar em botoes / eventos do Inspector e receber mensagens do site.
public class IdiomaReceptor : MonoBehaviour
{
    public void Alternar() => Idioma.Alternar();
    public void SetIngles(bool v) => Idioma.Definir(v);
    public void SetLang(string lang) => Idioma.Definir(!string.IsNullOrEmpty(lang) && lang.ToLower().StartsWith("en"));
}