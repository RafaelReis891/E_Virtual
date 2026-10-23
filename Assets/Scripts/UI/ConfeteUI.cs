using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Confete de UI. Coloque num objeto filho de um Canvas (RectTransform esticado na tela).
// Chame Disparar() (ex: pelo UnityEvent do puzzle concluido).
public class ConfeteUI : MonoBehaviour
{
    public enum Modo { ChuvaDoTopo, ExplosaoCentro, CanhoesLaterais }

    [Header("Disparo")]
    public Modo modo = Modo.CanhoesLaterais;
    public int quantidade = 120;
    public bool dispararAoIniciar = false;

    [Header("Visual")]
    public Sprite sprite; // opcional; vazio = quadrado branco
    public Color[] cores = new Color[] {
        new Color(1f,0.3f,0.3f), new Color(1f,0.8f,0.2f), new Color(0.3f,0.8f,0.4f),
        new Color(0.3f,0.6f,1f), new Color(0.8f,0.4f,1f), new Color(1f,0.5f,0.8f) };
    public Vector2 tamanhoMin = new Vector2(10, 16);
    public Vector2 tamanhoMax = new Vector2(18, 30);

    [Header("Fisica (pixels/s)")]
    public float velocidadeMin = 900f;
    public float velocidadeMax = 1600f;
    public float gravidade = 1800f;
    public float arrasto = 1.5f;
    public float duracao = 3.5f;
    public bool usarTempoReal = true; // funciona mesmo com Time.timeScale = 0

    [Header("Audio (opcional)")]
    public AudioSource audioSource;
    public AudioClip som;

    class Peca { public RectTransform rt; public Image img; public Vector2 vel; public float rot, velRot, vida, flipVel, fase; }

    readonly List<Peca> ativas = new List<Peca>();
    readonly Stack<Peca> pool = new Stack<Peca>();
    RectTransform area;

    void Awake()
    {
        area = (RectTransform)transform;
        if (dispararAoIniciar) Disparar();
    }

    public void Disparar() { Disparar(quantidade); }

    public void Disparar(int qtd)
    {
        if (audioSource != null && som != null) audioSource.PlayOneShot(som);
        Rect r = area.rect;
        for (int i = 0; i < qtd; i++)
        {
            Vector2 pos; Vector2 dir;
            switch (modo)
            {
                case Modo.ChuvaDoTopo:
                    pos = new Vector2(Random.Range(r.xMin, r.xMax), r.yMax + 20f);
                    dir = new Vector2(Random.Range(-0.3f, 0.3f), -Random.Range(0.05f, 0.3f));
                    break;
                case Modo.ExplosaoCentro:
                    pos = r.center;
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.4f);
                    break;
                default:
                    bool esq = (i % 2) == 0;
                    pos = new Vector2(esq ? r.xMin : r.xMax, r.yMin + r.height * 0.15f);
                    float ang = Random.Range(55f, 80f) * Mathf.Deg2Rad;
                    dir = new Vector2(Mathf.Cos(ang) * (esq ? 1f : -1f), Mathf.Sin(ang));
                    break;
            }
            Peca p = Pegar();
            p.rt.anchoredPosition = pos;
            p.rt.sizeDelta = new Vector2(Random.Range(tamanhoMin.x, tamanhoMax.x), Random.Range(tamanhoMin.y, tamanhoMax.y));
            float vel = Random.Range(velocidadeMin, velocidadeMax);
            if (modo == Modo.ChuvaDoTopo) vel *= 0.3f;
            p.vel = dir.normalized * vel;
            p.rot = Random.Range(0f, 360f);
            p.velRot = Random.Range(-540f, 540f);
            p.flipVel = Random.Range(4f, 10f);
            p.fase = Random.Range(0f, 6.28f);
            p.vida = duracao * Random.Range(0.8f, 1.2f);
            p.img.color = cores.Length > 0 ? cores[Random.Range(0, cores.Length)] : Color.white;
            p.rt.gameObject.SetActive(true);
            ativas.Add(p);
        }
    }

    Peca Pegar()
    {
        if (pool.Count > 0) return pool.Pop();
        GameObject go = new GameObject("Confete", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        Peca p = new Peca();
        p.rt = (RectTransform)go.transform;
        p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
        p.img = go.GetComponent<Image>();
        p.img.sprite = sprite;
        p.img.raycastTarget = false;
        return p;
    }

    void Update()
    {
        if (ativas.Count == 0) return;
        float dt = usarTempoReal ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = usarTempoReal ? Time.unscaledTime : Time.time;
        float fundo = area.rect.yMin - 50f;
        for (int i = ativas.Count - 1; i >= 0; i--)
        {
            Peca p = ativas[i];
            p.vel.y -= gravidade * dt;
            p.vel *= 1f / (1f + arrasto * dt);
            p.vel.x += Mathf.Sin(t * 3f + p.fase) * 60f * dt; // balanco
            p.rt.anchoredPosition += p.vel * dt;
            p.rot += p.velRot * dt;
            float flip = Mathf.Cos(t * p.flipVel + p.fase); // efeito de papel girando
            p.rt.localRotation = Quaternion.Euler(0, 0, p.rot);
            p.rt.localScale = new Vector3(1f, flip, 1f);
            p.vida -= dt;
            Color c = p.img.color;
            c.a = Mathf.Clamp01(p.vida / 0.5f);
            p.img.color = c;
            if (p.vida <= 0f || p.rt.anchoredPosition.y < fundo)
            {
                p.rt.gameObject.SetActive(false);
                ativas.RemoveAt(i);
                pool.Push(p);
            }
        }
    }
}
