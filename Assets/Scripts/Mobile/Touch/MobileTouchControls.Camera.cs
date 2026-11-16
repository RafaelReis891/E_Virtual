// MobileTouchControls.Camera.cs
// Camera do joystick direito no celular: gira o jogador (lados) e o CinemachineCameraTarget (cima/baixo) DIRETO, aqui.
// Por que: o FirstPersonController so gira quando look.sqrMagnitude >= 0.01 (|look| >= 0.1 grau por frame).
// Em sensibilidade baixa (ou joystick pouco empurrado, ou FPS alto) o valor por frame fica perto desse limite e
// a camera liga/desliga frame a frame (parece uma pausa/travadinha). Girando direto nao existe esse limite,
// e a velocidade fica proporcional a sensibilidade (0.2 .. 3.0) em qualquer FPS.
// - O pitch usa o mesmo campo do FirstPersonController (_cinemachineTargetPitch), entao o mouse do PC continua de onde parou.
// - Se algo faltar (campo nao encontrado, sem CinemachineCameraTarget), volta para o jeito antigo (StarterAssetsInputs.look).
// - Desligar: MobileTouchControls.GirarCameraDiretoAtivo = false.
// - PC sem controles de toque: nada disso roda.
using System.Reflection;
using UnityEngine;
using StarterAssets;

public partial class MobileTouchControls
{
    /// <summary>true = gira a camera direto (suave). false = usa o StarterAssetsInputs.look (jeito antigo).</summary>
    public static bool GirarCameraDiretoAtivo = true;

    static FieldInfo campoPitch;
    static bool campoPitchBuscado;
    bool girouDireto; // este frame a camera foi girada direto (entao o look do FPC tem que ficar zerado)

    /// <summary>Graus por segundo (x = lados, y = pitch do FirstPersonController) para o valor do joystick, ja com curva e sensibilidade.</summary>
    Vector2 GrausPorSegundoOlhar(Vector2 v)
    {
        float m = v.magnitude;
        if (m < 0.0001f) return Vector2.zero;
        Vector2 dir = v / m;
        float c = Mathf.Pow(Mathf.Clamp01(m), Mathf.Max(0.1f, curvaResposta));
        float y = inverterY ? dir.y : -dir.y; // pitch do StarterAssets: negativo = olhar para cima
        Vector2 grausPorSeg = new Vector2(dir.x * c * grausPorSegundoX, y * c * grausPorSegundoY);
        return grausPorSeg * MobileInput.SensibilidadeCamera; // ajuste < > na tela (1.0 = padrao)
    }

    /// <summary>Gira jogador e camera neste frame. Devolve false se nao deu (usa o jeito antigo).</summary>
    bool GirarCameraDireto(Vector2 v)
    {
        girouDireto = GirarCameraDiretoInterno(v);
        return girouDireto;
    }

    bool GirarCameraDiretoInterno(Vector2 v)
    {
        if (!GirarCameraDiretoAtivo || fpc == null) return false;
        GameObject alvo = fpc.CinemachineCameraTarget;
        if (alvo == null) return false;
        if (!campoPitchBuscado)
        {
            campoPitchBuscado = true;
            campoPitch = typeof(FirstPersonController).GetField("_cinemachineTargetPitch", BindingFlags.Instance | BindingFlags.NonPublic);
            if (campoPitch == null || campoPitch.FieldType != typeof(float))
            {
                campoPitch = null;
                Debug.LogWarning("[MobileTouch] campo _cinemachineTargetPitch nao encontrado: camera volta para o jeito antigo (StarterAssetsInputs.look)");
            }
        }
        if (campoPitch == null) return false;

        float dt = Time.deltaTime;
        if (dt <= 0f) return true; // jogo parado: nao gira, mas tambem nao manda look

        Vector2 graus = GrausPorSegundoOlhar(v) * dt;
        if (graus == Vector2.zero) return true;

        float pitch = (float)campoPitch.GetValue(fpc);
        pitch = Mathf.Clamp(pitch + graus.y, fpc.BottomClamp, fpc.TopClamp);
        campoPitch.SetValue(fpc, pitch);
        alvo.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        fpc.transform.Rotate(Vector3.up * graus.x);
        return true;
    }

    /// <summary>
    /// Chamado no LateUpdate (antes do LateUpdate do FirstPersonController, ordem -1000).
    /// O Input System pode escrever o Pointer delta (arraste do dedo) no look depois do nosso Update;
    /// aqui zera de novo para so o joystick girar a camera. Se estiver no jeito antigo com o joystick em uso, nao mexe.
    /// </summary>
    void ZerarLookAntesDoFPC()
    {
        if (!MobileInput.JogadorPronto || sai == null) return;
        if (MobileInput.Direito.Ativo && !girouDireto) return;
        if (sai.look != Vector2.zero) sai.LookInput(Vector2.zero);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetarCamera()
    {
        campoPitch = null;
        campoPitchBuscado = false;
    }
}
