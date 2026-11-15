// MobileInput.Arraste.cs
// Arrastar pecas no celular: "toca para pegar, a peca segue, toca de novo para soltar".
// No PC (MobileInput.Ativo == false) todas as funcoes devolvem o valor original, nada muda.
//
// Como usar num puzzle de arrastar (troca so as leituras de mouse):
//     if (MobileInput.ToqueLivre(Input.GetMouseButtonDown(0)))        -> pegar (ou MobileInput.Apertou(...))
//     Vector3 p = MobileInput.PosicaoArraste(Input.mousePosition);     -> onde a peca deve ir
//     if (peca != null && MobileInput.BotaoSegurado(Input.GetMouseButton(0)))   -> mover
//     if (peca != null && MobileInput.Soltou(Input.GetMouseButtonUp(0)))        -> soltar
//     if (MobileInput.SegurandoPeca && encaixouCerto) MobileInput.SoltarPeca(); -> solta sozinho no lugar certo
//
// Regras no mobile:
// - Pegar: toque no mundo (fora dos joysticks/botao) ou o botao Interagir (usa a mira do centro).
// - Enquanto o dedo que pegou continua na tela, a peca segue o dedo.
//   Se o dedo arrastou bastante e saiu da tela, solta ali (arrastar normal). Se foi so um toque, a peca fica na mao.
// - Com a peca na mao: o proximo toque no mundo leva a peca ate o dedo e solta quando o dedo sai da tela.
//   O botao Interagir tambem solta. Toques nos joysticks nao soltam (da para andar e girar a camera segurando).
// - Pegou pelo botao Interagir: a peca segue a mira (centro da tela) ate soltar.
using UnityEngine;

public static partial class MobileInput
{
    /// <summary>Se true, arrastar o dedo e tirar da tela tambem solta (alem do toque-toque).</summary>
    public static bool SoltarAoTerminarArraste = true;
    /// <summary>Quanto o dedo precisa andar (fracao do menor lado da tela) para contar como "arrastou".</summary>
    public static float FracaoArraste = 0.05f;

    static int idToqueMundo = MobileVirtualJoystick.SemDedo;

    static bool segurando;        // um puzzle confirmou que tem a peca (chamou BotaoSegurado)
    static bool candidato;        // toque de pegar neste frame, esperando o puzzle confirmar
    static int frameCandidato = -1;
    static int ultimoSegurado = -1;
    static int frameSoltou = -1;
    static bool viaBotao;         // pegou com o botao Interagir: segue a mira
    static int dedoArraste = MobileVirtualJoystick.SemDedo;
    static bool dedoDeSoltar;     // o dedo atual e o "segundo toque" (solta quando sair da tela)
    static bool moveuMuito;
    static Vector2 inicioDedo;
    static Vector2 posArraste;

    /// <summary>True no mobile enquanto tem uma peca na mao (ou acabou de tocar para pegar).</summary>
    public static bool SegurandoPeca { get { if (!Ativo) return false; Processar(); return segurando || candidato; } }

    /// <summary>Troca de Input.GetMouseButtonDown(0) para pegar a peca. No PC devolve o original.</summary>
    public static bool Apertou(bool original)
    {
        if (!Ativo) return original;
        Processar();
        return candidato && frameCandidato == Time.frameCount;
    }

    /// <summary>Troca de Input.GetMouseButton(0) enquanto arrasta. No PC devolve o original.</summary>
    public static bool BotaoSegurado(bool original)
    {
        if (!Ativo) return original;
        Processar();
        int f = Time.frameCount;
        if (frameSoltou == f) return true; // ultimo movimento antes de soltar
        if (candidato || segurando)
        {
            segurando = true;
            candidato = false;
            ultimoSegurado = f;
            return true;
        }
        return false;
    }

    /// <summary>Troca de Input.GetMouseButtonUp(0) ao soltar. No PC devolve o original.</summary>
    public static bool Soltou(bool original)
    {
        if (!Ativo) return original;
        Processar();
        return frameSoltou == Time.frameCount;
    }

