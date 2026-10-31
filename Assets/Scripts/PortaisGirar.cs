using System.Collections.Generic;
using UnityEngine;

// Gira todos os portais ("Portal_*") em volta de um pivot criado em runtime.
// Util quando os portais fazem parte de um FBX e nao da para reparentar no editor.
// Uso: colocar em qualquer objeto (ex.: arena_vortice_v13). Nada mais a configurar.
// Na Start cria 'Portais_Pivot' (filho do pai comum dos portais) e faz
// SetParent(pivot, worldPositionStays: true) em cada portal de nivel mais alto
// (portais aninhados dentro de outro Portal_ nao sao movidos separadamente).
[DisallowMultipleComponent]
public class PortaisGirar : MonoBehaviour
{
    const string NomePivot = "Portais_Pivot";

    [Tooltip("Prefixo do nome dos portais (procura em todos os descendentes da raiz)")]
    public string prefixo = "Portal_";
    [Tooltip("Opcional. Se vazio, usa o Transform deste objeto como raiz da busca")]
    public Transform raiz;

    [Header("Rotacao")]
    public float grausPorSegundo = 10f;
    [Tooltip("Eixo no espaco local do pai dos portais (up = giro horizontal)")]
    public Vector3 eixo = Vector3.up;

    [Header("Pivot")]
    [Tooltip("true: pivot na media das posicoes dos portais. false: pivot na posicao da raiz")]
    public bool usarCentroDosPortais = true;
    [Tooltip("Opcional. Se preenchido, o pivot fica na posicao deste Transform")]
    public Transform centro;

    Transform pivot;

    void Start()
    {
        Transform r = raiz != null ? raiz : transform;

        var portais = new List<Transform>();
        Coletar(r, portais);
        if (portais.Count == 0)
        {
            Debug.LogWarning("[PortaisGirar] Nenhum filho com prefixo '" + prefixo + "' em '" + r.name + "'.", this);
            enabled = false;
            return;
        }

        Transform paiComum = PaiComum(portais);

        Vector3 pos;
        if (centro != null) pos = centro.position;
        else if (usarCentroDosPortais)
        {
            pos = Vector3.zero;
            for (int i = 0; i < portais.Count; i++) pos += portais[i].position;
            pos /= portais.Count;
        }
        else pos = r.position;

        var go = new GameObject(NomePivot);
        pivot = go.transform;
        pivot.SetParent(paiComum, false);
        pivot.position = pos;
        pivot.localRotation = Quaternion.identity;

        for (int i = 0; i < portais.Count; i++)
            portais[i].SetParent(pivot, true);
    }

    void Update()
    {
        if (pivot == null) return;
        if (eixo.sqrMagnitude < 1e-6f) return;
        pivot.Rotate(eixo.normalized, grausPorSegundo * Time.deltaTime, Space.Self);
    }

    // Busca em profundidade; ao achar um match nao desce nele (so nivel mais alto).
    void Coletar(Transform pai, List<Transform> saida)
    {
        for (int i = 0; i < pai.childCount; i++)
        {
            Transform c = pai.GetChild(i);
            if (c.name == NomePivot) continue;
            if (c.name.StartsWith(prefixo, System.StringComparison.Ordinal)) saida.Add(c);
            else Coletar(c, saida);
        }
    }

    // Ancestral comum mais baixo entre os pais dos portais (null = raiz da cena).
    static Transform PaiComum(List<Transform> itens)
    {
        Transform cand = itens[0].parent;
        while (cand != null)
        {
            bool ok = true;
            for (int i = 1; i < itens.Count; i++)
                if (!itens[i].IsChildOf(cand)) { ok = false; break; }
            if (ok) return cand;
            cand = cand.parent;
        }
        return null;
    }
}