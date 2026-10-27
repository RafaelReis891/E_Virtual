using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Minigame de arrastar itens da parte de baixo do board para 1 a 5 targets.
/// Modo Ordem Definida: cada slot tem o seu item certo (1o, 2o, 3o...).
/// Modo Qualquer Ordem: basta colocar os itens certos, em qualquer slot.
/// </summary>
public class BoardDragPuzzle : MonoBehaviour
{
    public enum Modo { OrdemDefinida, QualquerOrdem }

    [Header("Configuracao")]
    public Modo modo = Modo.OrdemDefinida;

    [Tooltip("Targets da parte de cima (1 a 5).")]
    public BoardSlot[] slots;

    [Tooltip("Itens da parte de baixo que podem ser arrastados.")]
    public BoardItem[] itens;

    [Tooltip("Modo Qualquer Ordem: ids dos itens certos.")]
    public string[] respostasQualquerOrdem;

    [Tooltip("Distancia maxima para o item encaixar no slot ao soltar.")]
    public float distanciaEncaixe = 0.5f;

    [Tooltip("Se errar com todos os slots cheios, os itens voltam para baixo.")]
    public bool resetarAoErrar = true;

    [Tooltip("Se o item for errado, ele volta para baixo assim que o jogador soltar.")]
    public bool rejeitarErradoNaHora = false;

    public Camera mainCamera;

    [Header("Board")]
    [Tooltip("Objeto do board (fundo). Os itens deslizam colados na superficie dele.")]
    public Transform board;

    [Tooltip("Distancia do item para a frente do board enquanto arrasta (evita entrar no board).")]
    public float distanciaFrenteBoard = 0.05f;

    [Header("Feedback")]
    public AudioClip somPegar;
    public AudioClip somEncaixar;
    public AudioClip somErro;
    public AudioClip somSucesso;
    AudioSource audioSource;

    [Header("Eventos")]
    public UnityEvent OnErro;
    public UnityEvent OnEndGame;
    public UnityEvent OnPassNextScene;
    public float atrasoEndGame = 2f;
    public float atrasoNextScene = 3f;

    [Header("Debug")]
    public bool mostrarDebug = false;

