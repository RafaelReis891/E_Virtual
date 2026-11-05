from pathlib import Path
path = Path(r"H:\NeuronPlay\Projetos\E_Virtual\Assets\Scripts\Emuseu\CardRemoteCatalog.cs")
text = path.read_text(encoding="utf-8")
old = """Debug.Log("[CardRemoteCatalog] " + cards.Count + " final cards remotos");
        Debug.Log("[CardRemoteCatalog] " + herdaramMidiaPt + " cards herdaram a midia do ptbr (pacote ativo sem imagem/video)");"""
new = """Debug.Log("[CardRemoteCatalog] " + cards.Count + " final cards remotos");
        Debug.Log("[CardRemoteCatalog] textos pt=" + textoPtPorFrame.Count + " en=" + textoEnPorFrame.Count);
        Debug.Log("[CardRemoteCatalog] " + herdaramMidiaPt + " cards herdaram a midia do ptbr (pacote ativo sem imagem/video)");"""
if old not in text:
    raise SystemExit("debug log block not found")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
print("log ok")
