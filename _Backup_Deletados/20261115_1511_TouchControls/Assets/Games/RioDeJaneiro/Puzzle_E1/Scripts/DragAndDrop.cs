using System.Collections.Generic;
using UnityEngine;

public class DragAndDropSwap : MonoBehaviour
{
	[Header("Referências")]
	public Camera mainCamera;
	public Camiseta[] camisetas;

	[Header("Configurações")]
	public float distanciaMaximaTroca = 2.0f;

	[Tooltip("Distancia maxima (em metros) da camera ate a camiseta para conseguir clicar.")]
	public float alcanceClique = 100f;

	private List<Vector3> posicoesOriginais = new List<Vector3>();
	private List<Quaternion> rotacoesOriginais = new List<Quaternion>();

	private Camiseta camisaArrastada;
	private Vector3 posicaoOrigemArraste;
	private Quaternion rotacaoOrigemArraste;
	private float distanciaCameraParaPlano;



	void Start()
	{
		if (mainCamera == null)
			mainCamera = Camera.main;

		posicoesOriginais.Clear();
		rotacoesOriginais.Clear();

		foreach (Camiseta c in camisetas)
		{
			posicoesOriginais.Add(c.transform.position);
			rotacoesOriginais.Add(c.transform.rotation);
		}

		// Registra a ordem correta antes de embaralhar.
		if (GameManager.Instance != null)
		{
			GameManager.Instance.RegistrarPosicoesIniciais(
				posicoesOriginais.ToArray()
			);
		}

		// Só depois embaralha as camisetas.
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
		if (GameManager.Instance != null &&
			GameManager.Instance.JogoFinalizado)
		{
			return;
		}

		if (Input.GetMouseButtonDown(0))
		{
			Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

			// Procura a camiseta/quadro mais proximo no raio, pulando outros colliders (ex.: Trigger_Puzzle)
			if (BuscarAlvo(ray, out RaycastHit hit))
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

			Transform pai = camisaArrastada.transform.parent;

			Vector3 localMousePos = pai != null
				? pai.InverseTransformPoint(worldPos)
				: worldPos;

			Vector3 posicaoLocal = camisaArrastada.transform.localPosition;

			// Permite movimento somente nos eixos locais Y e Z.
			camisaArrastada.transform.localPosition = new Vector3(
				posicaoLocal.x,
				localMousePos.y,
				localMousePos.z
			);
		}

		if (camisaArrastada != null && Input.GetMouseButtonUp(0))
		{
			VerificarTrocaPorProximidade();
			camisaArrastada = null;
		}
	}

	bool BuscarAlvo(Ray ray, out RaycastHit alvo)
	{
		alvo = default;
		RaycastHit[] hits = Physics.RaycastAll(ray, alcanceClique, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
		float menor = float.MaxValue;
		bool achou = false;
		foreach (RaycastHit h in hits)
		{
			if (!h.collider.CompareTag("Camiseta") && !h.collider.CompareTag("Quadro")) continue;
			if (h.distance < menor) { menor = h.distance; alvo = h; achou = true; }
		}
		return achou;
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