using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.SceneManagement;

// Garante que a intro da CenaArena TERMINE, mesmo com FPS muito baixo (WebGL).
//
// Problema: a intro e um SplineAnimate (loop = Loop, 8 s) no objeto "CameraView". Ela so termina quando a camera
// entra no trigger "Trigger_FadeinWhite" (TriggerEvent -> FadeIn + TimedEvent). Com FPS baixo a camera "pula" o
// trigger entre dois frames, a fisica nao detecta a entrada e o spline da a volta, repetindo a intro.
//
// Solucao (sem editar cena nem scripts existentes; cria-se sozinho no sceneLoaded, so para a cena "CenaArena"):
//  1. SplineAnimate.Loop = Once (a camera para no fim do spline, nao repete).
//  2. Todo LateUpdate: teste "varrido" do segmento (posicao anterior -> atual da camera) contra a esfera do trigger
//     (raio do trigger + raio do collider da camera).
//  3. Se acertou, ou se a animacao terminou (Completed / normalizedTime >= 0.99), invoca UMA vez
//     TriggerEvent._OnTriggerEnter com o collider da camera - o mesmo efeito da fisica.
//  4. Se a fisica normal ja disparou o evento (detectado por um listener runtime no proprio _OnTriggerEnter),
//     nao invoca de novo.
// Se CameraView ou Trigger_FadeinWhite estiverem inativos (ex.: PularIntroArena pulou a intro), nao faz nada.
// Logs com prefixo [IntroArena].
public sealed class IntroArenaGarantirFim : MonoBehaviour
{
    const string NomeCena = "CenaArena";
    const string NomeCamera = "CameraView";
    const string NomeTrigger = "Trigger_FadeinWhite";
    const float FimNormalizado = 0.99f;

    static bool assinado;

    SplineAnimate spline;
    TriggerEvent triggerEvent;
    Collider triggerCol;
    Collider cameraCol;
    Transform cameraTr;
    Transform triggerTr;

