using System.Collections.Generic;
using UnityEngine;

// DIAGNOSTICO (nao precisa de Inspector, nao fica na cena): vigia os textos dos FinalCardBoard.
// - O FinalCardBoard chama Observar() no OnEnable e Aplicado() sempre que escreve titulo/descricao.
// - Por alguns frames depois de cada OnEnable/Aplicado (e em checkpoints de tempo) compara o texto atual do TMP
//   com o que o board aplicou. Se for diferente, loga "TEXTO ALTERADO POR OUTRO" (algo, ex: Animator/outro script, sobrescreveu).
// - Tambem loga quando o estado do texto muda (objeto ativo, componente ligado, alpha, escala) -> detecta Animator mexendo nos filhos.
// Todos os logs usam EmuseuDebug (tag "diag") => aparecem no console do navegador com o prefixo [Emuseu].
public class FinalCardTextWatcher : MonoBehaviour
{
    const int FramesVigia = 12;
    static readonly float[] Checkpoints = { 0.25f, 0.5f, 1f, 2f, 5f };

    class Item
    {
        public FinalCardBoard board;
        public TMPro.TMP_Text titulo, descricao;
        public bool temEsperado;
        public string esperadoTitulo, esperadoDesc;
        public int frameInicio;
        public float tempoInicio;
        public int proximoCheckpoint;
        public string motivo;
        public string ultimoAlertaTitulo, ultimoAlertaDesc;
        public string ultimoEstado;
        public bool avisouSemTexto;
    }

    static FinalCardTextWatcher instancia;
    static readonly List<Item> itens = new List<Item>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar()
    {
        instancia = null;
        itens.Clear();
    }

    static void Garantir()
    {
        if (instancia != null) return;
        var go = new GameObject("FinalCardTextWatcher");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<FinalCardTextWatcher>();
    }

    static Item Achar(FinalCardBoard b, bool criar)
    {
        for (int i = 0; i < itens.Count; i++) if (itens[i].board == b) return itens[i];
        if (!criar) return null;
        var it = new Item { board = b, frameInicio = Time.frameCount, tempoInicio = Time.realtimeSinceStartup, motivo = "criado" };
        itens.Add(it);
        return it;
    }

    // Board foi ligado: abre uma nova janela de vigia.
    public static void Observar(FinalCardBoard b, string motivo)
    {
        if (b == null) return;
        Garantir();
        var it = Achar(b, true);
        it.frameInicio = Time.frameCount;
        it.tempoInicio = Time.realtimeSinceStartup;
        it.proximoCheckpoint = 0;
        it.motivo = motivo;
        it.avisouSemTexto = false;
        it.ultimoAlertaTitulo = null;
        it.ultimoAlertaDesc = null;
        it.ultimoEstado = null;
        EmuseuDebug.Info("diag", "watcher: observando board=" + b.name + " motivo=" + motivo + " frame=" + Time.frameCount);
    }

    // O board acabou de escrever os textos: guarda o que deveria estar la.
    public static void Aplicado(FinalCardBoard b, TMPro.TMP_Text titulo, string t, TMPro.TMP_Text descricao, string d)
    {
        if (b == null) return;
        Garantir();
        var it = Achar(b, true);
        it.titulo = titulo;
        it.descricao = descricao;
        it.esperadoTitulo = t ?? "";
        it.esperadoDesc = d ?? "";
        it.temEsperado = true;
        it.ultimoAlertaTitulo = null;
        it.ultimoAlertaDesc = null;
        // reabre a janela de frames (mas nao mexe nos checkpoints de tempo)
        it.frameInicio = Time.frameCount;
        it.ultimoEstado = Estado(it);
    }

    public static void Parar(FinalCardBoard b)
    {
        for (int i = itens.Count - 1; i >= 0; i--)
            if (itens[i].board == b || itens[i].board == null) itens.RemoveAt(i);
    }

    static string Trecho(string s)
    {
        if (s == null) return "(null)";
        s = s.Replace("\n", " ").Replace("\r", "");
        return "\"" + (s.Length > 40 ? s.Substring(0, 40) + "..." : s) + "\"(len=" + s.Length + ")";
    }

