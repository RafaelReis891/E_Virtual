using System.Collections.Generic;
using UnityEngine;

public class DragAndDropSwap : MonoBehaviour
{
	[Header("Referências")]
	public Camera mainCamera;
	public Camiseta[] camisetas;

	[Header("Configurações")]
	public float distanciaMaximaTroca = 2.0f;

	private List<Vector3> posicoesOriginais = new List<Vector3>();
	private List<Quaternion> rotacoesOriginais = new List<Quaternion>();

	private Camiseta camisaArrastada;
	private Vector3 posicaoOrigemArraste;
	private Quaternion rotacaoOrigemArraste;
	private float distanciaCameraParaPlano;

	void Start()
	{
		if (mainCamera == null) mainCamera = Camera.main;

		foreach (Camiseta c in camisetas)
		{
			posicoesOriginais.Add(c.transform.position);
			rotacoesOriginais.Add(c.transform.rotation);
		}

		EmbaralharCamisetas();
	}

	void EmbaralharCamisetas()
	{
		List<Vector3> posEmbaralhadas = new List<Vector3>(posicoesOriginais);
		List<Quaternion> rotEmbaralhadas = new List<Quaternion>(rotacoesOriginais);

		for (int i = posEmbaralhadas.Count - 1; i > 0; i--)
		{
			int randomIndex = Random.Range(0, i + 1);

			Vector3 tempPos = posEmbaralhadas[i];
			posEmbaralhadas[i] = posEmbaralhadas[randomIndex];
			posEmbaralhadas[randomIndex] = tempPos;

			Quaternion tempRot = rotEmbaralhadas[i];
			rotEmbaralhadas[i] = rotEmbaralhadas[randomIndex];
			rotEmbaralhadas[randomIndex] = tempRot;
		}

		for (int i = 0; i < camisetas.Length; i++)
		{
			camisetas[i].transform.SetPositionAndRotation(posEmbaralhadas[i], rotEmbaralhadas[i]);
		}
	}

	void Update()
	{
		// Se o jogo já acabou, não permite mais arrastar
		if (GameManager.Instance != null && GameManager.Instance.GetType().GetField("jogoFinalizado", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(GameManager.Instance).ToString() == "True")
		{
			// Nota: Uma forma mais limpa é adicionar uma propriedade `public bool JogoFinalizado { get; private set; }` no GameManager. 
			// Vou assumir que você pode adicionar `public bool JogoFinalizado { get; private set; }` no GameManager se quiser travar o arraste.
		}

		if (Input.GetMouseButtonDown(0))
		{
			Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

			if (Physics.Raycast(ray, out RaycastHit hit))
			{
				if (hit.collider.CompareTag("Quadro"))
				{
					// Chama o GameManager para mostrar a dica
					if (GameManager.Instance != null) GameManager.Instance.MostrarDica();
				}
				else if (hit.collider.CompareTag("Camiseta"))
				{
					camisaArrastada = hit.collider.GetComponent<Camiseta>();

					if (camisaArrastada != null)
					{
						posicaoOrigemArraste = camisaArrastada.transform.position;
						rotacaoOrigemArraste = camisaArrastada.transform.rotation;

						distanciaCameraParaPlano = Vector3.Distance(
							mainCamera.transform.position,
							camisaArrastada.transform.position
						);
					}
				}
			}
		}

		if (camisaArrastada != null && Input.GetMouseButton(0))
		{
			Vector3 mousePoint = Input.mousePosition;
			mousePoint.z = distanciaCameraParaPlano;

			Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePoint);

			camisaArrastada.transform.position = new Vector3(
				worldPos.x,
				worldPos.y,
				posicaoOrigemArraste.z
			);
		}

		if (camisaArrastada != null && Input.GetMouseButtonUp(0))
		{
			VerificarTrocaPorProximidade();
			camisaArrastada = null;
		}
	}

	void VerificarTrocaPorProximidade()
	{
		Camiseta camisaMaisProxima = null;
		float menorDistancia = float.MaxValue;

		foreach (Camiseta c in camisetas)
		{
			if (c != camisaArrastada)
			{
				float dist = Vector3.Distance(camisaArrastada.transform.position, c.transform.position);
				if (dist < menorDistancia)
				{
					menorDistancia = dist;
					camisaMaisProxima = c;
				}
			}
		}

		if (camisaMaisProxima != null && menorDistancia <= distanciaMaximaTroca)
		{
			Vector3 posDestino = camisaMaisProxima.transform.position;
			Quaternion rotDestino = camisaMaisProxima.transform.rotation;

			camisaArrastada.transform.SetPositionAndRotation(posDestino, rotDestino);
			camisaMaisProxima.transform.SetPositionAndRotation(posicaoOrigemArraste, rotacaoOrigemArraste);

			// 🔥 AVISA O GAME MANAGER PARA VERIFICAR A PALAVRA APÓS A TROCA
			if (GameManager.Instance != null)
			{
				GameManager.Instance.VerificarPalavraAtual();
			}
		}
		else
		{
			RetornarParaOrigem();
		}
	}

	void RetornarParaOrigem()
	{
		camisaArrastada.transform.SetPositionAndRotation(posicaoOrigemArraste, rotacaoOrigemArraste);
	}
}