
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockDoor : MonoBehaviour
{
	[Header("Disco 1")]
	public List<int> rotatesDial1;
	public Transform dial1;
	public int indexDial1;

	[Header("Disco 2")]
	public List<int> rotatesDial2;
	public Transform dial2;
	public int indexDial2;

	[Header("Configurações")]
	public float rotationDuration = 0.2f;

	private Coroutine rotationDial1;
	private Coroutine rotationDial2;

	public void SelectDial(int dial)
	{
		if (dial == 0)
		{
			RotateDial(
				dial1,
				rotatesDial1,
				ref indexDial1,
				ref rotationDial1
			);
		}
		else
		{
			RotateDial(
				dial2,
				rotatesDial2,
				ref indexDial2,
				ref rotationDial2
			);
		}
	}

	private void RotateDial(
		Transform dial,
		List<int> rotations,
		ref int index,
		ref Coroutine rotationCoroutine)
	{
		if (dial == null || rotations == null || rotations.Count == 0)
			return;

		// Avança para o próximo índice.
		index = (index + 1) % rotations.Count;

		// Rotação configurada para esse índice.
		float targetAngle = rotations[index];

		// Interrompe a animação anterior, se ainda estiver ocorrendo.
		if (rotationCoroutine != null)
			StopCoroutine(rotationCoroutine);

		rotationCoroutine = StartCoroutine(
			SmoothRotate(dial, targetAngle, rotationDuration)
		);
	}

	private IEnumerator SmoothRotate(
		Transform dial,
		float targetAngle,
		float duration)
	{
		Quaternion startRotation = dial.localRotation;

		// Mantém os eixos X e Z e altera a rotação local Y.
		Vector3 currentEuler = dial.localEulerAngles;

		Quaternion targetRotation = Quaternion.Euler(
			currentEuler.x,
			targetAngle,
			currentEuler.z
		);

		float elapsed = 0f;

		if (duration <= 0f)
		{
			dial.localRotation = targetRotation;
			yield break;
		}

		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;

			float t = Mathf.Clamp01(elapsed / duration);
			t = Mathf.SmoothStep(0f, 1f, t);

			dial.localRotation = Quaternion.Slerp(
				startRotation,
				targetRotation,
				t
			);

			yield return null;
		}

		dial.localRotation = targetRotation;
	}
}
