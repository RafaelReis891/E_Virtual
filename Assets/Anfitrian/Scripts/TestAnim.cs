using UnityEngine;

public class TestAnim : MonoBehaviour
{
    public string animToPlay = "Talking02"; // coloque exatamente o nome que aparece no Animator

    void Start()
    {
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            Debug.Log("Tentando tocar: " + animToPlay);
            anim.Play(animToPlay, 0, 0f);
        }
        else
        {
            Debug.LogError("Animator não encontrado!");
        }
    }
}