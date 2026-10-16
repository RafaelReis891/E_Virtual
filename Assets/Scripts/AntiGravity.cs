
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class AntiGravityTrigger : MonoBehaviour
{
	[Header("Configuração")]
	[Min(0f)]
	public float launchForce = 8f;

	[Tooltip("Mantém a antigravidade enquanto estiver dentro da área.")]
	public bool maintainAntiGravity = true;

	private readonly HashSet<Rigidbody> rigidbodies = new();
	private readonly HashSet<StarterAssets.FirstPersonController> players = new();

	private void Reset()
	{
		GetComponent<Collider>().isTrigger = true;
	}

	private void OnTriggerEnter(Collider other)
	{
		Rigidbody rb = other.attachedRigidbody;

		if (rb != null && !rb.isKinematic)
		{
			if (rigidbodies.Add(rb))
			{
				rb.AddForce(
					Vector3.up * launchForce,
					ForceMode.VelocityChange
				);
			}
		}

		StarterAssets.FirstPersonController player =
			other.GetComponentInParent<StarterAssets.FirstPersonController>();

		if (player != null && players.Add(player))
		{
			player.ActivateAntiGravity(launchForce);
		}
	}

	private void FixedUpdate()
	{
		if (!maintainAntiGravity)
			return;

		foreach (Rigidbody rb in rigidbodies)
		{
			if (rb == null || rb.isKinematic)
				continue;

			// Cancela a gravidade global sem afetar outros objetos.
			rb.AddForce(-Physics.gravity, ForceMode.Acceleration);
		}
	}

	private void OnTriggerExit(Collider other)
	{
		Rigidbody rb = other.attachedRigidbody;

		if (rb != null)
			rigidbodies.Remove(rb);

		StarterAssets.FirstPersonController player =
			other.GetComponentInParent<StarterAssets.FirstPersonController>();

		if (player != null && players.Remove(player))
		{
			player.DeactivateAntiGravity();
		}
	}
}
