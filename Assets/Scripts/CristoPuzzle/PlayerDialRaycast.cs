
using UnityEngine;

public class PlayerDialRaycast : MonoBehaviour
{
	[Header("Referências")]
	public Camera playerCamera;
	public LockDoor lockDoor;

	[Header("Configurações")]
	public float rayDistance = 5f;

	void Start()
	{
		if (playerCamera == null)
			playerCamera = Camera.main;

		if (lockDoor == null)
			lockDoor = FindFirstObjectByType<LockDoor>();
	}

	void Update()
	{
		if (Input.GetMouseButtonDown(0))
			CheckDial();
	}

	void CheckDial()
	{
		if (playerCamera == null || lockDoor == null)
			return;

		Ray ray = new Ray(
			playerCamera.transform.position,
			playerCamera.transform.forward
		);

		if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
		{
			if (hit.collider.CompareTag("Dial1"))
			{
				lockDoor.SelectDial(0);
				Debug.Log("Dial1 selecionado");
			}
			else if (hit.collider.CompareTag("Dial2"))
			{
				lockDoor.SelectDial(1);
				Debug.Log("Dial2 selecionado");
			}
		}
	}

	void OnDrawGizmos()
	{
		if (playerCamera == null)
			return;

		Gizmos.color = Color.yellow;
		Gizmos.DrawRay(
			playerCamera.transform.position,
			playerCamera.transform.forward * rayDistance
		);
	}
}
