using UnityEngine;

// Faz o objeto sempre virar para a camera do jogador, sem girar junto com o pai.
// Texto: vira na hora (ex: TextoNumero da trigger).
// Personagem: vira de frente, devagar, so no eixo Y e so quando o jogador esta perto.
// Custo muito baixo.
public class OlharParaCamera : MonoBehaviour
{
    public enum Tipo { Texto, Personagem }

    [Tooltip("Texto = vira na hora. Personagem = vira de frente para o jogador, suave, quando ele chega perto.")]
    public Tipo tipo = Tipo.Texto;

    // Ajustes fixos do modo Personagem
    const float velocidadeGiro = 4f;     // quanto maior, mais rapido vira
    const float distanciaMaxima = 8f;    // so vira se o jogador estiver ate esta distancia

    Camera cam;

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 dir = transform.position - cam.transform.position;
        dir.y = 0f; // sempre em pe
        if (dir.sqrMagnitude < 0.0001f) return;

        if (tipo == Tipo.Texto)
        {
            transform.rotation = Quaternion.LookRotation(dir);
            return;
        }

        // Personagem: so vira se o jogador estiver perto
        if (dir.magnitude > distanciaMaxima) return;
        Quaternion alvo = Quaternion.LookRotation(-dir); // de frente para o jogador
        transform.rotation = Quaternion.Slerp(transform.rotation, alvo, velocidadeGiro * Time.deltaTime);
    }
}