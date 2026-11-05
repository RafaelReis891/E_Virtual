from pathlib import Path

path = Path(r"H:\NeuronPlay\Projetos\E_Virtual\Assets\Scripts\Emuseu\CardRemoteCatalog.cs")
text = path.read_text(encoding="utf-8")

old_midia = """static readonly Dictionary<string, EmuseuCard> midiaPtPorId = new Dictionary<string, EmuseuCard>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, EmuseuCard> midiaPtPorFrame = new Dictionary<string, EmuseuCard>(StringComparer.OrdinalIgnoreCase);"""

new_midia = """static readonly Dictionary<string, EmuseuCard> midiaPtPorId = new Dictionary<string, EmuseuCard>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, EmuseuCard> midiaPtPorFrame = new Dictionary<string, EmuseuCard>(StringComparer.OrdinalIgnoreCase);

    // Textos PT/EN por frame. O site manda pacotes separados (campaign.ptbr / campaign.eng)
    // com title/description no idioma do pacote — sem titleEn no mesmo card.
    // O board espera title + titleEN ao mesmo tempo (AtualizarTextos).
    class TextoCard
    {
        public string title;
        public string description;
    }
    static readonly Dictionary<string, TextoCard> textoPtPorFrame = new Dictionary<string, TextoCard>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, TextoCard> textoEnPorFrame = new Dictionary<string, TextoCard>(StringComparer.OrdinalIgnoreCase);"""

if old_midia not in text:
    raise SystemExit("midia dicts not found")
text = text.replace(old_midia, new_midia, 1)

old_preencher_start = """public static void Preencher(EmuseuPayload payload)
    {
        cards.Clear();
        // Midia (imagem/video) nao depende do idioma: o pacote eng vem sem URL, entao herda do ptbr.
        MontarMidiaPt(payload);"""

new_preencher_start = """public static void Preencher(EmuseuPayload payload)
    {
        cards.Clear();
        // Idioma do payload ANTES de avisar os boards (Changed), senao o 1o paint usa ingles=false.
        SincronizarIdioma(payload);
        // Textos dos dois pacotes: board precisa de title+titleEN juntos (site nao manda titleEn no mesmo card).
        IndexarTextos(payload);
        // Midia (imagem/video) nao depende do idioma: o pacote eng vem sem URL, entao herda do ptbr.
        MontarMidiaPt(payload);"""

if old_preencher_start not in text:
    raise SystemExit("Preencher start not found")
text = text.replace(old_preencher_start, new_preencher_start, 1)

old_aplicar_text = """if (!Vazio(r.title))
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
        else if (!Vazio(r.descriptionEn)) d.descriptionEN = r.descriptionEn;"""

new_aplicar_text = """// Textos: ptbr.title -> title, eng.title -> titleEN (pacotes separados do site).
        // titleEn/descriptionEn no mesmo card (legado) tem prioridade sobre o pacote eng.
        // Sem texto remoto naquele idioma, mantem o local do cards.json (nao apaga).
        string frameKey = !Vazio(r.frame) ? r.frame.Trim() : id.Trim();
        TextoCard ptTxt, enTxt;
        bool temPt = textoPtPorFrame.TryGetValue(frameKey, out ptTxt);
        bool temEn = textoEnPorFrame.TryGetValue(frameKey, out enTxt);

        if (temPt)
        {
            if (!Vazio(ptTxt.title)) d.title = ptTxt.title;
            if (!Vazio(ptTxt.description)) { d.description = ptTxt.description; d.shortDescription = ptTxt.description; }
        }
        if (temEn)
        {
            if (!Vazio(enTxt.title)) d.titleEN = enTxt.title;
            if (!Vazio(enTxt.description)) d.descriptionEN = enTxt.description;
        }

        // Campos bilingues no mesmo card (payload antigo / admin com titleEn).
        if (!Vazio(r.titleEn)) d.titleEN = r.titleEn;
        if (!Vazio(r.descriptionEn)) d.descriptionEN = r.descriptionEn;

        // Fallback: sem indice ptbr/eng (payload so com campaign.albums), usa o card ativo.
        // Nao zera titleEN/descriptionEN locais se o remoto nao trouxe o outro idioma.
        if (!temPt && !temEn)
        {
            if (!Vazio(r.title)) d.title = r.title;
            if (!Vazio(r.description)) { d.description = r.description; d.shortDescription = r.description; }
            if (!Vazio(r.titleEn)) d.titleEN = r.titleEn;
            if (!Vazio(r.descriptionEn)) d.descriptionEN = r.descriptionEn;
        }
        else if (!temPt && !Vazio(r.title) && Vazio(d.title))
        {
            // So existe pacote eng: se local nao tinha title, usa o ativo (melhor do que vazio).
            d.title = r.title;
        }"""

