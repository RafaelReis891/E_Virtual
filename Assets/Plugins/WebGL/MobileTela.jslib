// MobileTela.jslib (MobileTela.cs): tela cheia + travar na horizontal no primeiro toque na pagina.
// O navegador so aceita requestFullscreen / screen.orientation.lock dentro de um gesto do usuario,
// por isso o ouvinte fica aqui no JavaScript (touchend / pointerup na pagina), e nao no Update do Unity.
// - Uma tentativa por gesto; depois desliga os ouvintes. Se o usuario sair da tela cheia, arma de novo.
// - iPhone Safari nao tem tela cheia de pagina: nao faz nada (sem erro). iPad usa webkitRequestFullscreen.
// - orientation.lock so funciona em tela cheia (Android Chrome); erros sao ignorados.
mergeInto(LibraryManager.library, {
  MobileTela_Armar: function (telaCheia, travar, log) {
    var w = window, d = document;
    var st = w.__emuseuMobileTela;
    if (!st) {
      st = w.__emuseuMobileTela = { armado: false, ativo: false, ouvindo: false, telaCheia: false, travar: false, log: false, falhas: 0, ultimo: 0 };
      st.dbg = function (m) { if (st.log) { try { console.log("[MobileTela] " + m); } catch (e) {} } };
      st.emTelaCheia = function () {
        return !!(d.fullscreenElement || d.webkitFullscreenElement || d.webkitCurrentFullScreenElement || d.mozFullScreenElement || d.msFullscreenElement);
      };
      st.pedirTelaCheia = function () {
        var el = d.documentElement;
        var f = el.requestFullscreen || el.webkitRequestFullscreen || el.webkitRequestFullScreen || el.mozRequestFullScreen || el.msRequestFullscreen;
        if (!f) return null; // iPhone Safari
        try {
          var r = (f === el.requestFullscreen) ? f.call(el, { navigationUI: "hide" }) : f.call(el);
          return (r && typeof r.then === "function") ? r : Promise.resolve();
        } catch (e) {
          return Promise.reject(e);
        }
      };
      st.travarHorizontal = function () {
        if (!st.travar) return;
        try {
          var o = w.screen && w.screen.orientation;
          if (o && typeof o.lock === "function") {
            var p = o.lock("landscape");
            if (p && typeof p.then === "function") {
              p.then(function () { st.dbg("travado na horizontal"); }, function (e) { st.dbg("trava de orientacao recusada: " + (e && (e.name || e.message))); });
            }
          } else {
            st.dbg("sem screen.orientation.lock (iPhone/iPad?)");
          }
        } catch (e) {
          st.dbg("trava de orientacao falhou: " + (e && (e.name || e.message)));
        }
      };
      st.aoGesto = function () {
        if (!st.armado || !st.ativo) return;
        var agora = Date.now();
        if (agora - st.ultimo < 300) return; // touchend + pointerup do mesmo toque
        st.ultimo = agora;
        st.armado = false;
        st.desligarOuvintes();
        if (st.telaCheia && !st.emTelaCheia()) {
          var p = st.pedirTelaCheia();
          if (!p) {
            st.dbg("navegador sem tela cheia de pagina (iPhone?)");
            st.travarHorizontal();
            return;
          }
          p.then(function () {
            st.falhas = 0;
            st.dbg("tela cheia ok");
            st.travarHorizontal();
          }, function (e) {
            st.falhas++;
            st.dbg("tela cheia recusada (" + st.falhas + "): " + (e && (e.name || e.message)));
            if (st.falhas < 3) st.armar();
          });
        } else {
          st.travarHorizontal();
        }
      };
      st.ligarOuvintes = function () {
        if (st.ouvindo) return;
        st.ouvindo = true;
        var op = { capture: true, passive: true };
        d.addEventListener("touchend", st.aoGesto, op);
        d.addEventListener("pointerup", st.aoGesto, op);
      };
      st.desligarOuvintes = function () {
        if (!st.ouvindo) return;
        st.ouvindo = false;
        d.removeEventListener("touchend", st.aoGesto, true);
        d.removeEventListener("pointerup", st.aoGesto, true);
      };
      st.armar = function () {
        if (!st.ativo) return;
        st.armado = true;
        st.ligarOuvintes();
      };
      st.aoMudarTelaCheia = function () {
        if (!st.ativo) return;
        if (!st.emTelaCheia()) {
          st.dbg("saiu da tela cheia: arma de novo para o proximo toque");
          st.falhas = 0;
          st.armar();
        }
      };
      d.addEventListener("fullscreenchange", st.aoMudarTelaCheia);
      d.addEventListener("webkitfullscreenchange", st.aoMudarTelaCheia);
    }
    st.telaCheia = !!telaCheia;
    st.travar = !!travar;
    st.log = !!log;
    st.ativo = true;
    st.falhas = 0;
    if (st.telaCheia && st.emTelaCheia()) {
      st.armado = false;
      st.travarHorizontal();
    } else {
      st.armar();
    }
    st.dbg("armado (tela cheia=" + st.telaCheia + ", travar=" + st.travar + ")");
  },

  MobileTela_Desarmar: function () {
    var st = window.__emuseuMobileTela;
    if (!st) return;
    st.ativo = false;
    st.armado = false;
    st.desligarOuvintes();
    st.dbg("desarmado");
  },

  MobileTela_EmTelaCheia: function () {
    var d = document;
    return (d.fullscreenElement || d.webkitFullscreenElement || d.webkitCurrentFullScreenElement || d.mozFullScreenElement || d.msFullscreenElement) ? 1 : 0;
  },
});
