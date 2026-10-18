using UnityEngine;

public class Teleport : MonoBehaviour
{
	private CharacterController controller;
	StarterAssets.FirstPersonController player;



private void Awake()
	{
		controller = GetComponent<CharacterController>();
		player = GetComponent<StarterAssets.FirstPersonController>();
	}

	private void OnTriggerEnter(Collider other)
	{
		Teletransporter teleport = other.GetComponent<Teletransporter>();

		if (teleport == null || teleport.localTarget == null)
			return;

		// Desativa temporariamente para evitar conflitos de colisão.
		bool wasEnabled = controller != null && controller.enabled;

		if (wasEnabled)
			controller.enabled = false;

		// Teletransporta o próprio player.
		transform.SetPositionAndRotation(
			teleport.localTarget.position,
			teleport.localTarget.rotation
		);

		if (wasEnabled)
			controller.enabled = true;

		if (player != null)
		{
			player.DeactivateAntiGravity();
		}
	}
}