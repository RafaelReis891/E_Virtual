using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class ProgressoIntEvent : UnityEvent<int> { }

// Progresso dos puzzles da cena (um por mapa/cidade).
// Conta quantos puzzles foram terminados e mostra no Canvas: "0/5", "1/5" ... "5/5".
// Cada puzzle tem um PuzzleProgresso que chama Registrar Concluido aqui.
public class ProgressoPuzzles : MonoBehaviour
{
    [Header("Puzzles da cena (ordem sugerida)")]
    [Tooltip("Arraste aqui os PuzzleProgresso da cena, na ordem sugerida (1, 2, 3...). Se ficar vazio, procura sozinho na cena.")]
    public List<PuzzleProgresso> puzzles = new List<PuzzleProgresso>();

    [Tooltip("Total de puzzles. Se a lista tiver itens, usa o tamanho da lista. Senao usa este numero.")]
    public int totalPadrao = 5;

    [Header("Texto no Canvas")]
    [Tooltip("Texto TextMeshPro do Canvas que mostra o progresso (ex: 1/5).")]
    public TMPro.TMP_Text textoProgresso;

    [Tooltip("Formato do texto. {0} = feitos, {1} = total. Ex: \"{0}/{1}\" ou \"Puzzles: {0}/{1}\".")]
    public string formato = "Puzzles {0}/{1}";

    [Header("Numero nas triggers")]
    [Tooltip("Ligado = escreve sozinho o numero em cada trigger (1/5, 2/5...) na ordem da lista. Desligado = voce escreve o texto na mao.")]
    public bool numerarTriggersAutomatico = true;

    [Tooltip("Formato do numero da trigger. {0} = numero do puzzle, {1} = total.")]
    public string formatoTrigger = "{0}/{1}";

    [Header("Eventos")]
    [Tooltip("Chamado quando um puzzle termina. Envia quantos ja foram feitos.")]
    public ProgressoIntEvent OnPuzzleCompleto;

    [Tooltip("Chamado quando todos os puzzles da cena forem terminados.")]
    public UnityEvent OnTodosCompletos;

    private readonly HashSet<PuzzleProgresso> concluidos = new HashSet<PuzzleProgresso>();
    private bool todosAvisado;

    // Quantos puzzles ja foram feitos
    public int Feitos { get { return concluidos.Count; } }

    // Total de puzzles da cena
    public int Total { get { return puzzles != null && puzzles.Count > 0 ? puzzles.Count : totalPadrao; } }

    private void Awake()
    {
        // Lista vazia: procura todos os PuzzleProgresso da cena
        if (puzzles == null || puzzles.Count == 0)
        {
            puzzles = new List<PuzzleProgresso>(
                FindObjectsByType<PuzzleProgresso>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            // Ordena pelo campo "ordem" de cada puzzle
            puzzles.Sort((a, b) => a.ordem.CompareTo(b.ordem));
        }

        for (int i = 0; i < puzzles.Count; i++)
        {
            if (puzzles[i] == null) continue;
            puzzles[i].gerente = this;
            if (numerarTriggersAutomatico)
                puzzles[i].DefinirNumero(string.Format(formatoTrigger, i + 1, Total));
        }

        AtualizarTexto();
    }

    // Chamado pelo PuzzleProgresso quando o puzzle termina
    public void RegistrarConcluido(PuzzleProgresso puzzle)
    {
        if (puzzle == null || concluidos.Contains(puzzle)) return; // nao conta duas vezes
        concluidos.Add(puzzle);

        AtualizarTexto();
        OnPuzzleCompleto?.Invoke(Feitos);

        if (!todosAvisado && Feitos >= Total)
        {
            todosAvisado = true;
            OnTodosCompletos?.Invoke();
        }
    }

    // Diz se um puzzle ja foi feito
    public bool EstaConcluido(PuzzleProgresso puzzle)
    {
        return puzzle != null && concluidos.Contains(puzzle);
    }

    // Atualiza o texto do Canvas
    public void AtualizarTexto()
    {
        if (textoProgresso != null)
            textoProgresso.text = string.Format(formato, Feitos, Total);
    }
}