    BoardItem arrastado;
    Plane planoArraste;
    Vector3 offset;
    bool resolvido;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        if (modo == Modo.OrdemDefinida && respostasQualquerOrdem != null && respostasQualquerOrdem.Length > 0)
            Debug.LogWarning("[Board] " + name + ": Respostas Qualquer Ordem preenchidas, mas o Modo esta em Ordem Definida. Troque o Modo para Qualquer Ordem.", this);
        foreach (var s in slots) if (s != null) s.MostrarMoldura(true);
    }

    // Compara ids ignorando maiusculas, espacos e acentos ("Convivencia" = "convivencia").
    static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string n = s.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (char c in n)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    static bool Igual(string a, string b) => Norm(a) == Norm(b);

    void Update()
    {
        if (resolvido || mainCamera == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            // RaycastAll: acha o item mesmo se o collider do board estiver na frente.
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var hit in hits)
            {
                BoardItem item = hit.collider.GetComponentInParent<BoardItem>();
                if (item == null || !itens.Contains(item)) continue;

                arrastado = item;
                var rb = item.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
                planoArraste = CriarPlano(item);
                if (planoArraste.Raycast(ray, out float e))
                {
                    // Mantem o ponto que o jogador clicou, sem "pular".
                    Vector3 noPlano = planoArraste.ClosestPointOnPlane(item.transform.position);
                    offset = noPlano - ray.GetPoint(e);
                }
                Tocar(somPegar);
                break;
            }
        }

        if (arrastado != null && Input.GetMouseButton(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (planoArraste.Raycast(ray, out float e))
                arrastado.transform.position = ray.GetPoint(e) + offset;
        }

        if (arrastado != null && Input.GetMouseButtonUp(0))
        {
            Soltar(arrastado);
            arrastado = null;
        }
    }

    Plane CriarPlano(BoardItem item)
    {
        if (board == null)
            return new Plane(-mainCamera.transform.forward, item.transform.position);

        // Normal do board virada para o lado da camera.
        // Escolhe o eixo do board (X, Y ou Z) que mais aponta para a camera.
        // Assim funciona mesmo com FBX rodado (ex: -90 no X).
        Vector3 paraCam = (mainCamera.transform.position - board.position).normalized;
        Vector3[] eixos = { board.forward, board.up, board.right };
        Vector3 normal = board.forward;
        float melhor = -1f;
        foreach (var ax in eixos)
        {
            float d = Mathf.Abs(Vector3.Dot(ax, paraCam));
            if (d > melhor) { melhor = d; normal = ax; }
        }
        if (Vector3.Dot(normal, paraCam) < 0f) normal = -normal;

        return new Plane(normal, board.position + normal * distanciaFrenteBoard);
    }
    void Soltar(BoardItem item)
    {
        BoardSlot maisProximo = null;
        float menor = float.MaxValue;
        foreach (var s in slots)
        {
            if (s == null) continue;
            float dist = Vector3.Distance(item.transform.position, s.Encaixe.position);
            if (dist < menor) { menor = dist; maisProximo = s; }
        }

        if (maisProximo == null || menor > distanciaEncaixe)
        {
            item.VoltarParaInicio();
            Log($"{item.id} voltou para baixo");
            return;
        }

        // Target de erro (Id Correto vazio, Ordem Definida): sempre devolve o item.
        bool alvoDeErro = modo == Modo.OrdemDefinida && EhAlvoDeErro(maisProximo);
        if ((rejeitarErradoNaHora || alvoDeErro) && !ItemValido(item, maisProximo))
        {
            Log($"{item.id} rejeitado em {maisProximo.name}");
            Tocar(somErro);
            OnErro?.Invoke();
            item.VoltarParaInicio();
            return;
        }

        // Tira o item do slot antigo.
        if (item.slotAtual != null) item.slotAtual.itemAtual = null;

        // Se o slot ja tinha outro item, ele volta para baixo.
        if (maisProximo.itemAtual != null && maisProximo.itemAtual != item)
            maisProximo.itemAtual.VoltarParaInicio();

        maisProximo.itemAtual = item;
        item.slotAtual = maisProximo;
        item.transform.SetPositionAndRotation(maisProximo.Encaixe.position, maisProximo.Encaixe.rotation);
        Tocar(somEncaixar);

        bool certo = ItemValido(item, maisProximo);
        Log($"{item.id} -> {maisProximo.name}" + (certo ? " (CORRETO!)" : ""));
        if (certo) maisProximo.OnItemCorreto?.Invoke();
        maisProximo.MostrarMoldura(false);

        Verificar();
    }

    // Em Ordem Definida, um target com Id Correto vazio so serve para dar erro.
    bool EhAlvoDeErro(BoardSlot s) => modo == Modo.OrdemDefinida && string.IsNullOrWhiteSpace(s.idCorreto);

    bool ItemValido(BoardItem item, BoardSlot slot)
    {
        if (modo == Modo.OrdemDefinida) return Igual(item.id, slot.idCorreto);
        return respostasQualquerOrdem != null && respostasQualquerOrdem.Any(r => Igual(r, item.id));
    }

    void Verificar()
    {
        // Targets de erro (Id Correto vazio) nao precisam ser preenchidos.
        if (slots.Any(s => s != null && !EhAlvoDeErro(s) && s.itemAtual == null)) return; // ainda tem slot vazio

        bool venceu;
        if (modo == Modo.OrdemDefinida)
        {
            venceu = slots.All(s => s == null || EhAlvoDeErro(s) || (s.itemAtual != null && Igual(s.itemAtual.id, s.idCorreto)));
        }
        else
        {
            // Venceu se todos os itens colocados sao respostas certas e nao ha repetidos.
            var colocados = slots.Where(s => s != null).Select(s => Norm(s.itemAtual.id)).ToList();
            var certos = (respostasQualquerOrdem ?? new string[0]).Select(Norm).ToList();
            venceu = colocados.All(c => certos.Contains(c)) && colocados.Distinct().Count() == colocados.Count;
            Log("colocados: " + string.Join(", ", colocados) + " | certos: " + string.Join(", ", certos));
        }

        if (venceu)
        {
            resolvido = true;
            Log("PUZZLE RESOLVIDO!");
            Tocar(somSucesso);
            Invoke(nameof(ChamarEndGame), atrasoEndGame);
        }
        else
        {
            Log("Resposta errada");
            Tocar(somErro);
            OnErro?.Invoke();
            if (resetarAoErrar)
                foreach (var s in slots) if (s != null && s.itemAtual != null) s.itemAtual.VoltarParaInicio();
        }
    }

    void ChamarEndGame()
    {
        OnEndGame?.Invoke();
        Invoke(nameof(ChamarNextScene), atrasoNextScene);
    }

    void ChamarNextScene() => OnPassNextScene?.Invoke();

    void Tocar(AudioClip c) { if (c != null) audioSource.PlayOneShot(c); }

    void Log(string msg) { if (mostrarDebug) Debug.Log($"[Board] {msg}", this); }
}