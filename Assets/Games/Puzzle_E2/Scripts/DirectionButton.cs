
using UnityEngine;

public class DirectionButton : MonoBehaviour
{
	[Header("Configuração deste botão")]
	public string directionName;
	public float targetAngle;

	[Header("Referências")]
	public NeedleController needleController;
	public EmissionBlink emissionBlink;

	private void Awake()
	{
		if (emissionBlink == null)
			emissionBlink = GetComponent<EmissionBlink>();
	}

	private void OnMouseDown()
	{
		if (needleController != null)
		{
			needleController.RotateToDirection(
				directionName,
				targetAngle,
				false);
		}
	}

	public void SetBlinkWait()
	{
		if (emissionBlink != null)
			emissionBlink.SetWait();
	}

	public void SetBlinkWin()
	{
		if (emissionBlink != null)
			emissionBlink.SetWin();
	}

	public void SetBlinkLose()
	{
		if (emissionBlink != null)
			emissionBlink.SetLose();
	}
}
