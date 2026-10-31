#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Menu de teste do EmuseuAlbumCard. Nao precisa Play: o envelope aparece no Console pelo stub do editor.
public static class EmuseuAlbumCardMenu
{
    [MenuItem("Tools/Emuseu/Teste Album Card (enviar F11)")]
    static void TesteEnviarF11()
    {
        EmuseuAlbumCard.TesteEditor("F11");
    }

    [MenuItem("Tools/Emuseu/Teste Album Card (colecao simulada F11)")]
    static void TesteColecao()
    {
        // Com o play-config falso (album-teste), F11 = numero 1. Depois disso, "enviar F11" deve dizer "pulado, jogador ja tem".
        EmuseuAlbumCard.TesteConfigFalso();   // garante o play-config falso
        EmuseuAlbumCard.TesteColecaoEditor(EmuseuAlbumCard.TesteEnvelopeColecao("album-teste", 1));
        Debug.Log("[AlbumCard] [teste] agora rode 'Teste Album Card (enviar F11)': deve dizer pulado, ja tem");
    }
}
#endif
