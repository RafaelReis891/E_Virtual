using UnityEngine;

// Ponte de OnGUI do AdminToolsPanel. So fica ligado (enabled) quando o painel/aviso precisa desenhar,
// assim nao ha custo de IMGUI para jogadores normais.
public sealed class AdminToolsGui : MonoBehaviour
{
    public AdminToolsPanel dono;

    void OnGUI()
    {
        if (dono != null) dono.Desenhar();
    }
}