using System.Collections;
using UnityEngine;

// Trilha guia do HUB central ate os portais.
// Logo grande aparece no centro, pulsa e apaga; depois uma logo pequena
// acende espaco por espaco em todas as linhas ao mesmo tempo. Loop.
public class TrilhaLogoHub : MonoBehaviour
{
    [Tooltip("Logo do cliente (importar a textura como Sprite (2D and UI))")]
    public Sprite logo;

    [Header("Linhas")]
    public int linhas = 8;
    public int espacos = 6;
    [Tooltip("Distancia do centro ate o primeiro espaco")]
    public float raioInicial = 2f;
    [Tooltip("Distancia do primeiro ao ultimo espaco")]
    public float comprimento = 8f;
    [Tooltip("Gira todas as linhas para mirar nos portais (graus)")]
    public float anguloInicial = 0f;
    [Tooltip("Altura acima do chao para nao piscar com o piso")]
    public float altura = 0.02f;

    [Header("Tamanho")]
    public float tamanhoCentro = 3f;
    public float tamanhoPasso = 0.8f;
    [Tooltip("Quanto o ultimo espaco encolhe (1 = igual ao primeiro)")]
    [Range(0.2f, 1f)] public float escalaFinal = 0.7f;

    [Header("Tempo")]
    public float tempoCentro = 1.5f;
    public float tempoPasso = 0.35f;
    [Tooltip("Quanto tempo cada logo leva para apagar (rastro)")]
    public float tempoRastro = 0.6f;
    public float pausaFinal = 0.5f;

    SpriteRenderer centro;
    SpriteRenderer[,] passos;
    float[,] apagarDesde;

    void Start()
    {
        if (logo == null) { Debug.LogWarning("[TrilhaLogoHub] Falta a logo.", this); enabled = false; return; }
        Montar();
        StartCoroutine(Loop());
    }

    SpriteRenderer Criar(string nome, Vector3 posLocal, float tamanho, float anguloY)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = posLocal;
        go.transform.localRotation = Quaternion.Euler(90f, anguloY, 0f);
        float maior = Mathf.Max(logo.bounds.size.x, logo.bounds.size.y);
        go.transform.localScale = Vector3.one * (tamanho / maior);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = logo;
        sr.color = new Color(1, 1, 1, 0);
        return sr;
    }

    void Montar()
    {
        centro = Criar("Logo_Centro", new Vector3(0, altura, 0), tamanhoCentro, anguloInicial);
        passos = new SpriteRenderer[linhas, espacos];
        apagarDesde = new float[linhas, espacos];
        for (int l = 0; l < linhas; l++)
        {
            float ang = anguloInicial + l * 360f / linhas;
            Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
            for (int e = 0; e < espacos; e++)
            {
                float t = espacos > 1 ? (float)e / (espacos - 1) : 0f;
                Vector3 p = dir * (raioInicial + t * comprimento) + Vector3.up * altura;
                float tam = tamanhoPasso * Mathf.Lerp(1f, escalaFinal, t);
                passos[l, e] = Criar($"Logo_L{l}_E{e}", p, tam, ang);
                apagarDesde[l, e] = -999f;
            }
        }
    }

    void SetAlpha(SpriteRenderer sr, float a)
    {
        var c = sr.color; c.a = a; sr.color = c;
    }

    void Update()
    {
        if (passos == null) return;
        for (int l = 0; l < linhas; l++)
            for (int e = 0; e < espacos; e++)
            {
                float d = Time.time - apagarDesde[l, e];
                if (d >= 0f && d <= tempoRastro + 0.1f)
                    SetAlpha(passos[l, e], 1f - Mathf.Clamp01(d / Mathf.Max(0.01f, tempoRastro)));
            }
    }

    IEnumerator Loop()
    {
        while (true)
        {
            // Centro: fade in, pulso, fade out
            float t = 0f;
            Vector3 escBase = centro.transform.localScale;
            while (t < tempoCentro)
            {
                float k = t / tempoCentro;
                SetAlpha(centro, Mathf.Sin(k * Mathf.PI));
                centro.transform.localScale = escBase * (1f + 0.1f * Mathf.Sin(k * Mathf.PI * 2f));
                t += Time.deltaTime;
                yield return null;
            }
            SetAlpha(centro, 0f);
            centro.transform.localScale = escBase;

            // Passos: todas as linhas juntas
            for (int e = 0; e < espacos; e++)
            {
                for (int l = 0; l < linhas; l++)
                {
                    SetAlpha(passos[l, e], 1f);
                    apagarDesde[l, e] = Time.time + tempoPasso; // fica aceso um passo e depois apaga
                }
                yield return new WaitForSeconds(tempoPasso);
            }
            yield return new WaitForSeconds(tempoRastro + pausaFinal);
        }
    }
}
