using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

// Avisa o site emuseu quando o jogador ganha uma figurinha (album-card) e recebe a colecao (OnCollection).
// Script NOVO e sem setup manual: nao precisa mexer em PuzzleProgresso, PlayerConfig, EmuseuBridge nem cenas.
// - Nasce sozinho (RuntimeInitializeOnLoadMethod) e se anexa ao GameObject "EmuseuBridge" via AddComponent.
//   Assim o site chama SendMessage("EmuseuBridge", "OnCollection", json) e o metodo OnCollection daqui recebe.
// - Em cada cena carregada, escuta o OnFinalizado de todos os PuzzleProgresso.
// - Carta = cartaId do puzzle, ou idInicial do FinalCardBoard (mesma regra do PuzzleProgresso.Finalizar).
// - Frame (ex: "F11") -> albumId + number vem do play-config (card.frame, via EmuseuBridge.PlayConfigApplied).
// - Envia {albumId, number} com type "album-card" por EmuseuBridge.SendToReact.
// - Se o play-config ainda nao chegou, guarda e envia quando chegar.
// - OnCollection (payload.user.albums[].cards[].number) marca as cartas no PlayerConfig e evita reenvio.
// Logs: prefixo [AlbumCard].
public sealed class EmuseuAlbumCard : MonoBehaviour
{
    struct Ref { public string albumId; public int number; public int albumSize; }

    const string Tag = "[AlbumCard] ";

    static readonly Dictionary<string, Ref> porFrame = new Dictionary<string, Ref>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, string> framePorAlbumNumero = new Dictionary<string, string>(StringComparer.Ordinal);
    static readonly List<string> pendentes = new List<string>();
    static readonly HashSet<string> enviadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // enviadas nesta sessao
    static readonly HashSet<string> donasApi = new HashSet<string>(StringComparer.Ordinal);            // "albumId#number" que a API diz que o jogador tem
    static readonly HashSet<string> framesDonos = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // frames resolvidos de donasApi
    static readonly HashSet<PuzzleProgresso> puzzlesLigados = new HashSet<PuzzleProgresso>();
    static bool configRecebido;

    // Disparado depois de ler um OnCollection (frames que o jogador ja tem segundo a API).
    public static event Action<List<string>> ColecaoRecebida;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar()
    {
        porFrame.Clear(); framePorAlbumNumero.Clear(); pendentes.Clear(); enviadas.Clear();
        donasApi.Clear(); framesDonos.Clear(); puzzlesLigados.Clear();
        configRecebido = false;
        EmuseuBridge.PlayConfigApplied -= Preencher;
        EmuseuBridge.PlayConfigApplied += Preencher;
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    // Depois da primeira cena o EmuseuBridge (criado no BeforeSceneLoad) ja existe.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AposPrimeiraCena()
    {
        GarantirNaPonte();
        // Cobre a primeira cena caso o sceneLoaded ja tenha passado (puzzles ja ligados nao duplicam).
        LigarPuzzles(SceneManager.GetActiveScene());
        // Caso o play-config ja tenha chegado antes de nos (Domain Reload desligado etc.).
        if (!configRecebido && EmuseuBridge.PlayConfig != null) Preencher(EmuseuBridge.PlayConfig);
    }

    // Garante este componente no GameObject "EmuseuBridge" (alvo do SendMessage do site).
    static void GarantirNaPonte()
    {
        var ponte = EmuseuBridge.Instance;
        if (ponte == null) { if (Application.isPlaying) Debug.LogWarning(Tag + "EmuseuBridge.Instance nulo, OnCollection nao sera recebido"); return; }
        if (ponte.GetComponent<EmuseuAlbumCard>() == null)
        {
            ponte.gameObject.AddComponent<EmuseuAlbumCard>();
            Debug.Log(Tag + "componente anexado em '" + ponte.gameObject.name + "' (recebe OnCollection via SendMessage)");
        }
    }

    // ---- Unity -> site ----

    static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        GarantirNaPonte();
        LigarPuzzles(cena);
    }

    static void LigarPuzzles(Scene cena)
    {
        var todos = UnityEngine.Object.FindObjectsByType<PuzzleProgresso>(FindObjectsInactive.Include);
        foreach (var p in todos)
        {
            if (p == null || p.gameObject.scene != cena) continue;
            if (!puzzlesLigados.Add(p)) continue;
            var puzzle = p;
            puzzle.OnFinalizado.AddListener(() => AoFinalizar(puzzle));
        }
    }

