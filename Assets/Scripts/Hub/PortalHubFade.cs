using UnityEngine;

/// <summary>
/// Usado pelo prefab Portal_Hub: se existir na cena um objeto chamado "FadeIn"
/// (ex.: Canvas/FadeIn da CenaRiodeJaneiro), ativa-o antes de carregar a CenaArena.
/// Se nao existir (outras cidades), simplesmente nao faz nada (sem fade).
/// </summary>
public class PortalHubFade : MonoBehaviour
{
    public string nomeObjetoFade = "FadeIn";

    public void AtivarFade()
    {
        Transform[] todos = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < todos.Length; i++)
        {
            Transform t = todos[i];
            if (t == null || t.name != nomeObjetoFade) continue;
            if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded) continue;
            t.gameObject.SetActive(true);
            return;
        }
    }
}