    static string Estado(Item it)
    {
        var t = it.titulo;
        if (t == null) return "titulo=NULL";
        string cor = "?";
        try { cor = t.color.a.ToString("F2"); } catch (System.Exception) { }
        var r = t.GetComponent<Renderer>();
        return "ativoHier=" + t.gameObject.activeInHierarchy + " ativoSelf=" + t.gameObject.activeSelf + " tmpEnabled=" + t.enabled +
               " rendererEnabled=" + (r != null ? r.enabled.ToString() : "n/a") + " alpha=" + cor +
               " escalaMundo=" + t.transform.lossyScale.x.ToString("F3");
    }

    void LateUpdate()
    {
        for (int i = itens.Count - 1; i >= 0; i--)
        {
            var it = itens[i];
            if (it.board == null) { itens.RemoveAt(i); continue; }
            int frames = Time.frameCount - it.frameInicio;
            float dt = Time.realtimeSinceStartup - it.tempoInicio;

            bool janela = frames <= FramesVigia;
            bool checkpoint = it.proximoCheckpoint < Checkpoints.Length && dt >= Checkpoints[it.proximoCheckpoint];
            if (!janela && !checkpoint) continue;

            Verificar(it, frames, dt, checkpoint);
            if (checkpoint) it.proximoCheckpoint++;
        }
    }

    static void Verificar(Item it, int frames, float dt, bool checkpoint)
    {
        var b = it.board;
        string quem = "board=" + b.name + " id=" + b.IdAtual + " frames_desde_enable=" + frames + " dt=" + dt.ToString("F2") + "s frame=" + Time.frameCount;

        // titulo/descricao ainda nao conhecidos pelo watcher (board nunca aplicou texto): pega pelos campos do board.
        if (it.titulo == null) it.titulo = b.titulo;
        if (it.descricao == null) it.descricao = b.descricao;

        string atualT = it.titulo != null ? it.titulo.text : null;
        string atualD = it.descricao != null ? it.descricao.text : null;

        if (it.temEsperado)
        {
            if (it.titulo != null && atualT != it.esperadoTitulo && atualT != it.ultimoAlertaTitulo)
            {
                it.ultimoAlertaTitulo = atualT;
                EmuseuDebug.Warn("diag", "TEXTO ALTERADO POR OUTRO (titulo) " + quem + " esperado=" + Trecho(it.esperadoTitulo) + " novo=" + Trecho(atualT));
            }
            if (it.descricao != null && atualD != it.esperadoDesc && atualD != it.ultimoAlertaDesc)
            {
                it.ultimoAlertaDesc = atualD;
                EmuseuDebug.Warn("diag", "TEXTO ALTERADO POR OUTRO (descricao) " + quem + " esperado=" + Trecho(it.esperadoDesc) + " novo=" + Trecho(atualD));
            }
        }
        else if (frames >= 2 && !it.avisouSemTexto)
        {
            it.avisouSemTexto = true;
            EmuseuDebug.Warn("diag", "SEM TEXTO APLICADO pelo board ainda " + quem + " titulo_atual=" + Trecho(atualT) + " (placeholder do prefab?)");
        }

        string est = Estado(it);
        if (it.ultimoEstado != null && est != it.ultimoEstado)
            EmuseuDebug.Warn("diag", "ESTADO DO TEXTO MUDOU " + quem + " antes=[" + it.ultimoEstado + "] agora=[" + est + "]");
        it.ultimoEstado = est;

        if (checkpoint)
        {
            EmuseuDebug.Info("diag", "checkpoint " + quem + " motivo=" + it.motivo +
                " titulo_atual=" + Trecho(atualT) + " esperado=" + (it.temEsperado ? Trecho(it.esperadoTitulo) : "(nenhum)") +
                " igual=" + (it.temEsperado && atualT == it.esperadoTitulo) +
                " | estado=[" + est + "] | imagem=[" + b.DiagImagem() + "]");
        }
    }
}