    static void AoFinalizar(PuzzleProgresso p)
    {
        string carta = p.cartaId;
        string origem = "cartaId";
        if (string.IsNullOrEmpty(carta) && p.boardFinalCard != null)
        {
            var fb = p.boardFinalCard.GetComponentInChildren<FinalCardBoard>(true);
            if (fb != null) { carta = fb.idInicial; origem = "idInicial do FinalCardBoard"; }
        }
        if (string.IsNullOrEmpty(carta)) { Debug.LogWarning(Tag + "puzzle sem carta (cartaId e idInicial vazios): " + p.name, p); return; }
        carta = carta.Trim();
        Debug.Log(Tag + "carta resolvida: " + carta + " (puzzle '" + p.name + "', " + origem + ")");
        Enviar(carta);
    }

    // Pode ser chamado direto (ex: figurinhas especiais do Estadio): EmuseuAlbumCard.Enviar("F45");
    public static void Enviar(string frame)
    {
        if (string.IsNullOrEmpty(frame)) return;
        frame = frame.Trim();
        if (framesDonos.Contains(frame)) { Debug.Log(Tag + "pulado, jogador ja tem na API: " + frame); return; }
        if (enviadas.Contains(frame)) { Debug.Log(Tag + "pulado, ja enviado nesta sessao: " + frame); return; }
        if (!configRecebido)
        {
            if (!pendentes.Contains(frame)) pendentes.Add(frame);
            Debug.Log(Tag + "na fila (play-config ainda nao chegou): " + frame + " | fila=" + pendentes.Count);
            return;
        }
        if (!porFrame.TryGetValue(frame, out var r))
        {
            Debug.LogWarning(Tag + "frame " + frame + " nao existe em nenhum album do play-config, nada enviado");
            return;
        }
        enviadas.Add(frame);
        string payload = new JObject { ["albumId"] = r.albumId, ["number"] = r.number }.ToString(Formatting.None);
        string envelope = "{\"version\":1,\"channel\":\"" + EmuseuBridge.Channel + "\",\"type\":\"album-card\",\"payload\":" + payload + "}";
        Debug.Log(Tag + "enviado " + frame + " -> " + envelope);
        EmuseuBridge.SendToReact("album-card", payload);
    }

    // Pede a colecao de novo sem capturar nada (o site responde com OnCollection).
    public static void PedirColecao()
    {
        Debug.Log(Tag + "collection-request enviado");
        EmuseuBridge.SendToReact("collection-request", "{}");
    }

    static void Preencher(EmuseuPayload payload)
    {
        GarantirNaPonte();
        porFrame.Clear(); framePorAlbumNumero.Clear();
        var albums = payload != null && payload.campaign != null ? payload.campaign.albums : null;
        if (albums != null)
            foreach (var album in albums)
            {
                if (album == null || album.pages == null || string.IsNullOrEmpty(album.id)) continue;
                int total = 0;
                foreach (var pg in album.pages) if (pg != null && pg.cards != null) total += pg.cards.Count;
                foreach (var pg in album.pages)
                {
                    if (pg == null || pg.cards == null) continue;
                    foreach (var c in pg.cards)
                    {
                        if (c == null || string.IsNullOrWhiteSpace(c.frame)) continue;
                        string f = c.frame.Trim();
                        framePorAlbumNumero[album.id + "#" + c.number] = f;
                        // Mesmo frame em 2 albuns: fica o album com mais cards (igual ao CardRemoteCatalog).
                        if (porFrame.TryGetValue(f, out var old) && old.albumSize >= total) continue;
                        porFrame[f] = new Ref { albumId = album.id, number = c.number, albumSize = total };
                    }
                }
            }
        configRecebido = true;
        Debug.Log(Tag + "play-config recebido, frames mapeados: " + porFrame.Count);

        // Colecao que chegou antes do play-config: resolve agora.
        if (donasApi.Count > 0) ResolverDonas(true);

        if (pendentes.Count > 0)
        {
            var copia = new List<string>(pendentes);
            pendentes.Clear();
            Debug.Log(Tag + "enviando fila: " + string.Join(",", copia));
            foreach (var f in copia) Enviar(f);
        }
    }

    // ---- site -> Unity ----

    // Chamado pelo site: SendMessage("EmuseuBridge", "OnCollection", json). Aceita envelope ou so o payload.
    // [Preserve]: no WebGL (IL2CPP) metodos chamados so por SendMessage seriam cortados.
    [Preserve]
    public void OnCollection(string json)
    {
        ProcessarColecao(json, true);
    }

