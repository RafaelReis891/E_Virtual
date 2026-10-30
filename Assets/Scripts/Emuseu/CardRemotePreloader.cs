using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pre-carrega as IMAGENS dos Final Cards (nunca videos), sem precisar por nada na cena.
// Roda sozinho:
//  - quando uma cena carrega (SceneManager.sceneLoaded), e
//  - quando o payload do site chega (CardRemoteCatalog.Changed).
// Procura todos os FinalCardBoard da cena (inclusive desligados) e os PuzzleProgresso.cartaId,
// pega o id de cada card, acha a URL da imagem no catalogo remoto e pede ao CardRemoteImageCache.
// Os boards usam o cache e trocam a imagem sozinhos quando ela fica pronta.
public static class CardRemotePreloader
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar()
    {
        // Assina de novo com seguranca (editor com Domain Reload desligado).
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
        CardRemoteCatalog.Changed -= AoCatalogoMudar;
        CardRemoteCatalog.Changed += AoCatalogoMudar;
    }

    static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        Preload("cena '" + cena.name + "' carregada");
    }

    static void AoCatalogoMudar()
    {
        Preload("payload do site chegou");
    }

    // Pode ser chamado de fora tambem (ex: apos instanciar boards em runtime).
    public static void Preload(string motivo)
    {
        try
        {
            if (CardRemoteCatalog.Count == 0)
            {
                EmuseuDebug.Info("preload", "(" + motivo + ") catalogo remoto vazio; aguardando payload");
                return;
            }

            var ids = new List<string>();
            var vistos = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            var boards = Object.FindObjectsByType<FinalCardBoard>(FindObjectsInactive.Include);
            foreach (var b in boards)
            {
                if (b == null) continue;
                Adicionar(b.IdAtual, ids, vistos);
            }

            var puzzles = Object.FindObjectsByType<PuzzleProgresso>(FindObjectsInactive.Include);
            foreach (var p in puzzles)
            {
                if (p == null) continue;
                Adicionar(p.cartaId, ids, vistos);
            }

            int pedidas = 0, jaProntas = 0, semUrl = 0;
            foreach (string id in ids)
            {
                string url = UrlImagem(id);
                if (string.IsNullOrEmpty(url)) { semUrl++; continue; }
                Texture2D t;
                if (CardRemoteImageCache.TryGet(url, out t)) { jaProntas++; continue; }
                CardRemoteImageCache.Pedir(url, id, false);
                pedidas++;
            }

            EmuseuDebug.Info("preload", "(" + motivo + ") boards=" + boards.Length + " ids=" + ids.Count + " [" + string.Join(",", ids) + "]" +
                             " imagens: pedidas=" + pedidas + " ja_no_cache=" + jaProntas + " sem_url_remota=" + semUrl +
                             " (so imagens; videos nao sao pre-carregados)");
        }
        catch (System.Exception ex)
        {
            EmuseuDebug.Error("preload", "Preload falhou: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    static void Adicionar(string id, List<string> ids, HashSet<string> vistos)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        id = id.Trim();
        if (vistos.Add(id)) ids.Add(id);
    }

    // Mesma regra do CardRemoteCatalog.AplicarSobre: imageUrl, ou thumbUrl se nao tiver. So http(s).
    public static string UrlImagem(string id)
    {
        EmuseuCard c;
        if (!CardRemoteCatalog.TryGet(id, out c)) return null;
        string img = !string.IsNullOrWhiteSpace(c.imageUrl) ? c.imageUrl : c.thumbUrl;
        return CardRemoteImageCache.EhHttp(img) ? img : null;
    }
}