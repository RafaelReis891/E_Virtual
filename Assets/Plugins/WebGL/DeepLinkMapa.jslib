// Deep link de mapa (DeepLinkMapa.cs): le um parametro (ex.: "mapa") direto do navegador.
// Ordem: window.location (query e hash), window.parent / window.top se for do mesmo dominio, sessionStorage "emuseu.<nome>".
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
});
