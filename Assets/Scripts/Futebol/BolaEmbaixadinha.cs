using UnityEngine;

// Faz uma bola (ex: Jabulani) acompanhar as animacoes de futebol do Mixamo.
// Coloque este script NA BOLA e arraste o personagem (Animator Humanoid) no campo "Personagem".
//  - Modo Equilibrar: a bola fica parada em cima do pe (Stall Soccerball).
//  - Modo Embaixadinha: a bola quica no pe em parabola e gira (Kick Soccerball).
// Opcional: adicione Animation Events "ChutarEsquerdo"/"ChutarDireito" no clipe
// e desmarque "Detectar Toque Automatico" para controle exato.
public class BolaEmbaixadinha : MonoBehaviour
{
    public enum Modo { Equilibrar, Embaixadinha }

    public Animator personagem;
    public Modo modo = Modo.Embaixadinha;

    [Header("Bola")]
    [Tooltip("Raio da bola em metros (bola oficial ~0.11)")]
    public float raio = 0.11f;

    [Header("Equilibrar")]
    public bool peDireito = true;
    public Vector3 ajusteEquilibrio = new Vector3(0f, 0f, 0.05f);

    [Header("Embaixadinha")]
    [Tooltip("Altura que a bola sobe acima do pe a cada toque")]
    public float alturaToque = 0.45f;
    [Tooltip("Giro da bola em voltas por segundo a cada toque")]
    public float giro = 1.5f;
    public bool detectarToqueAutomatico = true;
    public float gravidade = 9.81f;

    Transform peEsq, peDir;
    Vector3 vel, velAngular;
    Vector3 ultimoPeEsq, ultimoPeDir;
    float espera;

    void Start()
    {
        if (personagem == null) { Debug.LogWarning("BolaEmbaixadinha: falta o Personagem."); enabled = false; return; }
        peEsq = personagem.GetBoneTransform(HumanBodyBones.LeftToes) ?? personagem.GetBoneTransform(HumanBodyBones.LeftFoot);
        peDir = personagem.GetBoneTransform(HumanBodyBones.RightToes) ?? personagem.GetBoneTransform(HumanBodyBones.RightFoot);
        if (peEsq == null || peDir == null) { Debug.LogWarning("BolaEmbaixadinha: personagem precisa ser Humanoid."); enabled = false; return; }

        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true; // a bola e controlada por este script

        ultimoPeEsq = peEsq.position; ultimoPeDir = peDir.position;
        Reposicionar(peDireito ? peDir : peEsq);
    }

    void LateUpdate() // depois da animacao, para usar a pose final do pe
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (modo == Modo.Equilibrar)
        {
            Transform pe = peDireito ? peDir : peEsq;
            Vector3 alvo = PontoNoPe(pe) + personagem.transform.TransformVector(ajusteEquilibrio);
            Vector3 mov = alvo - transform.position;
            transform.position = alvo;
            Girar(mov, dt, 0.3f);
            vel = mov / dt;
        }
        else
        {
            vel += Vector3.down * gravidade * dt;
            Vector3 antes = transform.position;
            transform.position += vel * dt;
            espera -= dt;

            if (detectarToqueAutomatico && espera <= 0f && vel.y < 0f)
            {
                if (Tocou(peEsq, ultimoPeEsq, dt)) Chutar(peEsq);
                else if (Tocou(peDir, ultimoPeDir, dt)) Chutar(peDir);
            }

            // caiu no chao: volta para o pe
            if (transform.position.y < personagem.transform.position.y + raio * 0.5f)
                Reposicionar(peDir);

            Girar(transform.position - antes, dt, 1f);
        }

        ultimoPeEsq = peEsq.position; ultimoPeDir = peDir.position;
    }

    bool Tocou(Transform pe, Vector3 ultimo, float dt)
    {
        Vector3 p = PontoNoPe(pe);
        float velPe = (pe.position.y - ultimo.y) / dt;
        float dist = Vector3.Distance(transform.position, p);
        return dist < raio * 1.5f && velPe > -0.2f; // pe perto e nao descendo
    }

    // Pode ser chamado por Animation Event
    public void ChutarEsquerdo() { if (modo == Modo.Embaixadinha) Chutar(peEsq); }
    public void ChutarDireito()  { if (modo == Modo.Embaixadinha) Chutar(peDir); }

    void Chutar(Transform pe)
    {
        Vector3 p = PontoNoPe(pe);
        // subida calculada para atingir alturaToque e voltar perto do centro do personagem
        float vy = Mathf.Sqrt(2f * gravidade * alturaToque);
        float tempoNoAr = 2f * vy / gravidade;
        Vector3 centro = personagem.transform.position + personagem.transform.forward * 0.25f;
        Vector3 horizontal = new Vector3(centro.x - p.x, 0f, centro.z - p.z) * 0.5f / tempoNoAr;
        transform.position = p;
        vel = horizontal + Vector3.up * vy;

        // giro para tras (backspin) com pequena variacao, como numa embaixadinha real
        Vector3 eixo = personagem.transform.right + Random.insideUnitSphere * 0.2f;
        velAngular = eixo.normalized * giro * 360f;
        espera = tempoNoAr * 0.5f;
    }

    void Reposicionar(Transform pe)
    {
        transform.position = PontoNoPe(pe);
        vel = Vector3.zero;
        if (modo == Modo.Embaixadinha) Chutar(pe);
    }

    Vector3 PontoNoPe(Transform pe) => pe.position + Vector3.up * raio;

    void Girar(Vector3 mov, float dt, float peso)
    {
        // rolamento proporcional ao movimento + giro do toque
        Vector3 eixoRolar = Vector3.Cross(Vector3.up, mov);
        float graus = mov.magnitude / (2f * Mathf.PI * raio) * 360f * peso;
        if (eixoRolar.sqrMagnitude > 1e-8f) transform.Rotate(eixoRolar.normalized, graus, Space.World);
        transform.Rotate(velAngular * dt, Space.World);
        velAngular *= Mathf.Exp(-0.5f * dt); // perde giro devagar
    }
}
