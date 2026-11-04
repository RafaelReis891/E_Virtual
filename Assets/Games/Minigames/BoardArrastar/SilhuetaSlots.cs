using System.Linq;
using UnityEngine;

/// <summary>
/// Coloque UMA vez no objeto do board. Para cada BoardSlot, acha a BoardItem com o mesmo id
/// (idCorreto == id), cria um filho "Silhueta" com o sprite da peca e usa como moldura do slot.
/// </summary>
public class SilhuetaSlots : MonoBehaviour
{
    [Header("Aparencia")]
    public Color cor = new Color(1f, 1f, 1f, 0.3f);
    [Tooltip("Ligado: peca toda chapada na cor (silhueta). Desligado: imagem da peca transparente.")]
    public bool modoSilhueta = true;
    [Tooltip("Opcional. Material usado no modo silhueta (vazio = GUI/Text Shader).")]
    public Material materialSilhueta;
    [Tooltip("Somado ao Order in Layer da peca (negativo = atras).")]
    public int deslocamentoOrdem = -1;
    [Tooltip("Escala da silhueta em relacao a peca (1 = mesmo tamanho). Diminua para as silhuetas nao se sobreporem.")]
    [Range(0.5f, 1.2f)] public float escala = 0.9f;

    [Header("Comportamento")]
    public bool aplicarAoIniciar = true;
    [Tooltip("Preenche o campo Moldura do slot com a silhueta.")]
    public bool usarComoMoldura = true;

    const string NomeFilho = "Silhueta";

    void Start() { if (aplicarAoIniciar) Aplicar(); }

    [ContextMenu("Aplicar")]
    public void Aplicar()
    {
        var itens = GetComponentsInChildren<BoardItem>(true);
        foreach (var slot in GetComponentsInChildren<BoardSlot>(true))
        {
            if (string.IsNullOrWhiteSpace(slot.idCorreto)) continue;
            var item = itens.FirstOrDefault(i => i != null &&
                string.Equals(i.id?.Trim(), slot.idCorreto.Trim(), System.StringComparison.OrdinalIgnoreCase));
            if (item == null) { Debug.LogWarning($"[SilhuetaSlots] slot '{slot.name}': nenhuma peca com id '{slot.idCorreto}'", slot); continue; }
            var srPeca = item.GetComponentInChildren<SpriteRenderer>(true);
            if (srPeca == null || srPeca.sprite == null) { Debug.LogWarning($"[SilhuetaSlots] peca '{item.name}' sem SpriteRenderer/sprite", item); continue; }

            Transform pai = slot.Encaixe;
            Transform t = pai.Find(NomeFilho);
            if (t == null) { t = new GameObject(NomeFilho).transform; t.SetParent(pai, false); }
            t.position = pai.position;
            t.rotation = srPeca.transform.rotation;
            Vector3 alvo = srPeca.transform.lossyScale, ps = pai.lossyScale;
            t.localScale = new Vector3(Div(alvo.x, ps.x) * escala, Div(alvo.y, ps.y) * escala, Div(alvo.z, ps.z));

            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = srPeca.sprite;
            sr.flipX = srPeca.flipX; sr.flipY = srPeca.flipY;
            sr.drawMode = srPeca.drawMode;
            if (srPeca.drawMode != SpriteDrawMode.Simple) sr.size = srPeca.size;
            sr.sortingLayerID = srPeca.sortingLayerID;
            sr.sortingOrder = srPeca.sortingOrder + deslocamentoOrdem;
            sr.color = cor;
            sr.sharedMaterial = modoSilhueta ? MaterialSilhueta() : srPeca.sharedMaterial;

            var col = t.GetComponent<Collider>(); if (col != null) col.enabled = false;
            if (usarComoMoldura) slot.moldura = t.gameObject;
            t.gameObject.SetActive(true);
        }
    }

    [ContextMenu("Remover Silhuetas")]
    public void Remover()
    {
        foreach (var slot in GetComponentsInChildren<BoardSlot>(true))
        {
            var t = slot.Encaixe.Find(NomeFilho);
            if (t == null) continue;
            if (slot.moldura == t.gameObject) slot.moldura = null;
            if (Application.isPlaying) Destroy(t.gameObject); else DestroyImmediate(t.gameObject);
        }
    }

    Material _matCache;
    Material MaterialSilhueta()
    {
        if (materialSilhueta != null) return materialSilhueta;
        if (_matCache == null)
        {
            var sh = Shader.Find("GUI/Text Shader");
            if (sh != null) _matCache = new Material(sh) { name = "Silhueta (auto)" };
        }
        return _matCache;
    }

    static float Div(float a, float b) => Mathf.Approximately(b, 0f) ? a : a / b;
}

