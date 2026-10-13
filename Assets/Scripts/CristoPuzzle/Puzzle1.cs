
using UnityEngine;

public class Puzzle1 : MonoBehaviour
{
	[Header("Referências")]
	public Camera mainCamera;
	public GameObject[] flagBlocks;
	public SpriteRenderer[] glowEffects;
	public Animator door;
	public Transform hint;
	public AudioClip successSound;
	public AudioClip errorSound;
	public LockDoor lockDoor;

	[Header("Resposta Correta")]
	public string correctCountryTag;
	public string correctAnswer;

	[Header("Configurações")]
	public float rayDistance = 100f;

	private AudioSource audioSource;
	private bool puzzleSolved;

	private Quaternion doorInitialRotation;
	private bool hasDoorRotation;

	void Start()
	{
		// Usa a câmera principal automaticamente se não foi configurada.
		if (mainCamera == null)
			mainCamera = Camera.main;

		// Garante que exista um AudioSource.
		audioSource = GetComponent<AudioSource>();

		if (audioSource == null)
			audioSource = gameObject.AddComponent<AudioSource>();

		// Guarda a rotação original da porta.
		if (door != null)
		{
			doorInitialRotation = door.transform.localRotation;
			hasDoorRotation = true;
		}

		// Esconde todos os efeitos de brilho.
		if (glowEffects != null)
		{
			foreach (SpriteRenderer glow in glowEffects)
			{
				if (glow != null)
					glow.enabled = false;
			}
		}
	}

	void Update()
	{
		if (puzzleSolved)
			return;

		// Detecta o clique, mas não utiliza a posição do mouse.
		if (Input.GetMouseButtonDown(0))
			HandleClick();
	}

	void HandleClick()
	{
		if (mainCamera == null)
		{
			Debug.LogWarning("Puzzle1: câmera não encontrada.", this);
			return;
		}

		// O raio parte do centro da câmera e segue sua direção frontal.
		Ray ray = new Ray(
			mainCamera.transform.position,
			mainCamera.transform.forward
		);

		RaycastHit[] hits = Physics.RaycastAll(
			ray,
			rayDistance,
			Physics.DefaultRaycastLayers,
			QueryTriggerInteraction.Collide
		);

		// Encontra a bandeira válida mais próxima atingida pelo raio.
		GameObject closestBlock = null;
		float closestDistance = float.MaxValue;

		if (flagBlocks == null)
			return;

		foreach (RaycastHit hit in hits)
		{
			foreach (GameObject block in flagBlocks)
			{
				if (block == null)
					continue;

				Transform hitTransform = hit.collider.transform;

				bool belongsToBlock =
					hitTransform == block.transform ||
					hitTransform.IsChildOf(block.transform);

				if (belongsToBlock && hit.distance < closestDistance)
				{
					closestDistance = hit.distance;
					closestBlock = block;
				}
			}
		}

		if (closestBlock != null)
			CheckAnswer(closestBlock);
	}


	void CheckAnswer(GameObject clickedBlock)
	{
		Debug.Log("Bandeira clicada: " + clickedBlock.name);

		// Desativa todos os glows.
		if (glowEffects != null)
		{
			foreach (SpriteRenderer glow in glowEffects)
			{
				if (glow != null)
					glow.enabled = false;
			}
		}

		// Ativa somente o glow do bloco clicado.
		if (flagBlocks != null && glowEffects != null)
		{
			int index = System.Array.IndexOf(flagBlocks, clickedBlock);

			if (index >= 0 && index < glowEffects.Length)
			{
				if (glowEffects[index] != null)
					glowEffects[index].enabled = true;
			}
		}

		bool correctByTag =
			!string.IsNullOrEmpty(correctCountryTag) &&
			clickedBlock.tag == correctCountryTag;

		bool correctByName =
			!string.IsNullOrEmpty(correctAnswer) &&
			clickedBlock.name.IndexOf(
				correctAnswer,
				System.StringComparison.OrdinalIgnoreCase
			) >= 0;

		if (correctByTag || correctByName)
			HandleCorrectAnswer(clickedBlock);
		else
			HandleWrongAnswer();
	}



	void HandleCorrectAnswer(GameObject correctBlock)
	{
		if (puzzleSolved)
			return;

		// Verifica a combinação dos discos antes de abrir a porta.
		if (lockDoor == null)
		{
			Debug.LogWarning("Puzzle1: LockDoor não foi configurado.");
			return;
		}

		bool dial1Correct = lockDoor.indexDial1 == 6;

		bool dial2Correct =
			lockDoor.indexDial2 == 0 ||
			lockDoor.indexDial2 == 12;

		if (!dial1Correct || !dial2Correct)
		{
			Debug.Log("Combinação dos discos incorreta!");

			if (errorSound != null && audioSource != null)
				audioSource.PlayOneShot(errorSound);

			return;
		}

		puzzleSolved = true;

		Debug.Log("Combinação correta! Porta aberta.");

		if (successSound != null && audioSource != null)
			audioSource.PlayOneShot(successSound);

		if (flagBlocks != null && glowEffects != null)
		{
			int index = System.Array.IndexOf(flagBlocks, correctBlock);

			if (index >= 0 && index < glowEffects.Length &&
				glowEffects[index] != null)
			{
				glowEffects[index].enabled = true;
			}
		}

		if (door != null)
			door.Play("DoorOpen");

		if (hint != null)
			hint.gameObject.SetActive(false);
	}


	void HandleWrongAnswer()
	{
		Debug.Log("Resposta errada!");

		if (errorSound != null && audioSource != null)
			audioSource.PlayOneShot(errorSound);
	}

	public void ResetPuzzle()
	{
		puzzleSolved = false;

		// Desativa todos os glows.
		if (glowEffects != null)
		{
			foreach (SpriteRenderer glow in glowEffects)
			{
				if (glow != null)
					glow.enabled = false;
			}
		}

		// Restaura a rotação original da porta.
		if (door != null && hasDoorRotation)
			door.transform.localRotation = doorInitialRotation;

		Debug.Log("Puzzle reiniciado.");
	}
}
