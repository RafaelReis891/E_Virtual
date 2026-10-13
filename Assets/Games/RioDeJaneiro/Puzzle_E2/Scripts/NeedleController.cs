using UnityEngine;

public class NeedleController : MonoBehaviour
{
	[Header("Configurações de Rotação")]
	public float rotationSpeed = 120f;
	public float arrivalThreshold = 3f;

	[Header("Referências")]
	public Transform needle;

	private float targetAngle = 0f;
	private bool isRotating = false;
	private string currentDirection = "";
	private string expectedDirection = "";

	private void Start()
	{
		needle.localRotation = Quaternion.Euler(0, 0, 45);
	}

	private void Update()
	{
		if (isRotating)
		{
			float currentAngle = needle.localEulerAngles.z;
			float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
			needle.localRotation = Quaternion.Euler(0, 0, newAngle);

			if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) < arrivalThreshold)
			{
				isRotating = false;
				needle.localRotation = Quaternion.Euler(0, 0, targetAngle);
				if(!dontCheck)
					OnArrived();
			}
		}
	}

	public void RotateToDirection(string direction, float angle, bool onlyRotate)
	{
		// Trava se já estiver girando OU se o GameManager estiver processando a resposta
		if (isRotating || GameManagerE2.Instance.IsLocked) return;

		currentDirection = direction;
		targetAngle = angle;
		isRotating = true;
		dontCheck = onlyRotate;
	}
	bool dontCheck;

	public void SetExpectedAnswer(string direction)
	{
		expectedDirection = direction;
	}

	private void OnArrived()
	{
		GameManagerE2.Instance.CheckAnswer(currentDirection);
	}
}