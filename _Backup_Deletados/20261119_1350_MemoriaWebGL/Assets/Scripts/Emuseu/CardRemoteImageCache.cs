using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Cache estatico de IMAGENS dos Final Cards vindos do site (so imagens; videos NAO entram aqui).
// - Dedupe: a mesma URL so e baixada uma vez (mesmo pedida por varios boards).
// - Fila com no maximo MaxSimultaneos downloads ao mesmo tempo; pedido "urgente" (board precisa agora) fura a fila.
// - Nao usa MonoBehaviour: usa UnityWebRequest + callback completed.
// - TextureReady avisa os boards quando uma textura fica pronta (eles trocam sozinhos, sem sair/voltar do trigger).
// - Os boards NAO destroem texturas do cache (elas vivem ate o fim da sessao; ficam sem leitura na CPU para poupar memoria).
public static class CardRemoteImageCache
{
    public const int MaxSimultaneos = 4;
    public const int MaxTentativas = 3;
    public const float EsperaEntreTentativas = 2f;

    // (url, textura). Disparado na thread principal quando um download termina com sucesso.
    public static event Action<string, Texture2D> TextureReady;

    static readonly Dictionary<string, Texture2D> prontas = new Dictionary<string, Texture2D>();
    static readonly HashSet<string> pendentes = new HashSet<string>();        // na fila ou baixando
    static readonly LinkedList<string> fila = new LinkedList<string>();
    static readonly Dictionary<string, string> idDaUrl = new Dictionary<string, string>();   // so para log
    static readonly Dictionary<string, int> tentativas = new Dictionary<string, int>();
    static readonly Dictionary<string, float> ultimaFalha = new Dictionary<string, float>();
    static int ativos;

    public static int Count { get { return prontas.Count; } }
    public static int Pendentes { get { return pendentes.Count; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar()
    {
        // Seguro com o Domain Reload do editor desligado: as texturas antigas ja foram destruidas pelo Unity.
        prontas.Clear();
        pendentes.Clear();
        fila.Clear();
        idDaUrl.Clear();
        tentativas.Clear();
        ultimaFalha.Clear();
        ativos = 0;
        TextureReady = null;
    }

    public static bool EhHttp(string s)
    {
        return !string.IsNullOrEmpty(s) &&
               (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryGet(string url, out Texture2D tex)
    {
        tex = null;
        if (string.IsNullOrEmpty(url)) return false;
        if (!prontas.TryGetValue(url, out tex)) return false;
        if (tex == null) { prontas.Remove(url); tex = null; return false; } // destruida por fora
        return true;
    }

    public static bool Baixando(string url)
    {
        return !string.IsNullOrEmpty(url) && pendentes.Contains(url);
    }

    // Pede uma imagem. Nao faz nada se ja esta pronta ou ja esta na fila/baixando.
    // urgente = um board precisa dela agora (vai para o inicio da fila).
    public static void Pedir(string url, string idCard, bool urgente)
    {
        if (!EhHttp(url)) return;
        Texture2D t;
        if (TryGet(url, out t)) return;

        if (pendentes.Contains(url))
        {
            if (urgente && fila.Remove(url)) { fila.AddFirst(url); Bombear(); }
            return;
        }

        int n;
        tentativas.TryGetValue(url, out n);
        if (n >= MaxTentativas)
        {
            float t0;
            // Depois de esgotar as tentativas, so tenta de novo se o pedido for urgente e a ultima falha for antiga (ex: board reabriu).
            if (!urgente || !ultimaFalha.TryGetValue(url, out t0) || Time.realtimeSinceStartup - t0 < 10f) return;
            tentativas[url] = 0;
        }
        else
        {
            float t1;
            if (ultimaFalha.TryGetValue(url, out t1) && Time.realtimeSinceStartup - t1 < EsperaEntreTentativas) return;
        }

        pendentes.Add(url);
        idDaUrl[url] = idCard ?? "?";
        if (urgente) fila.AddFirst(url); else fila.AddLast(url);
        Bombear();
    }

    static void Bombear()
    {
        while (ativos < MaxSimultaneos && fila.Count > 0)
        {
            string url = fila.First.Value;
            fila.RemoveFirst();
            Baixar(url);
        }
    }

    static void Baixar(string url)
    {
        string id;
        if (!idDaUrl.TryGetValue(url, out id)) id = "?";
        ativos++;
        UnityWebRequest req = null;
        try
        {
            EmuseuDebug.ImagemInicio(id, url, "preloader");
            req = UnityWebRequestTexture.GetTexture(url, true); // true = nao legivel na CPU (menos memoria)
            var op = req.SendWebRequest();
            var r = req;
            op.completed += _ => Concluir(url, id, r);
        }
        catch (Exception ex)
        {
            EmuseuDebug.Error("preload", "falha ao iniciar download id=" + id + ": " + ex.GetType().Name + ": " + ex.Message);
            if (req != null) req.Dispose();
            ativos = Mathf.Max(0, ativos - 1);
            Falhou(url);
        }
    }

    static void Concluir(string url, string id, UnityWebRequest req)
    {
        Texture2D tex = null;
        try
        {
            if (req.result == UnityWebRequest.Result.Success)
            {
                tex = DownloadHandlerTexture.GetContent(req);
                if (tex != null)
                {
                    tex.name = "CardRemote_" + id;
                    EmuseuDebug.ImagemResultado(id, url, true, tex.width, tex.height, req.downloadedBytes, null, req.responseCode);
                }
            }
            if (tex == null)
            {
                EmuseuDebug.ImagemResultado(id, url, false, 0, 0, req.downloadedBytes, req.error ?? "textura nula", req.responseCode);
            }
        }
        catch (Exception ex)
        {
            tex = null;
            EmuseuDebug.Error("preload", "erro ao ler textura id=" + id + ": " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            try { req.Dispose(); } catch (Exception) { }
        }

        ativos = Mathf.Max(0, ativos - 1);
        pendentes.Remove(url);

        if (tex != null)
        {
            prontas[url] = tex;
            tentativas.Remove(url);
            ultimaFalha.Remove(url);
            EmuseuDebug.Info("preload", "textura pronta id=" + id + " (no cache: " + prontas.Count + ", ouvintes=" + (TextureReady != null ? TextureReady.GetInvocationList().Length : 0) + ")");
            Avisar(url, tex);
        }
        else
        {
            Falhou(url);
        }
        Bombear();
    }

    static void Falhou(string url)
    {
        int n;
        tentativas.TryGetValue(url, out n);
        tentativas[url] = n + 1;
        ultimaFalha[url] = Time.realtimeSinceStartup;
        pendentes.Remove(url);
        EmuseuDebug.Warn("preload", "download falhou (tentativa " + (n + 1) + "/" + MaxTentativas + ")" +
                         ((n + 1) < MaxTentativas ? "; sera tentado de novo quando algum board pedir" : "; desisti ate novo pedido urgente"));
    }

    static void Avisar(string url, Texture2D tex)
    {
        var h = TextureReady;
        if (h == null) return;
        foreach (Delegate d in h.GetInvocationList())
        {
            try { ((Action<string, Texture2D>)d)(url, tex); }
            catch (Exception ex)
            {
                EmuseuDebug.Error("preload", "listener de TextureReady falhou: " + ex.GetType().Name + ": " + ex.Message);
                Debug.LogWarning("[CardRemoteImageCache] listener falhou: " + ex.Message);
            }
        }
    }
}