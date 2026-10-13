using UnityEngine;

public class RotateObject : MonoBehaviour
{
    // Variáveis públicas para definir o eixo de rotação e a velocidade
    public Vector3 rotationAxis = new Vector3(0, 1, 0); // Eixo Y por padrão
    public float rotationSpeed = 50f; // Velocidade em graus por segundo

    void Update()
    {
        // Rotaciona o objeto ao longo do eixo definido, com a velocidade multiplicada pelo tempo para suavidade
        transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime);
    }
}
