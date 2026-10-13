
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Linq;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance;

	[Header("Configuração do Puzzle")]
	public string palavraCorreta = "FAVELÃO";
	public string palavraFormada = "";
	public Camiseta[] camisetas;

	private Vector3[] posicoesIniciais;

	[Header("Ordem da Curva")]
	[Tooltip("Crie um ponto para cada posição, na ordem de leitura da palavra.")]
	public Transform[] pontosOrdenacao;

	[Header("Elementos da Cena")]
	public GameObject telaVitoria;
	public Text textoDica;
	public GameObject painelDicaObjeto;

	private bool jogoFinalizado;
	public bool JogoFinalizado => jogoFinalizado;

	public UnityEvent OnCompleteGame;
	public UnityEvent OnPassNextScene;

	void Awake()
	{
		if (Instance == null)
			Instance = this;
		else
			Destroy(gameObject);
	}

	void Start()
	{
		if (telaVitoria != null)
			telaVitoria.SetActive(false);

		if (painelDicaObjeto != null)
			painelDicaObjeto.SetActive(false);
	}

	public void RegistrarPosicoesIniciais(Vector3[] posicoes)
	{
		posicoesIniciais = (Vector3[])posicoes.Clone();

		Debug.Log("Posições originais das camisetas registradas: " +
				  posicoesIniciais.Length);
	}

	public void MostrarDica()
	{
		if (textoDica != null)
			textoDica.text =
				"A Taça das Favelas, carinhosamente conhecida como 'FAVELÃO'...";

		if (painelDicaObjeto != null)
			painelDicaObjeto.SetActive(true);
	}


	public void VerificarPalavraAtual()
	{
		if (jogoFinalizado)
			return;

		if (camisetas == null || camisetas.Length == 0)
			return;

		if (posicoesIniciais == null ||
			posicoesIniciais.Length != camisetas.Length)
		{
			Debug.LogWarning("Posições iniciais ainda não registradas.");
			return;
		}

		// Cada índice representa uma posição da palavra original.
		List<Camiseta> camisetasDisponiveis = camisetas
			.Where(c => c != null)
			.ToList();

		StringBuilder builder = new StringBuilder();

		for (int i = 0; i < posicoesIniciais.Length; i++)
		{
			Camiseta maisProxima = null;
			float menorDistancia = float.MaxValue;

			foreach (Camiseta camiseta in camisetasDisponiveis)
			{
				float distancia = Vector3.Distance(
					camiseta.transform.position,
					posicoesIniciais[i]
				);

				if (distancia < menorDistancia)
				{
					menorDistancia = distancia;
					maisProxima = camiseta;
				}
			}

			if (maisProxima == null)
			{
				Debug.LogWarning($"Não foi possível identificar a posição {i}.");
				return;
			}

			builder.Append(maisProxima.letra);
			camisetasDisponiveis.Remove(maisProxima);
		}

		palavraFormada = builder.ToString();

		string formadaNormalizada = NormalizarPalavra(palavraFormada);
		string corretaNormalizada = NormalizarPalavra(palavraCorreta);

		Debug.Log(
			$"Palavra formada: {palavraFormada} | " +
			$"Palavra esperada: {palavraCorreta}"
		);

		if (formadaNormalizada == corretaNormalizada)
		{
			jogoFinalizado = true;
			Invoke(nameof(AcionarVitoria), 5f);
		}
	}


	string NormalizarPalavra(string palavra)
	{
		if (string.IsNullOrEmpty(palavra))
			return "";

		string normalizada = palavra.Trim()
			.ToUpperInvariant()
			.Normalize(NormalizationForm.FormD);

		StringBuilder resultado = new StringBuilder();

		foreach (char c in normalizada)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) !=
				UnicodeCategory.NonSpacingMark)
			{
				resultado.Append(c);
			}
		}

		return resultado.ToString()
			.Normalize(NormalizationForm.FormC)
			.Replace(" ", "");
	}

	void AcionarVitoria()
	{
		Debug.Log("Parabéns! Você montou " + palavraCorreta + "!");

		if (telaVitoria != null)
			telaVitoria.SetActive(true);

		OnCompleteGame?.Invoke();

		Invoke(nameof(PassNextSceneEvent), 6f);
	}

	void PassNextSceneEvent()
	{
		OnPassNextScene?.Invoke();
	}
}
