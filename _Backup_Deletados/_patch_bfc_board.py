from pathlib import Path

path = Path(r"H:\NeuronPlay\Projetos\E_Virtual\Assets\Scripts\FinalCard\FinalCardBoard.cs")
text = path.read_text(encoding="utf-8")

old = """void AtualizarTextos()
    {
        if (atual == null) return;
        string t = ingles && !string.IsNullOrEmpty(atual.titleEN) ? atual.titleEN : atual.title;
        string d = ingles && !string.IsNullOrEmpty(atual.descriptionEN) ? atual.descriptionEN : atual.description;
        if (ingles && string.IsNullOrEmpty(atual.titleEN)) Log("card " + atual.id + " sem titleEN, usando PT");"""

new = """void AtualizarTextos()
    {
        if (atual == null) return;
        // Alinha ao idioma global (site header / Idioma.Definir). Sem isso o board pode ficar em PT
        // enquanto o catalogo ja tem titleEN do pacote eng.
        ingles = Idioma.Ingles;
        string t = ingles && !string.IsNullOrEmpty(atual.titleEN) ? atual.titleEN : atual.title;
        string d = ingles && !string.IsNullOrEmpty(atual.descriptionEN) ? atual.descriptionEN : atual.description;
        if (ingles && string.IsNullOrEmpty(atual.titleEN)) Log("card " + atual.id + " sem titleEN, usando PT");"""

if old not in text:
    raise SystemExit("AtualizarTextos block not found")
text = text.replace(old, new, 1)

# AlternarIdioma should also update global Idioma so site sync stays consistent when using in-board button
old_alt = """public void AlternarIdioma()
    {
        ingles = !ingles;
        AtualizarTextos();
        AtualizarBotao();
        Log("idioma: " + (ingles ? "EN" : "BR"));
    }"""

new_alt = """public void AlternarIdioma()
    {
        Idioma.Definir(!Idioma.Ingles); // SetIngles via AplicarFinalCards + AtualizarTextos
        Log("idioma: " + (Idioma.Ingles ? "EN" : "BR"));
    }"""

if old_alt not in text:
    raise SystemExit("AlternarIdioma block not found")
text = text.replace(old_alt, new_alt, 1)

path.write_text(text, encoding="utf-8")
print("OK FinalCardBoard.cs")
