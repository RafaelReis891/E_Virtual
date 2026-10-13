using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.Events; // Necessário para usar o OrderBy

public class GameManager : MonoBehaviour
{
	public static GameManager Instance; // Permite que outros scripts chamem este facilmente

	[Header("Configuração do Puzzle")]
	public string palavraCorreta = "FAVELAO";
	public string palavraFormada = "";
	public Camiseta[] camisetas; // Arraste as 8 camisetas aqui no Inspector

	[Header("Elementos da Cena")]
	public GameObject telaVitoria;
	public Text textoDica;
	public GameObject painelDicaObjeto; // O painel que contém o texto da dica

	private bool jogoFinalizado = false;

	public UnityEvent OnCompleteGame;
	public UnityEvent OnPassNextScene;

	void Awake()
	{
		// Padrão Singleton para acessar de qualquer lugar
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	void Start()
	{
		if (telaVitoria != null) telaVitoria.SetActive(false);
		if (painelDicaObjeto != null) painelDicaObjeto.SetActive(false);
	}

	// Chamado quando clica no quadro
	public void MostrarDica()
	{
		if (textoDica != null)
		{
			textoDica.text = "A Taça das Favelas, carinhosamente conhecida como 'FAVELÃO'...";
		}
		if (painelDicaObjeto != null)
		{
			painelDicaObjeto.SetActive(true);
		}
	}

	// 🔥 Este método será chamado pelo DragAndDrop após cada troca bem-sucedida
	public void VerificarPalavraAtual()
	{
		if (jogoFinalizado) return;

		// 1. Ordena as camisetas da esquerda para a direita (baseado no eixo X)
		var camisetasOrdenadas = camisetas.OrderBy(c => c.transform.position.x).ToList();

		// 2. Monta a string com as letras na ordem atual
		palavraFormada = "";
		foreach (Camiseta c in camisetasOrdenadas)
		{
			palavraFormada += c.letra;
		}

		// (Opcional) Mostra no console para você debugar
		Debug.Log("Palavra atual formada: " + palavraFormada);

		// 3. Compara com a palavra correta
		if (palavraFormada == palavraCorreta)
		{
			jogoFinalizado = true;
			Invoke(nameof(AcionarVitoria),3f);
		}
	}

	void AcionarVitoria()
	{
		Debug.Log("🎉 PARABÉNS! Você montou FAVELÃO!");

		if (telaVitoria != null)
		{
			telaVitoria.SetActive(true);
			OnCompleteGame.Invoke();
		}

		Invoke(nameof(PassNextSceneEvent), 3f);
	}

	void PassNextSceneEvent()
	{
		OnPassNextScene.Invoke();
	}
}