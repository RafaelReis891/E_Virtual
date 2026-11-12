using UnityEngine;
using UnityEngine.SceneManagement;

// Coloque em qualquer objeto da CenaArena.
// A 1a vez que a CenaArena abre, a animacao inicial toca normal.
// Nas proximas vezes (ex: voltando do Rio pelo portal), pula direto para o player no campo.
public class PularIntroArena : MonoBehaviour
{
    [Tooltip("Ligado = sempre pula a intro (bom para testar).")]
    public bool sempre = false;

    public static bool introVista;          // fica true depois da 1a vez
    public static bool pularProxima;        // pode ser ligado por outro script antes do LoadScene

    void Awake()
    {
        bool pular = sempre || introVista || pularProxima;
        pularProxima = false;
        introVista = true;
        if (!pular) return;

        // Cancela a parada no fim da intro (IntroCameraHold)
        IntroCameraHold.Cancelar(gameObject.scene);

        // Mesmo resultado do fim da intro (Trigger_FadeinWhite)
        Ligar("Player_Things", true);
        Ligar("CameraView", false);
        DesligarCamerasIntro();
        Ligar("Trigger_FadeinWhite", false);
        Ligar("FadeIn", false);
        Ligar("FadeOut", true);
        Debug.Log("[PularIntroArena] intro pulada");
    }

    void Ligar(string nome, bool ativo)
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) { t.gameObject.SetActive(ativo); return; }
        Debug.LogWarning("[PularIntroArena] nao achei " + nome, this);
    }

    // Desliga "CameraView (1)" e qualquer outra camera de intro (CameraView* com Animator IntroCamAnimation)
    void DesligarCamerasIntro()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "CameraView (1)" || IntroCameraHold.EhCameraIntro(t.gameObject))
                {
                    if (t.gameObject.activeSelf) Debug.Log("[PularIntroArena] desligando camera da intro " + t.name);
                    t.gameObject.SetActive(false);
                }
    }
}
