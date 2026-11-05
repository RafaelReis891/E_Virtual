using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// Catalogo dos Final Cards vindos do site (payload play-config).
// Chave = card.frame (nosso id, ex: "F11"). So entram frames que batem com ^F\d+$.
// Se o mesmo frame aparece em mais de um album, vale o album com mais cards.
public static class CardRemoteCatalog
{
    // Se o card remoto nao tem video, mantem o video local do cards.json?
    // true = mantem (pedido atual). Ponha false para o site mandar sozinho (sem video = so imagem).
    public static bool ManterVideoLocalSeRemotoSemVideo = true;

    // Disparado quando o catalogo foi preenchido de novo (boards podem recarregar).
    public static event Action Changed;

    static readonly Regex FrameValido = new Regex(@"^F\d+$");
    static readonly Dictionary<string, EmuseuCard> cards = new Dictionary<string, EmuseuCard>(StringComparer.OrdinalIgnoreCase);

    public static int Count => cards.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar()
    {
        // Limpa e (re)assina; seguro se o Domain Reload do editor estiver desligado.
        cards.Clear();
        EmuseuBridge.PlayConfigApplied -= Preencher;
        EmuseuBridge.PlayConfigApplied += Preencher;
    }

    public static bool TryGet(string id, out EmuseuCard card)
    {
        card = null;
        if (string.IsNullOrEmpty(id)) return false;
        return cards.TryGetValue(id.Trim(), out card) && card != null;
    }

    // Junta os cards de todos os albuns do payload.
    public static void Preencher(EmuseuPayload payload)
    {
        cards.Clear();
        // Midia (imagem/video) nao depende do idioma: o pacote eng vem sem URL, entao herda do ptbr.
        MontarMidiaPt(payload);
        int herdaramMidiaPt = 0;
        var tamanhoDoAlbum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // So para debug (EmuseuDebug): nome do album de cada frame, duplicados e ignorados.
        var albumDoFrame = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var duplicados = new List<string>();
        var invalidos = new List<string>();
        int totalCards = 0, semFrame = 0, frameInvalido = 0;

        var albums = payload != null && payload.campaign != null ? payload.campaign.albums : null;
        if (albums != null)
        {
            foreach (var album in albums)
            {
                if (album == null || album.pages == null) continue;

                int total = 0;
                foreach (var page in album.pages)
                    if (page != null && page.cards != null) total += page.cards.Count;

                foreach (var page in album.pages)
                {
                    if (page == null || page.cards == null) continue;
                    foreach (var card in page.cards)
                    {
                        if (card == null) continue;
                        totalCards++;
                        if (string.IsNullOrEmpty(card.frame)) { semFrame++; continue; }
                        string frame = card.frame.Trim();
                        if (!FrameValido.IsMatch(frame)) { frameInvalido++; if (invalidos.Count < 10) invalidos.Add(frame); continue; }

                        if (HerdarMidiaPt(card, frame)) herdaramMidiaPt++;

                        // Duplicado: fica com o album que tem mais cards.
                        if (cards.ContainsKey(frame))
                        {
                            bool mantem = tamanhoDoAlbum[frame] >= total;
                            duplicados.Add(frame + ": '" + albumDoFrame[frame] + "'(" + tamanhoDoAlbum[frame] + ") x '" + album.name + "'(" + total + ") -> fica '" + (mantem ? albumDoFrame[frame] : album.name) + "'");
                            if (mantem) continue;
                        }
                        cards[frame] = card;
                        tamanhoDoAlbum[frame] = total;
                        albumDoFrame[frame] = album.name;
                    }
                }
            }
        }

        Debug.Log("[CardRemoteCatalog] " + cards.Count + " final cards remotos");
        Debug.Log("[CardRemoteCatalog] " + herdaramMidiaPt + " cards herdaram a midia do ptbr (pacote ativo sem imagem/video)");
        EmuseuDebug.CatalogoPreenchido(albums != null ? albums.Count : 0, new List<string>(cards.Keys), totalCards, semFrame, frameInvalido, invalidos, duplicados);
        EmuseuDebug.Info("catalogo", "avisando boards (Changed), ouvintes=" + (Changed != null ? Changed.GetInvocationList().Length : 0));
        try { Changed?.Invoke(); }
        catch (Exception ex) { EmuseuDebug.Error("catalogo", "listener de Changed falhou: " + ex.GetType().Name + ": " + ex.Message); Debug.LogWarning("[CardRemoteCatalog] listener falhou: " + ex.Message); }
    }

