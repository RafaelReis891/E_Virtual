using System;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Clique do admin (root) em banners: raio do centro da tela (cursor travado, FPS com mira) ou do mouse (cursor livre).
// Criado sozinho pelo RemoteBanner quando o usuario e admin. Um unico Update para a cena toda.
// Aceita os dois sistemas de input (Input System novo e Input legado). Cada clique gera log [banner] de diagnostico.
public sealed class RemoteBannerClickDriver : MonoBehaviour
{
    public static float DistanciaMaxima = 120f;

    static RemoteBannerClickDriver instancia;
    bool avisouSemCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { instancia = null; }

    public static void Garantir()
    {
        if (instancia != null) return;
        var go = new GameObject("RemoteBannerClickDriver");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<RemoteBannerClickDriver>();
        EmuseuDebug.Info("banner", "ClickDriver criado (admin=" + EmuseuSettingsCatalog.IsAdmin + ")");
    }

    void Update()
    {
        string origem;
        if (!CliqueNesteFrame(out origem)) return;

        bool admin = EmuseuSettingsCatalog.IsAdmin;
        EmuseuDebug.Info("banner", "clique detectado (" + origem + ") admin=" + admin + " cursor=" + Cursor.lockState);
        if (!admin) return;

        Camera cam = Camera.main;
        if ((cam == null || !cam.isActiveAndEnabled) && Camera.allCamerasCount > 0)
        {
            cam = null;
            foreach (Camera c in Camera.allCameras)
                if (c != null && c.isActiveAndEnabled && c.targetTexture == null) { cam = c; break; }
        }
        if (cam == null)
        {
            if (!avisouSemCamera) { avisouSemCamera = true; EmuseuDebug.Warn("banner", "clique: nenhuma camera ativa encontrada"); }
            return;
        }

        bool travado = Cursor.lockState == CursorLockMode.Locked;
        Ray ray = travado
            ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : cam.ScreenPointToRay(MousePos());

        RaycastHit[] hits = Physics.RaycastAll(ray, DistanciaMaxima, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            EmuseuDebug.Info("banner", "clique: raio (" + cam.name + ", " + (travado ? "centro" : "mouse") + ") nao acertou nenhum collider em " + DistanciaMaxima + "m");
            return;
        }
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var sb = new StringBuilder("clique: raio (" + cam.name + ", " + (travado ? "centro" : "mouse") + ") acertou:");
        int n = Math.Min(hits.Length, 6);
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            sb.Append("\n   ").Append(i).Append(' ').Append(c.name).Append(" d=").Append(hits[i].distance.ToString("0.0"))
              .Append(" layer=").Append(LayerMask.LayerToName(c.gameObject.layer)).Append(c.isTrigger ? " [trigger]" : "");
        }
        EmuseuDebug.Info("banner", sb.ToString());

        Transform raizCamera = cam.transform.root;
        foreach (RaycastHit hit in hits)
        {
            Collider c = hit.collider;
            if (c == null) continue;
            RemoteBanner rb = c.GetComponentInParent<RemoteBanner>();
            if (rb != null)
            {
                EmuseuDebug.Info("banner", "clique: banner '" + rb.Chave + "' a " + hit.distance.ToString("0.0") + "m -> pedindo upload");
                rb.SolicitarUpload();
                return;
            }
            if (c.isTrigger) continue;                                           // gatilhos nao bloqueiam a mira
            if (c.GetComponentInParent<CharacterController>() != null) continue; // o proprio jogador
            if (c.transform.root == raizCamera) continue;                        // collider junto da camera/jogador
            if (Invisivel(c)) continue;                                          // parede invisivel (Limite_*, sem renderer ou renderer desligado)
            EmuseuDebug.Warn("banner", "clique: '" + c.name + "' (" + hit.distance.ToString("0.0") + "m) esta na frente e bloqueou a mira");
            return;
        }
        EmuseuDebug.Info("banner", "clique: nenhum RemoteBanner na linha de mira");
    }

    // Collider "fantasma": nome Limite_/Trigger_ ou o Renderer (do proprio objeto, senao do pai) nao existe/esta desligado.
    // Esses colliders nao bloqueiam a mira do admin (continuam bloqueando o jogador normalmente).
    static bool Invisivel(Collider c)
    {
        string n = c.name;
        if (n.StartsWith("Limite_", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Trigger_", StringComparison.OrdinalIgnoreCase)) return true;
        Renderer r = c.GetComponent<Renderer>();
        if (r == null) r = c.GetComponentInParent<Renderer>();
        return r == null || !r.enabled;
    }
    static bool CliqueNesteFrame(out string origem)
    {
        origem = null;
#if ENABLE_INPUT_SYSTEM
        try
        {
            Mouse m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) { origem = "InputSystem"; return true; }
        }
        catch (Exception) { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.GetMouseButtonDown(0)) { origem = "Input legado"; return true; }
        }
        catch (Exception) { }
#endif
        return false;
    }

    static Vector3 MousePos()
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            Mouse m = Mouse.current;
            if (m != null) { Vector2 p = m.position.ReadValue(); return new Vector3(p.x, p.y, 0f); }
        }
        catch (Exception) { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
#endif
    }
}