    bool fisicaDisparou;      // _OnTriggerEnter foi invocado por qualquer um (fisica ou nos)
    bool nosDisparamos;
    bool completouPendente;   // evento Completed do spline
    bool temAnterior;
    Vector3 posAnterior;
    int frames;
    float inicio;
    float menorDist = float.MaxValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { assinado = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Assinar()
    {
        if (assinado) return;
        assinado = true;
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    // Cobre tambem o caso da cena inicial ja estar carregada quando este metodo roda (ex.: Play direto na CenaArena).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void VerificarCenaAtiva()
    {
        Scene s = SceneManager.GetActiveScene();
        if (s.IsValid() && s.isLoaded) AoCarregarCena(s, LoadSceneMode.Single);
    }

    static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (cena.name != NomeCena) return;

        // evita criar duas vezes (BeforeSceneLoad + AfterSceneLoad)
        foreach (GameObject root in cena.GetRootGameObjects())
            if (root.GetComponent<IntroArenaGarantirFim>() != null) return;

        GameObject camGo = Achar(cena, NomeCamera);
        GameObject trigGo = Achar(cena, NomeTrigger);
        if (camGo == null || trigGo == null)
        {
            Debug.Log("[IntroArena] nao achei " + (camGo == null ? NomeCamera : NomeTrigger) + " - nada a fazer");
            return;
        }
        if (!camGo.activeInHierarchy || !trigGo.activeInHierarchy)
        {
            Debug.Log("[IntroArena] " + NomeCamera + " ou " + NomeTrigger + " inativo (intro pulada?) - nada a fazer");
            return;
        }

        SplineAnimate sa = camGo.GetComponent<SplineAnimate>();
        TriggerEvent te = trigGo.GetComponent<TriggerEvent>();
        Collider tc = trigGo.GetComponent<Collider>();
        if (sa == null || te == null || tc == null)
        {
            Debug.LogWarning("[IntroArena] componentes faltando: SplineAnimate=" + (sa != null) + " TriggerEvent=" + (te != null) + " Collider=" + (tc != null));
            return;
        }

        var go = new GameObject("IntroArenaGarantirFim");
        SceneManager.MoveGameObjectToScene(go, cena); // morre junto com a cena
        var comp = go.AddComponent<IntroArenaGarantirFim>();
        comp.Iniciar(sa, te, tc, camGo);
    }

    static GameObject Achar(Scene cena, string nome)
    {
        foreach (GameObject root in cena.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t.gameObject;
        return null;
    }

    void Iniciar(SplineAnimate sa, TriggerEvent te, Collider tc, GameObject camGo)
    {
        spline = sa;
        triggerEvent = te;
        triggerCol = tc;
        triggerTr = tc.transform;
        cameraTr = camGo.transform;
        cameraCol = camGo.GetComponent<Collider>();
        if (cameraCol == null) cameraCol = camGo.GetComponentInChildren<Collider>();
        inicio = Time.realtimeSinceStartup;

        if (cameraCol == null)
        {
            Debug.LogWarning("[IntroArena] CameraView sem Collider - nao da para invocar o trigger; desativando");
            Destroy(gameObject);
            return;
        }

        te._OnTriggerEnter.AddListener(AoTriggerEnter);   // listener de runtime: marca quando a fisica (ou nos) disparou
        spline.Completed += AoCompletar;

        SplineAnimate.LoopMode antes = spline.Loop;
        spline.Loop = SplineAnimate.LoopMode.Once;
        Debug.Log("[IntroArena] ativo: loop " + antes + " -> Once, duracao=" + spline.Duration.ToString("0.0")
                  + "s, trigger raio=" + RaioTrigger().ToString("0.00") + " centro=" + CentroTrigger().ToString("F1")
                  + ", camera raio=" + RaioCamera().ToString("0.00"));
    }

    void AoTriggerEnter(Collider c)
    {
        // chamado em QUALQUER invocacao do _OnTriggerEnter (fisica normal ou a nossa)
        if (!fisicaDisparou)
        {
            fisicaDisparou = true;
            if (!nosDisparamos)
                Debug.Log("[IntroArena] fisica normal disparou o trigger (t=" + (Time.realtimeSinceStartup - inicio).ToString("0.00")
                          + "s, " + frames + " frames) - nada a forcar");
        }
    }

    void AoCompletar() { completouPendente = true; }

    void OnDestroy()
    {
        if (spline != null) spline.Completed -= AoCompletar;
        if (triggerEvent != null) triggerEvent._OnTriggerEnter.RemoveListener(AoTriggerEnter);
    }

    Vector3 CentroTrigger()
    {
        var sc = triggerCol as SphereCollider;
        return sc != null ? triggerTr.TransformPoint(sc.center) : triggerCol.bounds.center;
    }

    float RaioTrigger()
    {
        var sc = triggerCol as SphereCollider;
        if (sc != null)
        {
            Vector3 s = triggerTr.lossyScale;
            return sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
        Vector3 e = triggerCol.bounds.extents;
        return Mathf.Max(e.x, e.y, e.z);
    }

    float RaioCamera()
    {
        var sc = cameraCol as SphereCollider;
        if (sc != null)
        {
            Vector3 s = cameraCol.transform.lossyScale;
            return sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
        Vector3 e = cameraCol.bounds.extents;
        return Mathf.Max(e.x, e.y, e.z);
    }

    // menor distancia do ponto p ao segmento a-b
    static float DistSegmento(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-12f) return Vector3.Distance(a, p);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return Vector3.Distance(a + ab * t, p);
    }

    void LateUpdate()
    {
        // Intro ja acabou por outro caminho (TimedEvent desativou CameraView/trigger, PularIntro, etc.)
        if (spline == null || triggerEvent == null || cameraCol == null ||
            !spline.gameObject.activeInHierarchy || !triggerEvent.gameObject.activeInHierarchy)
        {
            Debug.Log("[IntroArena] CameraView/trigger inativo - encerrando (disparou fisica=" + fisicaDisparou + ", forcado=" + nosDisparamos
                      + ", frames=" + frames + ", t=" + (Time.realtimeSinceStartup - inicio).ToString("0.00") + "s)");
            Destroy(gameObject);
            return;
        }

        frames++;
        Vector3 atual = cameraTr.position;
        if (!temAnterior) { posAnterior = atual; temAnterior = true; }

        if (fisicaDisparou) { Destroy(gameObject); return; } // trabalho feito pela fisica

        Vector3 centro = CentroTrigger();
        float alcance = RaioTrigger() + RaioCamera();
        float dist = DistSegmento(posAnterior, atual, centro);
        if (dist < menorDist) menorDist = dist;

        bool acertou = dist <= alcance;
        bool terminou = completouPendente || spline.NormalizedTime >= FimNormalizado;

        if (acertou || terminou)
        {
            string motivo = acertou ? "varredura acertou o trigger" : "animacao terminou sem passar pelo trigger";
            Disparar(motivo, dist, alcance);
            return;
        }
        posAnterior = atual;
    }

    void Disparar(string motivo, float dist, float alcance)
    {
        nosDisparamos = true;
        Debug.Log("[IntroArena] forcando fim da intro: " + motivo + " (dist=" + dist.ToString("0.00") + " alcance=" + alcance.ToString("0.00")
                  + " menorDist=" + menorDist.ToString("0.00") + " normT=" + spline.NormalizedTime.ToString("0.000")
                  + " frames=" + frames + " t=" + (Time.realtimeSinceStartup - inicio).ToString("0.00") + "s fps~" + (frames / Mathf.Max(0.001f, Time.realtimeSinceStartup - inicio)).ToString("0.0") + ")");

        // se o TriggerEvent esta em playOnce, marca como ja disparado (campo privado) para a fisica nao repetir
        if (triggerEvent.playOnce)
        {
            try
            {
                FieldInfo f = typeof(TriggerEvent).GetField("hasTriggered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) f.SetValue(triggerEvent, true);
            }
            catch (Exception) { }
        }

        if (triggerEvent._OnTriggerEnter != null) triggerEvent._OnTriggerEnter.Invoke(cameraCol);
        Destroy(gameObject);
    }
}