    static void ProcessarColecao(string json, bool aplicarNoPlayerConfig)
    {
        if (string.IsNullOrEmpty(json)) { Debug.LogWarning(Tag + "OnCollection vazio, ignorado"); return; }
        int totalNoSite = 0;
        donasApi.Clear();
        try
        {
            var raiz = JToken.Parse(json) as JObject;
            if (raiz == null) { Debug.LogWarning(Tag + "OnCollection: json nao e um objeto"); return; }
            var payload = raiz["payload"] as JObject ?? raiz;
            var albums = payload.SelectToken("user.albums") as JArray;
            if (albums != null)
                foreach (var a in albums)
                {
                    string albumId = (string)a["albumId"];
                    var cards = a["cards"] as JArray;
                    if (string.IsNullOrEmpty(albumId) || cards == null) continue;
                    foreach (var c in cards)
                    {
                        int? n = c.Value<int?>("number");
                        if (n == null) continue;
                        totalNoSite++;
                        donasApi.Add(albumId + "#" + n.Value);
                    }
                }
        }
        catch (Exception ex) { Debug.LogWarning(Tag + "OnCollection invalido: " + ex.Message); return; }

        Debug.Log(Tag + "colecao recebida: " + totalNoSite + " cartas no site" + (configRecebido ? "" : " (play-config ainda nao chegou, mapeio depois)"));
        if (configRecebido) ResolverDonas(aplicarNoPlayerConfig);
    }

    // Converte donasApi ("albumId#number") em frames, marca no PlayerConfig e avisa ouvintes.
    static void ResolverDonas(bool aplicarNoPlayerConfig)
    {
        framesDonos.Clear();
        var frames = new List<string>();
        foreach (var chave in donasApi)
            if (framePorAlbumNumero.TryGetValue(chave, out var f)) { frames.Add(f); framesDonos.Add(f); }

        int novas = 0;
        var cfg = aplicarNoPlayerConfig ? PlayerConfig.Instancia : null;
        if (cfg != null)
        {
            foreach (var f in frames)
                if (!cfg.TemCarta(f)) { cfg.AdicionarCarta(f); novas++; }
        }
        else if (aplicarNoPlayerConfig) Debug.LogWarning(Tag + "PlayerConfig.Instancia nulo (Resources/PlayerConfig), cartas nao marcadas");

        Debug.Log(Tag + "colecao aplicada: " + frames.Count + " cartas com frame (" + string.Join(",", frames) + "), novas no PlayerConfig: " + novas);
        try { ColecaoRecebida?.Invoke(frames); }
        catch (Exception ex) { Debug.LogWarning(Tag + "listener de ColecaoRecebida falhou: " + ex.Message); }
    }

#if UNITY_EDITOR
    // ---- Teste no Editor (menu Tools > Emuseu > Teste Album Card) ----

    // Simula o jogador terminar o puzzle da carta 'frame'. Sem play-config real, usa um falso (album-teste, F11..F50 = numero 1..40).
    public static void TesteEditor(string frame)
    {
        TesteConfigFalso();
        enviadas.Remove(frame);
        Debug.Log(Tag + "[teste] simulando fim do puzzle com carta " + frame);
        Debug.Log(Tag + "carta resolvida: " + frame + " (teste)");
        Enviar(frame);
    }

    public static void TesteConfigFalso()
    {
        if (!configRecebido)
        {
            Debug.Log(Tag + "[teste] sem play-config real, usando album falso 'album-teste'");
            var cards = new List<EmuseuCard>();
            for (int i = 11; i <= 50; i++) cards.Add(new EmuseuCard { id = "card-" + i, number = i - 10, frame = "F" + i });
            Preencher(new EmuseuPayload
            {
                campaign = new EmuseuCampaign
                {
                    albums = new List<EmuseuAlbum>
                    {
                        new EmuseuAlbum { id = "album-teste", pages = new List<EmuseuPage> { new EmuseuPage { cards = cards } } }
                    }
                }
            });
        }
    }

    // Simula o site mandando OnCollection (sem mexer no PlayerConfig).
    public static void TesteColecaoEditor(string json)
    {
        ProcessarColecao(json, false);
    }

    public static string TesteEnvelopeColecao(string albumId, int numero)
    {
        return "{\"version\":1,\"channel\":\"unity-react\",\"type\":\"collection\",\"payload\":{\"user\":{\"id\":\"teste\",\"albums\":[{\"albumId\":\"" +
               albumId + "\",\"collectedCount\":1,\"cards\":[{\"cardId\":\"c\",\"number\":" + numero + "}]}]}}}";
    }
#endif
}
