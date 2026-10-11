using System.Collections.Generic;
using UnityEngine;

public class PathReader : MonoBehaviour
{
	[Header("Path")]
	public List<Transform> Path = new List<Transform>();

	[Header("Progressão")]
	public int index = 0;
	public float distanceToReach = 10f;
	public int visibleWays = 3;

	[Header("Seta de Direção")]
	public RectTransform directionArrow;
	public bool hideArrowWhenFinished = true;

	[Header("Suavidade")]
	public float arrowSmoothTime = 0.2f;

	private float currentAngle;
	private float angleVelocity;

	private void Start()
	{
		UpdatePath();
	}

	private void Update()
	{
		CheckDistance();
		UpdateArrow();
	}

	private void CheckDistance()
	{
		if (index < 0 || index >= Path.Count)
			return;

		Transform way = Path[index];

		if (way == null)
			return;

		if (Vector3.Distance(transform.position, way.position) <= distanceToReach)
		{
			index++;
			UpdatePath();
		}
	}

	public void UpdatePath()
	{
		for (int i = 0; i < Path.Count; i++)
		{
			if (Path[i] == null)
				continue;

			Path[i].gameObject.SetActive(
				i >= index && i < index + visibleWays
			);
		}
	}

	private void UpdateArrow()
	{
		if (directionArrow == null)
			return;

		if (index < 0 || index >= Path.Count || Path[index] == null)
		{
			if (hideArrowWhenFinished)
				directionArrow.gameObject.SetActive(false);

			return;
		}

		directionArrow.gameObject.SetActive(true);

		// 1. Converte a posição do Way para o espaço local do jogador.
		Vector3 localWay = transform.InverseTransformPoint(Path[index].position);

		// 2. Calcula o ângulo exato usando Atan2.
		// Em jogos 3D: O eixo X representa Esquerda/Direita e o eixo Z representa Frente/Trás.
		// Mapeamos o Z do mundo para o eixo Y da UI (cima/baixo na tela) e o X do mundo para o eixo X da UI.
		float targetAngle = Mathf.Atan2(localWay.z, localWay.x) * Mathf.Rad2Deg;

		// OBSERVAÇÃO PARA JOGOS 2D:
		// Se o seu jogo for 2D (Top-Down ou Plataforma) e o movimento ocorrer nos eixos X e Y,
		// troque 'localWay.z' por 'localWay.y' na linha acima.

		// 3. Ajuste do Offset da Imagem (Sprite):
		// Se o seu desenho da seta aponta para a DIREITA (->) quando a rotação Z é 0, deixe como está.
		// Se o seu desenho aponta para CIMA (^) quando a rotação Z é 0, descomente a linha abaixo:
		// targetAngle -= 90f;

		// 4. Suaviza a rotação
		currentAngle = Mathf.SmoothDampAngle(
			currentAngle,
			targetAngle,
			ref angleVelocity,
			Mathf.Max(0.01f, arrowSmoothTime)
		);

		directionArrow.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
	}
}