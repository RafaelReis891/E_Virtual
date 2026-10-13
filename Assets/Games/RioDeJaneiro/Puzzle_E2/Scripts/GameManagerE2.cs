
using UnityEngine;
using TMPro;
using System;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManagerE2 : MonoBehaviour
{
	public static GameManagerE2 Instance;

	[Header("UI Elements - Feedback")]
	public TMP_Text feedbackText;

	[Header("UI Elements - End Screen Lose")]
	public GameObject panelEndScreenLose;
	public TMP_Text labelScoreLose;
	public TMP_Text labelTotalLose;

	[Header("UI Elements - End Screen Win")]
	public GameObject panelEndScreenWin;
	public TMP_Text labelScoreWin;
	public TMP_Text labelTotalWin;

	[Header("Referências")]
	public NeedleController needleController;
	public DirectionButton[] directionButtons;

	[Header("Puzzles da Fase Rio (E2)")]
	public PuzzleData[] puzzles;

	private int currentPuzzleIndex = 0;
	private int score = 0;

	public bool IsLocked { get; private set; } = false;

	public UnityEvent OnEndGame, OnPassNextScene;

	private void Awake()
	{
		if (Instance == null)
			Instance = this;
		else
		{
			Destroy(gameObject);
			return;
		}

		if (directionButtons == null || directionButtons.Length == 0)
			directionButtons = FindObjectsOfType<DirectionButton>(true);
	}

	private void Start()
	{
		if (panelEndScreenLose != null)
			panelEndScreenLose.SetActive(false);

		if (panelEndScreenWin != null)
			panelEndScreenWin.SetActive(false);

		SetAllBlinksWait();
		LoadPuzzle(currentPuzzleIndex);
	}

	public void LoadPuzzle(int index)
	{
		if (index >= puzzles.Length)
		{
			TriggerEndGame();
			return;
		}

		currentPuzzleIndex = index;

		PuzzleData p = puzzles[index];

		feedbackText.text = p.clue;
		feedbackText.color = Color.white;
		IsLocked = false;

		// Novo puzzle: todos voltam a piscar em azul.
		SetAllBlinksWait();

		if (needleController != null)
			needleController.SetExpectedAnswer(p.correctDirection);
	}

	public void CheckAnswer(string selectedDirection)
	{
		if (IsLocked)
			return;

		IsLocked = true;

		PuzzleData p = puzzles[currentPuzzleIndex];

		bool isCorrect = NormalizeDirection(selectedDirection) ==
						 NormalizeDirection(p.correctDirection);

		string selectedText = GetOptionText(p, selectedDirection);
		string correctText = GetOptionText(p, p.correctDirection);

		string selectedDirFormatted = FormatDirection(selectedDirection);
		string correctDirFormatted = FormatDirection(p.correctDirection);

		string message = $"{p.clue}\n\n";

		// Primeiro, todos ficam azuis.
		SetAllBlinksWait();

		if (isCorrect)
		{
			score++;

			// A direção escolhida pisca em verde.
			SetDirectionBlink(selectedDirection, true);

			message += $"<color=green>Sua resposta: \"{selectedDirFormatted} - {selectedText}\"\n";
			message += $"Resposta correta: \"{correctDirFormatted} - {correctText}\"</color>";

			feedbackText.text = message;

			Invoke(nameof(LoadNextPuzzle), 3f);
		}
		else
		{
			// Direção escolhida: vermelho.
			SetDirectionBlink(selectedDirection, false);

			// Direção correta: verde.
			SetDirectionBlink(p.correctDirection, true);

			message += $"<color=red>Sua resposta: \"{selectedDirFormatted} - {selectedText}\"\n</color>";
			message += $"<color=green>Resposta correta: \"{correctDirFormatted} - {correctText}\"</color>";

			feedbackText.text = message;

			Invoke(nameof(LoadNextPuzzle), 5f);
		}
	}

	private void SetAllBlinksWait()
	{
		if (directionButtons == null)
			return;

		foreach (DirectionButton button in directionButtons)
		{
			if (button != null)
				button.SetBlinkWait();
		}
	}

	private void SetDirectionBlink(string direction, bool correct)
	{
		string normalized = NormalizeDirection(direction);

		if (directionButtons == null)
			return;

		foreach (DirectionButton button in directionButtons)
		{
			if (button == null)
				continue;

			if (NormalizeDirection(button.directionName) != normalized)
				continue;

			if (correct)
				button.SetBlinkWin();
			else
				button.SetBlinkLose();
		}
	}

	private string GetOptionText(PuzzleData p, string direction)
	{
		switch (NormalizeDirection(direction))
		{
			case "Norte": return p.optionNorth;
			case "Leste": return p.optionEast;
			case "Sul": return p.optionSouth;
			case "Oeste": return p.optionWest;
			default: return "???";
		}
	}

	private string NormalizeDirection(string direction)
	{
		if (string.IsNullOrEmpty(direction))
			return "";

		switch (direction.Trim().ToLowerInvariant())
		{
			case "north":
			case "norte":
				return "Norte";

			case "east":
			case "leste":
			case "este":
				return "Leste";

			case "south":
			case "sul":
				return "Sul";

			case "west":
			case "oeste":
				return "Oeste";

			default:
				return direction.Trim();
		}
	}

	private string FormatDirection(string direction)
	{
		return NormalizeDirection(direction);
	}

	private void LoadNextPuzzle()
	{
		currentPuzzleIndex++;
		LoadPuzzle(currentPuzzleIndex);

		if (currentPuzzleIndex < puzzles.Length &&
			needleController != null &&
			needleController.needle != null)
		{
			float currentAngle =
				needleController.needle.localEulerAngles.z;

			needleController.RotateToDirection(
				"",
				UnityEngine.Random.Range(currentAngle + 45f, currentAngle + 180f),
				true);
		}
	}

	private void TriggerEndGame()
	{
		IsLocked = true;
		feedbackText.text = "";

		int totalPuzzles = puzzles.Length;
		bool isWin = score == totalPuzzles;

		if (isWin)
		{
			if (panelEndScreenLose != null)
				panelEndScreenLose.SetActive(false);

			if (panelEndScreenWin != null)
				panelEndScreenWin.SetActive(true);

			if (labelScoreWin != null)
				labelScoreWin.text = score.ToString();

			if (labelTotalWin != null)
				labelTotalWin.text = totalPuzzles.ToString();
		}
		else
		{
			if (panelEndScreenWin != null)
				panelEndScreenWin.SetActive(false);

			if (panelEndScreenLose != null)
				panelEndScreenLose.SetActive(true);

			if (labelScoreLose != null)
				labelScoreLose.text = score.ToString();

			if (labelTotalLose != null)
				labelTotalLose.text = totalPuzzles.ToString();
		}

		OnEndGame?.Invoke();
		Invoke(nameof(PassNextSceneEvent), 3f);
	}

	void PassNextSceneEvent()
	{
		OnPassNextScene.Invoke();
	}
}
