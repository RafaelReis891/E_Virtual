using UnityEngine;
using UnityEngine.Events;

// Coloque em qualquer objeto 3D com Collider para virar um botao.
// Ex: botao ENG/BR -> OnClique chama FinalCardBoard.AlternarIdioma.
[RequireComponent(typeof(Collider))]
public class BotaoClicavel3D : MonoBehaviour
{
    public UnityEvent OnClique;
    public AudioClip somClique;

    void OnMouseDown()
    {
        if (somClique != null) AudioSource.PlayClipAtPoint(somClique, transform.position);
        OnClique.Invoke();
    }
}
