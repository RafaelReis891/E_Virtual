from pathlib import Path
path = Path(r"H:\NeuronPlay\Projetos\E_Virtual\Assets\Scripts\FinalCard\FinalCardBoard.cs")
text = path.read_text(encoding="utf-8")
# After writing TMP texts and watcher, refresh language button
old = """FinalCardTextWatcher.Aplicado(this, titulo, t, descricao, d);
    }

    void LimparTextos()"""
new = """FinalCardTextWatcher.Aplicado(this, titulo, t, descricao, d);
        AtualizarBotao();
    }

    void LimparTextos()"""
if old not in text:
    raise SystemExit("watcher/Aplicado block not found for AtualizarBotao")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
print("board botao ok")

# Read key sections for final report
print("--- AplicarSobre excerpt ---")
lines = path.read_text(encoding="utf-8").splitlines()
# wrong file - read catalog
cat = Path(r"H:\NeuronPlay\Projetos\E_Virtual\Assets\Scripts\Emuseu\CardRemoteCatalog.cs").read_text(encoding="utf-8")
# basic braces check
print("catalog braces", cat.count("{"), cat.count("}"))
board = Path(r"H:\NeuronPlay\Projetos\E_Virtual\Assets\Scripts\FinalCard\FinalCardBoard.cs").read_text(encoding="utf-8")
print("board braces", board.count("{"), board.count("}"))
