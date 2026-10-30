using UnityEngine;
using UnityEngine.Events;

// Coloque este script em cada puzzle.
// Ligue o metodo Finalizar() no evento de fim do puzzle (ex: OnEndGame ou OnPassNextScene).
// Ao terminar: avisa o ProgressoPuzzles, reativa a trigger, troca a cor do spark
// e desliga o BoardFinalCard deste puzzle.
public class PuzzleProgresso : MonoBehaviour
{
    [Header("Gerente")]
    [Tooltip("ProgressoPuzzles da cena. Se ficar vazio, procura sozinho.")]
    public ProgressoPuzzles gerente;

    [Tooltip("Ordem sugerida deste puzzle (1, 2, 3...). Usado so se a lista do gerente estiver vazia.")]
    public int ordem = 1;

    [Header("Trigger do puzzle")]
    [Tooltip("Objeto Trigger_Puzzle que comeca este puzzle. Ele e desligado quando o puzzle comeca e sera ligado de novo no fim.")]
    public GameObject trigger;

    [Tooltip("Ligado = depois de terminar, a trigger nao comeca o puzzle de novo (desliga o TriggerEvent). Desligado = pode jogar de novo.")]
    public bool bloquearRepetir = false;

    [Tooltip("Texto com o numero da trigger (1/5). Se ficar vazio, procura um filho chamado TextoNumero dentro da trigger.")]
    public TMPro.TMP_Text textoNumero;

    [Header("Spark (cor de concluido)")]
    [Tooltip("MeshRenderer do Spark. Se ficar vazio, procura um objeto chamado Spark dentro da trigger.")]
    public Renderer sparkRenderer;

    [Tooltip("Material de concluido (ex: Sparkles-new_Concluido). Pode ficar vazio.")]
    public Material materialConcluido;

    [Tooltip("Ligado = tambem troca a cor do spark pela cor abaixo.")]
    public bool usarCor = false;

    [Tooltip("Cor do spark quando o puzzle foi feito.")]
    public Color corConcluido = new Color(0.2f, 1f, 0.3f, 1f);

    [Header("Board Final Card")]
    [Tooltip("Objeto BoardFinalCard deste puzzle. Sera desligado quando a trigger voltar.")]
    public GameObject boardFinalCard;

    [Header("Player Config")]
    [Tooltip("Etapa deste puzzle, ex: DF_E2. Vazio = usa a cena e a ordem.")]
    public string etapaId;
    [Tooltip("Vazio = usa a carta (idInicial) do BoardFinalCard deste puzzle.")]
    public string cartaId;

    [Header("Eventos")]
    [Tooltip("Chamado uma vez quando este puzzle termina.")]
    public UnityEvent OnFinalizado;

    private bool finalizado;

    // Diz se este puzzle ja foi feito
    public bool Finalizado { get { return finalizado; } }

    // Chame quando o puzzle terminar. Chamar de novo nao conta duas vezes.
    public void Finalizar()
    {
        if (finalizado) return;
        finalizado = true;

        // a) avisa o gerente
        if (gerente == null) gerente = FindFirstObjectByType<ProgressoPuzzles>(FindObjectsInactive.Include);
        if (gerente != null) gerente.RegistrarConcluido(this);
        else Debug.LogWarning("PuzzleProgresso: nenhum ProgressoPuzzles na cena.", this);

        // b) reativa a trigger
        if (trigger != null)
        {
            trigger.SetActive(true);
            Collider[] cols = trigger.GetComponentsInChildren<Collider>(true);
            // Unity chama OnTriggerEnter mesmo com o script desligado, entao para bloquear desligamos os colliders
            foreach (Collider c in cols) c.enabled = !bloquearRepetir;

            TriggerEvent te = trigger.GetComponentInChildren<TriggerEvent>(true);
            if (te != null)
            {
                te.ResetTrigger();
                te.enabled = !bloquearRepetir;
            }
        }

        // c) troca a cor do spark
        TrocarSpark();

        // d) desliga o BoardFinalCard
        if (boardFinalCard != null) boardFinalCard.SetActive(false);

        // e) salva no PlayerConfig
        PlayerConfig cfg = PlayerConfig.Instancia;
        string carta = cartaId;
        if (string.IsNullOrEmpty(carta) && boardFinalCard != null)
        {
            FinalCardBoard fb = boardFinalCard.GetComponentInChildren<FinalCardBoard>(true);
            if (fb != null) carta = fb.idInicial;
        }
        if (cfg != null) cfg.RegistrarPuzzle(string.IsNullOrEmpty(etapaId) ? PlayerConfig.EtapaDaCena(ordem) : etapaId, carta);

        OnFinalizado?.Invoke();
    }

    // Escreve o numero na trigger (usado pelo gerente)
    public void DefinirNumero(string texto)
    {
        // Vazio: procura um texto chamado "TextoNumero" dentro da trigger
        if (textoNumero == null && trigger != null)
            foreach (var tx in trigger.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (tx.gameObject.name == "TextoNumero") { textoNumero = tx; break; }
        if (textoNumero != null) textoNumero.text = texto;
    }

    private void TrocarSpark()
    {
        if (sparkRenderer == null && trigger != null)
        {
            foreach (Renderer r in trigger.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name == "Spark") { sparkRenderer = r; break; }
        }
        if (sparkRenderer == null) return;

        if (materialConcluido != null)
            sparkRenderer.sharedMaterial = materialConcluido;

        if (usarCor)
        {
            // .material cria uma copia so para este spark
            Material m = sparkRenderer.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", corConcluido);
            if (m.HasProperty("_Color")) m.SetColor("_Color", corConcluido);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", corConcluido * 3f);
        }
    }
}