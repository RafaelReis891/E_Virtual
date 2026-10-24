using UnityEngine;
using UnityEngine.Events;

// Um botao da mesa. Precisa de um Collider para ser clicado.
public class MesaBotao : MonoBehaviour
{
    [Tooltip("Identificador da resposta (ex: A, B, 1, Rio...)")]
    public string id;

    [Header("Conteudo (opcional)")]
    public TMPro.TMP_Text texto;
    public SpriteRenderer imagem;
    public string textoInicial;
    public Sprite imagemInicial;

    [Header("Animacao de apertar")]
    public Transform tampa;
    public float profundidade = 0.01f;
    public float velocidade = 12f;

    [Header("Cores (opcional, usa o Renderer da tampa)")]
    public Renderer rendererTampa;
    public Color corSelecionado = new Color(1f, 0.85f, 0.3f);

    [Header("Eventos")]
    public UnityEvent OnApertado;

    [HideInInspector] public bool selecionado;
    [HideInInspector] public bool travado;

    Vector3 posOriginal;
    float alvo;
    Color corOriginal;
    bool temCor;

    void Awake()
    {
        if (tampa == null) tampa = transform;
        posOriginal = tampa.localPosition;
        if (texto != null && !string.IsNullOrEmpty(textoInicial)) texto.text = textoInicial;
        if (imagem != null && imagemInicial != null) imagem.sprite = imagemInicial;
        if (rendererTampa != null) { corOriginal = rendererTampa.material.color; temCor = true; }
    }

    void Update()
    {
        Vector3 destino = posOriginal - Vector3.up * (profundidade * alvo);
        tampa.localPosition = Vector3.Lerp(tampa.localPosition, destino, Time.deltaTime * velocidade);
        if (alvo > 0f && !selecionado) alvo = Mathf.MoveTowards(alvo, 0f, Time.deltaTime * 4f);
    }

    public void Apertar()
    {
        alvo = 1f;
        OnApertado.Invoke();
    }

    public void SetSelecionado(bool s)
    {
        selecionado = s;
        alvo = s ? 1f : 0f;
        if (temCor) rendererTampa.material.color = s ? corSelecionado : corOriginal;
    }
}
