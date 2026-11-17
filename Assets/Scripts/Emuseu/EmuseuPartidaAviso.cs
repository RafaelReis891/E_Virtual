using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Avisa o site quando um puzzle começa e quando termina de verdade.
// Nasce sozinho, sem setup na cena. Abandono não chama Finalizar, então não vira conclusão.
// O mesmo puzzle não dispara de novo nesta sessão.
public static class EmuseuPartidaAviso
{
    static readonly HashSet<string> ligados = new HashSet<string>();
    static readonly HashSet<string> iniciadas = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar()
    {
        ligados.Clear();
        iniciadas.Clear();
        SceneManager.sceneLoaded -= AoCena;
        SceneManager.sceneLoaded += AoCena;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Depois()
    {
        Ligar(SceneManager.GetActiveScene());
    }

    static void AoCena(Scene cena, LoadSceneMode modo)
    {
        Ligar(cena);
    }

    static void Ligar(Scene cena)
    {
        var todos = Object.FindObjectsByType<PuzzleProgresso>(FindObjectsInactive.Include);
        foreach (var puzzle in todos)
        {
            if (puzzle == null || puzzle.gameObject.scene != cena) continue;
            if (!ligados.Add(puzzle.GetEntityId().ToString())) continue;
            var atual = puzzle;
            if (atual.trigger != null)
            {
                var eventos = atual.trigger.GetComponentsInChildren<TriggerEvent>(true);
                foreach (var evento in eventos)
                    evento._OnTriggerEnter.AddListener(_ => AvisarInicio(atual));
            }
            atual.OnFinalizado.AddListener(() => AvisarFim(atual));
        }
    }

    static string Id(PuzzleProgresso puzzle)
    {
        string etapa = string.IsNullOrEmpty(puzzle.etapaId) ? puzzle.ordem.ToString() : puzzle.etapaId.Trim();
        return puzzle.gameObject.scene.name + ":" + etapa;
    }

    static void AvisarInicio(PuzzleProgresso puzzle)
    {
        if (puzzle == null || puzzle.Finalizado) return;
        string id = Id(puzzle);
        if (!iniciadas.Add(id)) return;
        EmuseuBridge.SendToReact("partida-iniciada", "{\"id\":\"" + Limpar(id) + "\"}");
    }

    static void AvisarFim(PuzzleProgresso puzzle)
    {
        if (puzzle == null) return;
        EmuseuBridge.SendToReact("partida-concluida", "{\"id\":\"" + Limpar(Id(puzzle)) + "\"}");
    }

    static string Limpar(string valor)
    {
        return valor.Replace("\\", "").Replace("\"", "");
    }
}