    // ---- Midia do ptbr para o pacote em ingles ----
    static readonly Dictionary<string, EmuseuCard> midiaPtPorId = new Dictionary<string, EmuseuCard>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, EmuseuCard> midiaPtPorFrame = new Dictionary<string, EmuseuCard>(StringComparer.OrdinalIgnoreCase);

    // Indexa os cards do pacote ptbr por id e por frame (so os que tem alguma URL de midia).
    static void MontarMidiaPt(EmuseuPayload payload)
    {
        midiaPtPorId.Clear();
        midiaPtPorFrame.Clear();
        var albums = payload != null && payload.campaign != null && payload.campaign.ptbr != null ? payload.campaign.ptbr.albums : null;
        if (albums == null) return;
        foreach (var album in albums)
        {
            if (album == null || album.pages == null) continue;
            foreach (var page in album.pages)
            {
                if (page == null || page.cards == null) continue;
                foreach (var c in page.cards)
                {
                    if (c == null || !(TemImagem(c) || !Vazio(c.videoUrl))) continue;
                    if (!string.IsNullOrEmpty(c.id)) midiaPtPorId[c.id.Trim()] = c;
                    if (!string.IsNullOrEmpty(c.frame)) midiaPtPorFrame[c.frame.Trim()] = c;
                }
            }
        }
    }

    static bool TemImagem(EmuseuCard c) { return !Vazio(c.imageUrl) || !Vazio(c.thumbUrl); }

    // Se o card do pacote ativo nao tem imagem (ou video), copia do card ptbr com o mesmo id (ou o mesmo frame).
    // Retorna true se herdou alguma coisa. Nao mexe em quem ja tem URL.
    static bool HerdarMidiaPt(EmuseuCard card, string frame)
    {
        if (card == null) return false;
        bool precisaImagem = !TemImagem(card);
        bool precisaVideo = Vazio(card.videoUrl);
        if (!precisaImagem && !precisaVideo) return false;

        EmuseuCard pt = null;
        if (!string.IsNullOrEmpty(card.id)) midiaPtPorId.TryGetValue(card.id.Trim(), out pt);
        if (pt == null) midiaPtPorFrame.TryGetValue(frame, out pt);
        if (pt == null) return false;
        if (ReferenceEquals(pt, card)) return false;

        bool herdou = false;
        if (precisaImagem && TemImagem(pt)) { card.imageUrl = pt.imageUrl; card.thumbUrl = pt.thumbUrl; herdou = true; }
        if (precisaVideo && !Vazio(pt.videoUrl)) { card.videoUrl = pt.videoUrl; herdou = true; }
        if (herdou && !Vazio(pt.media)) card.media = pt.media;
        return herdou;
    }

    // Devolve uma COPIA dos dados locais com os campos remotos (nao vazios) por cima.
    // Nao mexe no cards.json em cache. Sem card remoto: devolve o local como veio.
    // Se nao existe local mas existe remoto, cria o dado so com o remoto.
    public static FinalCardData AplicarSobre(string id, FinalCardData local)
    {
        if (!TryGet(id, out var r)) return local;

        var d = new FinalCardData();
        if (local != null)
        {
            d.id = local.id;
            d.filename = local.filename;
            d.orientation = local.orientation;
            d.title = local.title;
            d.description = local.description;
            d.shortDescription = local.shortDescription;
            d.image = local.image;
            d.imagePath = local.imagePath;
            d.titleEN = local.titleEN;
            d.descriptionEN = local.descriptionEN;
            d.video = local.video;
        }
        else
        {
            d.id = id;
            d.filename = id;
        }

        if (!Vazio(r.title))
        {
            d.title = r.title;
            d.titleEN = r.titleEn;          // se vier vazio, o board cai no PT (nao mistura com EN local antigo)
        }
        else if (!Vazio(r.titleEn)) d.titleEN = r.titleEn;

        if (!Vazio(r.description))
        {
            d.description = r.description;
            d.shortDescription = r.description;
            d.descriptionEN = r.descriptionEn;
        }
        else if (!Vazio(r.descriptionEn)) d.descriptionEN = r.descriptionEn;

        // Imagem: imageUrl, ou thumbUrl se nao tiver. URL http(s) passa direto no board.
        string img = !Vazio(r.imageUrl) ? r.imageUrl : r.thumbUrl;
        if (!Vazio(img)) { d.imagePath = img; d.image = img; }

        // Video: remoto se tiver; senao local (ou nenhum, conforme a opcao acima).
        if (!Vazio(r.videoUrl)) d.video = r.videoUrl;
        else if (!ManterVideoLocalSeRemotoSemVideo) d.video = null;

        return d;
    }

    static bool Vazio(string s) { return string.IsNullOrWhiteSpace(s); }
}