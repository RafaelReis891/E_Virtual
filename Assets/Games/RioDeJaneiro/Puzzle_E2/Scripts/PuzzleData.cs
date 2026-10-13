using System;
using UnityEngine;

[Serializable]
public class PuzzleData
{
	[TextArea(3, 5)] // Deixa a caixa de texto maior no Inspector
	public string clue;

	public string optionNorth;
	public string optionEast;
	public string optionSouth;
	public string optionWest;

	// Deve ser exatamente: "North", "East", "South" ou "West" (respeite as maiúsculas!)
	public string correctDirection;
}