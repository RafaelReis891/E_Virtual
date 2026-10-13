using UnityEngine;

public class Camiseta : MonoBehaviour
{
	public char letra;
	public int posicaoCorreta; // Índice do slot onde ela DEVE estar (0=F, 1=A...)
	public int indiceSlotAtual; // Em qual slot ela ESTÁ agora
	
	public Vector3 posicaoSlotCorreta; // Posição física do slot correto
}