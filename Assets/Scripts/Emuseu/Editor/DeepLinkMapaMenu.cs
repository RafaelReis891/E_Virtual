using UnityEditor;
using UnityEngine;

// Menu de teste do DeepLinkMapa (so Editor).
//  - Fora do Play: guarda o mapa e, ao dar Play na CenaArena, o jogo vai para a cidade (vale um Play so).
//  - Em Play: vai agora, como se o site chamasse SendMessage("DeepLinkMapa", "IrParaMapa", slug).
public static class DeepLinkMapaMenu
{
    const string Raiz = "Tools/Emuseu/Testar deep link mapa/";

    [MenuItem(Raiz + "rio-de-janeiro")] static void Rj() { Testar("rio-de-janeiro"); }
    [MenuItem(Raiz + "sao-paulo")] static void Sp() { Testar("sao-paulo"); }
    [MenuItem(Raiz + "belo-horizonte")] static void Bh() { Testar("belo-horizonte"); }
    [MenuItem(Raiz + "porto-alegre")] static void Poa() { Testar("porto-alegre"); }
    [MenuItem(Raiz + "salvador")] static void Ssa() { Testar("salvador"); }
    [MenuItem(Raiz + "recife")] static void Rec() { Testar("recife"); }
    [MenuItem(Raiz + "fortaleza")] static void For() { Testar("fortaleza"); }
    [MenuItem(Raiz + "brasilia")] static void Df() { Testar("brasilia"); }
    [MenuItem(Raiz + "Teste: slug invalido (fluxo normal)")] static void Invalido() { Testar("cidade-que-nao-existe"); }
    [MenuItem(Raiz + "Teste: 'São Paulo' com acento")] static void Acento() { Testar("São Paulo"); }

    [MenuItem(Raiz + "Limpar teste", false, 100)]
    static void Limpar()
    {
        SessionState.EraseString(DeepLinkMapa.ChaveTesteEditor);
        Debug.Log("[DeepLinkMapa] teste do Editor limpo");
    }

    static void Testar(string slug)
    {
        if (Application.isPlaying)
        {
            if (DeepLinkMapa.Instance != null) DeepLinkMapa.Instance.IrPara(slug, "menu do Editor");
            else Debug.LogWarning("[DeepLinkMapa] instancia nao encontrada");
            return;
        }
        SessionState.SetString(DeepLinkMapa.ChaveTesteEditor, slug);
        string cena = DeepLinkMapa.CenaDoSlug(slug);
        Debug.Log("[DeepLinkMapa] teste guardado: mapa='" + slug + "' -> " + (cena ?? "desconhecido (fluxo normal)") + ". Abra a CenaArena e aperte Play.");
    }
}
