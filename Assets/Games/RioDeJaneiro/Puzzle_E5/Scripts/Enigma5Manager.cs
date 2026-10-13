using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Enigma5Manager : MonoBehaviour
{
	[Header("🎯 OBJETOS DA CENA (Arraste aqui)")]
	public Transform dial1;          // O disco do primeiro número
	public Transform dial2;          // O disco do segundo número
	public GameObject[] leftFlags;   // Array com todas as bandeiras da esquerda
	public GameObject[] rightFlags;  // Array com todas as bandeiras da direita
	public Animator doorAnimator;    // Animator da porta (ou deixe vazio se for outro método)
	public GameObject successEffect; // Efeito visual de vitória (partícula, luz, etc)

	[Header("🔢 CONFIGURAÇÃO DOS DISCOS")]
	[Tooltip("Ângulos de rotação para os números 0 a 9. Ex: 0, 36, 72, 108...")]
	public float[] dialAngles = { 0f, 36f, 72f, 108f, 144f, 180f, 216f, 252f, 288f, 324f };

	[Tooltip("Resposta correta: Índice do array dialAngles (5 = número 5)")]
	public int targetDial1Index = 5;

	[Tooltip("Resposta correta: Índice do array dialAngles (0 = número 0)")]
	public int targetDial2Index = 0;

	[Header("🏳️ CONFIGURAÇÃO DAS BANDEIRAS")]
	[Tooltip("Parte do nome do objeto da bandeira correta da esquerda (Ex: 'Brazil' ou 'Brasil')")]
	public string correctLeftFlagName = "Brazil";

	[Tooltip("Parte do nome do objeto da bandeira correta da direita (Ex: 'USA' ou 'Estados Unidos')")]
	public string correctRightFlagName = "USA";

	[Header("🔊 FEEDBACK")]
	public AudioClip clickSound;
	public AudioClip successSound;
	public AudioClip errorSound;
	private AudioSource audioSource;

	[Header("⚙️ ESTADO INTERNO (Não mexer)")]
	private int currentDial1Index = 0;
	private int currentDial2Index = 0;
	private int selectedLeftFlagIndex = -1;
	private int selectedRightFlagIndex = -1;
	private bool isSolved = false;
	private Coroutine activeRotation1;
	private Coroutine activeRotation2;

	public UnityEvent OnEndGame, OnPassNextScene;

	void Start()
	{
		audioSource = GetComponent<AudioSource>();
		if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

		// Garante que os discos comecem na posição correta do índice 0
		if (dial1 != null) dial1.localEulerAngles = new Vector3(0, dialAngles[0], 0);
		if (dial2 != null) dial2.localEulerAngles = new Vector3(0, dialAngles[0], 0);

		ResetVisuals();
	}

	void Update()
	{
		if (isSolved) return;

		// Detecção de clique do mouse (funciona em qualquer objeto com Collider)
		if (Input.GetMouseButtonDown(0))
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			if (Physics.Raycast(ray, out RaycastHit hit, 100f))
			{
				ProcessClick(hit.collider.gameObject);
			}
		}
	}

	private void ProcessClick(GameObject clickedObject)
	{
		// 1. Verifica se clicou no Disco 1
		if (clickedObject.transform == dial1 || clickedObject.transform.IsChildOf(dial1))
		{
			PlaySound(clickSound);
			currentDial1Index = (currentDial1Index + 1) % dialAngles.Length;
			RotateDial(dial1, dialAngles[currentDial1Index], ref activeRotation1);
			CheckWinCondition();
			return;
		}

		// 2. Verifica se clicou no Disco 2
		if (clickedObject.transform == dial2 || clickedObject.transform.IsChildOf(dial2))
		{
			PlaySound(clickSound);
			currentDial2Index = (currentDial2Index + 1) % dialAngles.Length;
			RotateDial(dial2, dialAngles[currentDial2Index], ref activeRotation2);
			CheckWinCondition();
			return;
		}

		// 3. Verifica se clicou em uma bandeira da ESQUERDA
		for (int i = 0; i < leftFlags.Length; i++)
		{
			if (leftFlags[i] != null && (clickedObject == leftFlags[i] || clickedObject.transform.IsChildOf(leftFlags[i].transform)))
			{
				PlaySound(clickSound);
				selectedLeftFlagIndex = i;
				UpdateFlagVisuals();
				CheckWinCondition();
				return;
			}
		}

		// 4. Verifica se clicou em uma bandeira da DIREITA
		for (int i = 0; i < rightFlags.Length; i++)
		{
			if (rightFlags[i] != null && (clickedObject == rightFlags[i] || clickedObject.transform.IsChildOf(rightFlags[i].transform)))
			{
				PlaySound(clickSound);
				selectedRightFlagIndex = i;
				UpdateFlagVisuals();
				CheckWinCondition();
				return;
			}
		}
	}

	private void CheckWinCondition()
	{
		// Verifica se os discos estão no 5 e 0
		bool dialsCorrect = (currentDial1Index == targetDial1Index) && (currentDial2Index == targetDial2Index);

		// Verifica se as bandeiras foram selecionadas e se os nomes batem
		bool leftFlagCorrect = false;
		if (selectedLeftFlagIndex != -1 && leftFlags[selectedLeftFlagIndex] != null)
		{
			leftFlagCorrect = leftFlags[selectedLeftFlagIndex].name.Contains(correctLeftFlagName);
		}

		bool rightFlagCorrect = false;
		if (selectedRightFlagIndex != -1 && rightFlags[selectedRightFlagIndex] != null)
		{
			rightFlagCorrect = rightFlags[selectedRightFlagIndex].name.Contains(correctRightFlagName);
		}

		// SE TUDO ESTIVER CORRETO:
		if (dialsCorrect && leftFlagCorrect && rightFlagCorrect)
		{
			SolvePuzzle();
		}
		else
		{
			// Opcional: Tocar som de erro se o jogador clicar em algo errado após ter selecionado tudo
			// (Descomente a linha abaixo se quiser esse feedback)
			 if (selectedLeftFlagIndex != -1 && selectedRightFlagIndex != -1 && !leftFlagCorrect || !rightFlagCorrect) PlaySound(errorSound);
		}
	}

	private void SolvePuzzle()
	{
		isSolved = true;
		Debug.Log("✅ ENIGMA 5 RESOLVIDO: 5x0, Brasil e EUA!");

		PlaySound(successSound);

		if (successEffect != null) successEffect.SetActive(true);
		if (doorAnimator != null) doorAnimator.SetTrigger("Open"); // Certifique-se de ter um trigger "Open" no animator

		Invoke(nameof(CallEndGame), 3f);
	}

	void CallEndGame()
	{
		OnEndGame?.Invoke();
		Invoke(nameof(PassNextSceneEvent), 3f);
	}
	void PassNextSceneEvent()
	{
		OnPassNextScene.Invoke();
	}

	private void RotateDial(Transform dial, float targetAngle, ref Coroutine activeCoroutine)
	{
		if (activeCoroutine != null) StopCoroutine(activeCoroutine);
		activeCoroutine = StartCoroutine(SmoothRotate(dial, targetAngle, 0.3f));
	}

	private IEnumerator SmoothRotate(Transform dial, float targetYAngle, float duration)
	{
		Quaternion startRot = dial.localRotation;
		Quaternion targetRot = Quaternion.Euler(0, targetYAngle, 0);
		float elapsed = 0f;

		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			dial.localRotation = Quaternion.Slerp(startRot, targetRot, t);
			yield return null;
		}
		dial.localRotation = targetRot;
	}

	private void UpdateFlagVisuals()
	{
		ResetVisuals();

		// Ilumina a bandeira da esquerda selecionada
		if (selectedLeftFlagIndex != -1 && leftFlags[selectedLeftFlagIndex] != null)
		{
			HighlightObject(leftFlags[selectedLeftFlagIndex], Color.green);
		}

		// Ilumina a bandeira da direita selecionada
		if (selectedRightFlagIndex != -1 && rightFlags[selectedRightFlagIndex] != null)
		{
			HighlightObject(rightFlags[selectedRightFlagIndex], Color.green);
		}
	}

	private void ResetVisuals()
	{
		if (leftFlags != null)
			foreach (var flag in leftFlags) if (flag != null) HighlightObject(flag, Color.white);

		if (rightFlags != null)
			foreach (var flag in rightFlags) if (flag != null) HighlightObject(flag, Color.white);
	}

	private void HighlightObject(GameObject obj, Color color)
	{
		// Tenta mudar a cor do SpriteRenderer (para 2D/Sprites)
		SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
		if (sr != null) { sr.color = color; return; }

		// Tenta mudar a cor do Material (para 3D/Mesh)
		MeshRenderer mr = obj.GetComponent<MeshRenderer>();
		if (mr != null && mr.material != null) { mr.material.color = color; }
	}

	private void PlaySound(AudioClip clip)
	{
		if (clip != null && audioSource != null)
		{
			audioSource.PlayOneShot(clip);
		}
	}
}