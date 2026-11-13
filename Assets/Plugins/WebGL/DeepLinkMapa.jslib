// Deep link de mapa (DeepLinkMapa.cs): le um parametro (ex.: "mapa") direto do navegador.
// Ordem: window.location (query e hash), window.parent / window.top se for do mesmo dominio, sessionStorage "emuseu.<nome>".
// Tambem instala window.emuseuIrParaMapa(slug) e dispara eventos emuseu:mapa-* na window.
mergeInto(LibraryManager.library, {
  DeepLinkMapa_LerParametro: function (nomePtr) {
    var nome = UTF8ToString(nomePtr);
    function daUrl(loc) {
      try {
        if (!loc) return null;
        var s = loc.search || "";
        var h = loc.hash || "";
        var qi = h.indexOf("?");
        var textos = [s, qi >= 0 ? h.substring(qi) : ""];
        for (var i = 0; i < textos.length; i++) {
          if (!textos[i]) continue;
          var v = new URLSearchParams(textos[i]).get(nome);
          if (v) return v;
        }
      } catch (e) {}
      return null;
    }
    var valor = null;
    try { valor = daUrl(window.location); } catch (e) {}
    if (!valor) { try { if (window.parent && window.parent !== window) valor = daUrl(window.parent.location); } catch (e) {} }
    if (!valor) { try { if (window.top && window.top !== window) valor = daUrl(window.top.location); } catch (e) {} }
    if (!valor) { try { valor = window.sessionStorage ? window.sessionStorage.getItem("emuseu." + nome) : null; } catch (e) {} }
    if (!valor) valor = "";
    var tam = lengthBytesUTF8(valor) + 1;
    var buf = _malloc(tam);
    stringToUTF8(valor, buf, tam);
    return buf;
  },

  DeepLinkMapa_InstalarPonte: function () {
    function enviar(slug) {
      try {
        if (typeof SendMessage === "function") {
          SendMessage("DeepLinkMapa", "IrParaMapa", String(slug || ""));
          return true;
        }
      } catch (e) {}
      return false;
    }
    var fila = (typeof window !== "undefined" && window.__emuseuMapaFila) ? window.__emuseuMapaFila.slice() : [];
    if (typeof window !== "undefined") window.__emuseuMapaFila = [];
    if (typeof window !== "undefined") {
      window.emuseuIrParaMapa = function (slug) {
        if (!enviar(slug)) {
          window.__emuseuMapaFila = window.__emuseuMapaFila || [];
          window.__emuseuMapaFila.push(slug);
        }
      };
    }
    for (var i = 0; i < fila.length; i++) enviar(fila[i]);
    try { window.dispatchEvent(new CustomEvent("emuseu:mapa-pronto")); } catch (e) {}
  },

  DeepLinkMapa_AvisarSite: function (eventoPtr, slugPtr, cenaPtr) {
    var evento = UTF8ToString(eventoPtr);
    var slug = UTF8ToString(slugPtr);
    var cena = UTF8ToString(cenaPtr);
    try {
      window.dispatchEvent(new CustomEvent("emuseu:" + evento, { detail: { mapa: slug, cena: cena } }));
    } catch (e) {}
  },
});
