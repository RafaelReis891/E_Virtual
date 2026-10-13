using UnityEngine;

public class Slot : MonoBehaviour
{
	public int posicaoEsperada; // 0=F, 1=A, 2=V...
	public Camiseta camisetaAtual;

	public bool EstaVazio() => camisetaAtual == null;

	public bool Verificar(Camiseta c)
	{
		return c.posicaoCorreta == posicaoEsperada;
	}
}