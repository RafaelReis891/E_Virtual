using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Puzzle de mesa com N botoes.
// RespostaUnica: clicou no botao certo, venceu.
// Sequencia: precisa apertar os botoes na ordem configurada.
// Multipla: seleciona varios (liga/desliga) e confirma com o botao Confirmar.
public class MesaBotoesPuzzle : MonoBehaviour
{
    public enum Modo { RespostaUnica, Sequencia, Multipla }

    public Modo modo = Modo.RespostaUnica;
    public List<MesaBotao> botoes = new List<MesaBotao>();

    [Tooltip("RespostaUnica: 1 id. Sequencia: ids na ordem. Multipla: ids que devem estar selecionados.")]
    public List<string> idsCorretos = new List<string>();

    [Tooltip("Somente no modo Multipla: botao que confirma a resposta")]
    public MesaBotao botaoConfirmar;

    public Camera cam;
    public bool travarAoVencer = true;

    [Header("Sons")]
    public AudioSource audioSource;
    public AudioClip somClique;
    public AudioClip somErro;
    public AudioClip somSucesso;

    [Header("Eventos")]
    public UnityEvent OnErro;
    public UnityEvent OnEndGame;
    public UnityEvent OnPassNextScene;

    [Header("Debug")]
    public bool mostrarDebug;

    int passo;
    bool terminou;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (terminou || !Input.GetMouseButtonDown(0) || cam == null) return;
        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(r, 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            MesaBotao b = h.collider.GetComponentInParent<MesaBotao>();
            if (b == null) continue;
            if (b == botaoConfirmar) { b.Apertar(); Tocar(somClique); Confirmar(); return; }
            if (botoes.Contains(b)) { Clicou(b); return; }
        }
    }

    void Clicou(MesaBotao b)
    {
        if (b.travado) return;
        Tocar(somClique);
        Log("apertou " + b.id);

        if (modo == Modo.RespostaUnica)
        {
            b.Apertar();
            if (idsCorretos.Count > 0 && b.id == idsCorretos[0]) Vencer(); else Errar();
        }
        else if (modo == Modo.Sequencia)
        {
            b.Apertar();
            if (passo < idsCorretos.Count && b.id == idsCorretos[passo])
            {
                passo++;
                Log("sequencia " + passo + "/" + idsCorretos.Count);
                if (passo >= idsCorretos.Count) Vencer();
            }
            else { passo = 0; Errar(); }
        }
        else
        {
            b.SetSelecionado(!b.selecionado);
        }
    }

    public void Confirmar()
    {
        if (terminou || modo != Modo.Multipla) return;
        bool ok = true;
        foreach (var b in botoes)
        {
            bool deve = idsCorretos.Contains(b.id);
            if (b.selecionado != deve) { ok = false; break; }
        }
        if (ok) Vencer();
        else
        {
            foreach (var b in botoes) b.SetSelecionado(false);
            Errar();
        }
    }

    void Errar()
    {
        Log("ERRO");
        Tocar(somErro);
        OnErro.Invoke();
    }

    void Vencer()
    {
        terminou = true;
        Log("CORRETO! Puzzle resolvido");
        if (travarAoVencer) foreach (var b in botoes) b.travado = true;
        Tocar(somSucesso);
        OnEndGame.Invoke();
        OnPassNextScene.Invoke();
    }

    public void Resetar()
    {
        terminou = false; passo = 0;
        foreach (var b in botoes) { b.travado = false; b.SetSelecionado(false); }
    }

    void Tocar(AudioClip c) { if (c != null && audioSource != null) audioSource.PlayOneShot(c); }
    void Log(string m) { if (mostrarDebug) Debug.Log("[Mesa] " + name + ": " + m); }
}
