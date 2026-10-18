using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleLoadScene : MonoBehaviour
{
	private void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
	}
	public void LoadScene(string name)
    {
        SceneManager.LoadScene(name);
    }
}