if old_aplicar_text not in text:
    raise SystemExit("AplicarSobre text block not found")
text = text.replace(old_aplicar_text, new_aplicar_text, 1)

old_vazio = """static bool Vazio(string s) { return string.IsNullOrWhiteSpace(s); }
}"""

new_helpers = """static void SincronizarIdioma(EmuseuPayload payload)
    {
        if (payload == null || string.IsNullOrEmpty(payload.language)) return;
        string lang = payload.language.Trim().ToLowerInvariant();
        bool ingles = lang == "eng" || lang == "en" || lang.StartsWith("en-");
        Idioma.Definir(ingles);
    }

    // Indexa title/description de cada pacote por frame. Se o card ja trouxer titleEn no mesmo objeto, tambem indexa EN.
    static void IndexarTextos(EmuseuPayload payload)
    {
        textoPtPorFrame.Clear();
        textoEnPorFrame.Clear();
        var campaign = payload != null ? payload.campaign : null;
        if (campaign == null) return;

        IndexarTextosPacote(campaign.ptbr != null ? campaign.ptbr.albums : null, textoPtPorFrame, textoEnPorFrame);
        IndexarTextosPacote(campaign.eng != null ? campaign.eng.albums : null, textoEnPorFrame, null);

        // Payload legado: albums no topo com title + titleEn no mesmo card.
        if (textoPtPorFrame.Count == 0 && textoEnPorFrame.Count == 0)
            IndexarTextosPacote(campaign.albums, textoPtPorFrame, textoEnPorFrame);
    }

    static void IndexarTextosPacote(List<EmuseuAlbum> albums, Dictionary<string, TextoCard> destinoPrincipal, Dictionary<string, TextoCard> destinoEnDoMesmoCard)
    {
        if (albums == null || destinoPrincipal == null) return;
        foreach (var album in albums)
        {
            if (album == null || album.pages == null) continue;
            foreach (var page in album.pages)
            {
                if (page == null || page.cards == null) continue;
                foreach (var c in page.cards)
                {
                    if (c == null || string.IsNullOrEmpty(c.frame)) continue;
                    string frame = c.frame.Trim();
                    if (!FrameValido.IsMatch(frame)) continue;

                    if (!Vazio(c.title) || !Vazio(c.description))
                    {
                        var t = new TextoCard();
                        t.title = c.title;
                        t.description = c.description;
                        destinoPrincipal[frame] = t;
                    }
                    // Mesmo card com titleEn (legado): alimenta o dicionario EN.
                    if (destinoEnDoMesmoCard != null && (!Vazio(c.titleEn) || !Vazio(c.descriptionEn)))
                    {
                        var te = new TextoCard();
                        te.title = c.titleEn;
                        te.description = c.descriptionEn;
                        destinoEnDoMesmoCard[frame] = te;
                    }
                }
            }
        }
    }

    static bool Vazio(string s) { return string.IsNullOrWhiteSpace(s); }
}"""

if old_vazio not in text:
    raise SystemExit("Vazio footer not found")
text = text.replace(old_vazio, new_helpers, 1)

path.write_text(text, encoding="utf-8")
print("OK CardRemoteCatalog.cs len=", len(text))
