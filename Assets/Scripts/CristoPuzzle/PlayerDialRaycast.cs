
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

		// Cria o raio na posição atual do mouse.
		Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

		if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
		{
			if (hit.collider.CompareTag("Dial1"))
			{
				lockDoor.SelectDial(0);
				Debug.Log("Dial1 selecionado pelo mouse");
			}
			else if (hit.collider.CompareTag("Dial2"))
			{
				lockDoor.SelectDial(1);
				Debug.Log("Dial2 selecionado pelo mouse");
			}
		}
	}

	void OnDrawGizmos()
	{
		if (playerCamera == null)
			return;

		Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

		Gizmos.color = Color.yellow;
		Gizmos.DrawRay(ray.origin, ray.direction * rayDistance);
	}
}