    /// <summary>Troca de Input.mousePosition no arraste. No PC devolve o original.</summary>
    public static Vector3 PosicaoArraste(Vector3 original)
    {
        if (!Ativo) return original;
        Processar();
        if (viaBotao && dedoArraste == MobileVirtualJoystick.SemDedo)
            return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, original.z);
        return new Vector3(posArraste.x, posArraste.y, original.z);
    }

    /// <summary>O puzzle chama quando a peca encaixou certo: Soltou() fica true neste frame.</summary>
    public static void SoltarPeca()
    {
        if (!Ativo) return;
        Processar();
        if (segurando || candidato) Liberar();
    }

    /// <summary>Enquanto segura uma peca, o toque e do arraste (Clique() nao dispara outros scripts).</summary>
    static bool ArrasteUsaToque()
    {
        return segurando || frameSoltou == Time.frameCount;
    }

    static void Liberar()
    {
        frameSoltou = Time.frameCount;
        segurando = false;
        candidato = false;
        dedoArraste = MobileVirtualJoystick.SemDedo;
        dedoDeSoltar = false;
    }

    static void LimparArraste()
    {
        segurando = false;
        candidato = false;
        frameCandidato = -1;
        ultimoSegurado = -1;
        frameSoltou = -1;
        viaBotao = false;
        dedoArraste = MobileVirtualJoystick.SemDedo;
        dedoDeSoltar = false;
        moveuMuito = false;
        idToqueMundo = MobileVirtualJoystick.SemDedo;
    }

    /// <summary>Chamado no fim do Processar(), uma vez por frame.</summary>
    static void AtualizarArraste()
    {
        int f = Time.frameCount;

        // toque de pegar que nenhum puzzle confirmou (nao acertou peca)
        if (candidato && frameCandidato != f)
        {
            candidato = false;
            if (!segurando) { dedoArraste = MobileVirtualJoystick.SemDedo; viaBotao = false; }
        }
        // o puzzle parou de perguntar (resolvido, desativado, trocou de cena)
        if (segurando && f - ultimoSegurado > 2) LimparArraste();

        // dedo que esta levando a peca
        if (dedoArraste != MobileVirtualJoystick.SemDedo)
        {
            bool achou = false, terminou = false;
            for (int i = 0; i < pontos.Count; i++)
            {
                if (pontos[i].id != dedoArraste) continue;
                achou = true;
                posArraste = pontos[i].pos;
                float lim = Mathf.Min(Screen.width, Screen.height) * FracaoArraste;
                if ((posArraste - inicioDedo).sqrMagnitude > lim * lim) moveuMuito = true;
                if (pontos[i].terminou) terminou = true;
                break;
            }
            if (!achou || terminou)
            {
                bool soltar = dedoDeSoltar || (moveuMuito && SoltarAoTerminarArraste);
                dedoArraste = MobileVirtualJoystick.SemDedo;
                dedoDeSoltar = false;
                if (segurando && soltar) Liberar();
            }
        }

        if (frameSoltou == f) return; // ja soltou neste frame

        // novo gesto
        if (interagirNoFrame)
        {
            if (segurando) Liberar();
            else
            {
                candidato = true;
                frameCandidato = f;
                viaBotao = true;
                dedoArraste = MobileVirtualJoystick.SemDedo;
                posArraste = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
        }
        else if (temToqueMundo)
        {
            if (segurando)
            {
                if (dedoArraste == MobileVirtualJoystick.SemDedo)
                {
                    // segundo toque: leva a peca ate o dedo e solta quando o dedo sair
                    dedoArraste = idToqueMundo;
                    dedoDeSoltar = true;
                    viaBotao = false;
                    moveuMuito = false;
                    inicioDedo = toqueMundo;
                    posArraste = toqueMundo;
                }
            }
            else
            {
                candidato = true;
                frameCandidato = f;
                viaBotao = false;
                dedoArraste = idToqueMundo;
                dedoDeSoltar = false;
                moveuMuito = false;
                inicioDedo = toqueMundo;
                posArraste = toqueMundo;
            }
        }
    }
}
