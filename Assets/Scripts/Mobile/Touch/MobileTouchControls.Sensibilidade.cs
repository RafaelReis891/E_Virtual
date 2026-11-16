// MobileTouchControls.Sensibilidade.cs
// Desenha o ajuste PROVISORIO de sensibilidade da camera: [ < ] 1.0 [ > ] no canto de cima a esquerda.
// A logica (valor, PlayerPrefs, toque) fica em MobileInput.Sensibilidade.cs. So aparece junto com os controles de toque.
// Esconder: MobileInput.MostrarAjusteSensibilidade = false (ou MobileInput.MostrarAjusteSensibilidadePadrao = false).
using UnityEngine;
using UnityEngine.UI;

public partial class MobileTouchControls
{
    RectTransform ajusteRaiz, ajusteMenos, ajusteNumero, ajusteMais;
    Image imgAjusteMenos, imgAjusteMais;
    Text txtAjusteNumero;
    float ajusteValorMostrado = -1f;

    static Font fonteAjuste;

    static Font FonteUI()
    {
        if (fonteAjuste != null) return fonteAjuste;
        try { fonteAjuste = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (System.Exception) { fonteAjuste = null; }
        if (fonteAjuste == null)
        {
            try { fonteAjuste = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (System.Exception) { fonteAjuste = null; }
        }
        return fonteAjuste;
    }

    void ConstruirAjusteSensibilidade(Transform pai)
    {
        var raiz = new GameObject("AjusteSensibilidade", typeof(RectTransform));
        raiz.transform.SetParent(pai, false);
        ajusteRaiz = (RectTransform)raiz.transform;
        ajusteRaiz.anchorMin = Vector2.zero;
        ajusteRaiz.anchorMax = Vector2.one;
        ajusteRaiz.offsetMin = ajusteRaiz.offsetMax = Vector2.zero;

        ajusteMenos = CriarImagem("Sensibilidade_Menos", raiz.transform, null, corBotao, true, out imgAjusteMenos);
        ajusteNumero = CriarImagem("Sensibilidade_Numero", raiz.transform, null, new Color(0f, 0f, 0f, 0.35f), true, out _);
        ajusteMais = CriarImagem("Sensibilidade_Mais", raiz.transform, null, corBotao, true, out imgAjusteMais);
        imgAjusteMenos.preserveAspect = false;
        imgAjusteMais.preserveAspect = false;

        CriarTexto("Texto", ajusteMenos, "<");
        txtAjusteNumero = CriarTexto("Texto", ajusteNumero, MobileInput.SensibilidadeTexto);
        CriarTexto("Texto", ajusteMais, ">");
        ajusteValorMostrado = MobileInput.SensibilidadeCamera;

        raiz.SetActive(false);
    }

    static Text CriarTexto(string nome, RectTransform pai, string texto)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>();
        Font f = FonteUI();
        if (f != null) t.font = f;
        t.text = texto;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 1f, 1f, 0.95f);
        t.fontStyle = FontStyle.Bold;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = 48;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    void AtualizarAjusteSensibilidade(float sf)
    {
        if (ajusteRaiz == null) return;
        bool mostrar = MobileInput.AjusteSensibilidadeNaTela;
        if (ajusteRaiz.gameObject.activeSelf != mostrar) ajusteRaiz.gameObject.SetActive(mostrar);
        if (!mostrar) return;

        PosicionarRect(ajusteMenos, MobileInput.AjusteMenosPx, sf);
        PosicionarRect(ajusteNumero, MobileInput.AjusteNumeroPx, sf);
        PosicionarRect(ajusteMais, MobileInput.AjusteMaisPx, sf);

        float agora = Time.unscaledTime;
        imgAjusteMenos.color = agora - MobileInput.UltimoToqueMenos < 0.15f ? corBotaoAtivo : corBotao;
        imgAjusteMais.color = agora - MobileInput.UltimoToqueMais < 0.15f ? corBotaoAtivo : corBotao;

        float v = MobileInput.SensibilidadeCamera;
        if (!Mathf.Approximately(v, ajusteValorMostrado))
        {
            ajusteValorMostrado = v;
            if (txtAjusteNumero != null) txtAjusteNumero.text = MobileInput.SensibilidadeTexto;
        }
    }

    static void PosicionarRect(RectTransform rt, Rect px, float sf)
    {
        rt.anchoredPosition = px.center / sf;
        rt.sizeDelta = px.size / sf;
    }
}
