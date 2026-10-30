using UnityEngine;

// Fica em cada texto (TMP, UI Text ou TextMesh). O PT e o texto que ja esta no objeto.
// O EN e preenchido pela ferramenta Tools > Idioma > Importar Textos.
[DisallowMultipleComponent]
public class TextoIdioma : MonoBehaviour
{
    [TextArea] public string en;
    string pt;
    bool pronto;

    void Awake() { pt = Ler(); pronto = true; }
    void OnEnable() { Idioma.OnMudou += Aplicar; Aplicar(Idioma.Ingles); }
    void OnDisable() { Idioma.OnMudou -= Aplicar; }

    void Aplicar(bool ingles)
    {
        if (!pronto) return;
        Escrever(ingles && !string.IsNullOrEmpty(en) ? en : pt);
    }

    string Ler()
    {
        var tmp = GetComponent<TMPro.TMP_Text>(); if (tmp) return tmp.text;
        var ui = GetComponent<UnityEngine.UI.Text>(); if (ui) return ui.text;
        var tm = GetComponent<TextMesh>(); if (tm) return tm.text;
        return "";
    }

    void Escrever(string s)
    {
        var tmp = GetComponent<TMPro.TMP_Text>(); if (tmp) { tmp.text = s; return; }
        var ui = GetComponent<UnityEngine.UI.Text>(); if (ui) { ui.text = s; return; }
        var tm = GetComponent<TextMesh>(); if (tm) tm.text = s;
    }
}