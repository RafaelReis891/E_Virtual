// Debug do site emuseu: expoe window.emuseuDebug no navegador (so diagnostico).
//   emuseuDebug.dump()           -> imprime resumo + ultimos eventos no console
//   emuseuDebug.text()           -> resumo + todos os eventos em texto (use copy(emuseuDebug.text()))
//   emuseuDebug.lastPayload      -> json bruto completo do ultimo play-config (use copy(emuseuDebug.lastPayload))
//   emuseuDebug.payload()        -> o mesmo json ja convertido em objeto
mergeInto(LibraryManager.library, {
  // Ajuda interna: cria window.emuseuDebug na primeira vez.
  $EmuseuDebugGet: function () {
    if (typeof window === "undefined") { return null; }
    var d = window.emuseuDebug;
    if (!d) {
      d = {
        versao: 1,
        lastPayload: "",
        lastPayloadAt: "",
        summary: "",
        log: [],
        dump: function () {
          var x = window.emuseuDebug;
          console.log("[Emuseu] ===== RESUMO =====\n" + x.summary);
          console.log("[Emuseu] payload: " + x.lastPayload.length + " chars. Para copiar: copy(emuseuDebug.lastPayload)");
          console.log("[Emuseu] ===== ULTIMOS EVENTOS (" + Math.min(60, x.log.length) + " de " + x.log.length + ") =====\n" + x.log.slice(-60).join("\n"));
          return "ok (resumo impresso acima)";
        },
        text: function () {
          var x = window.emuseuDebug;
          return x.summary + "\n---- eventos ----\n" + x.log.join("\n");
        },
        payload: function () {
          var x = window.emuseuDebug;
          try { return JSON.parse(x.lastPayload); } catch (e) { console.error("[Emuseu] lastPayload nao e json valido", e); return null; }
        }
      };
      window.emuseuDebug = d;
    }
    return d;
  },

  // Guarda o json bruto completo do ultimo play-config.
  EmuseuDebug_SetPayload__deps: ["$EmuseuDebugGet"],
  EmuseuDebug_SetPayload: function (strPtr) {
    var d = EmuseuDebugGet();
    if (!d) { return; }
    d.lastPayload = UTF8ToString(strPtr);
    d.lastPayloadAt = new Date().toISOString();
  },

  // Acrescenta uma linha no log (guarda as ultimas 300).
  EmuseuDebug_Push__deps: ["$EmuseuDebugGet"],
  EmuseuDebug_Push: function (strPtr) {
    var d = EmuseuDebugGet();
    if (!d) { return; }
    d.log.push(UTF8ToString(strPtr));
    if (d.log.length > 300) { d.log.splice(0, d.log.length - 300); }
  },

  // Guarda o texto de resumo atual.
  EmuseuDebug_SetSummary__deps: ["$EmuseuDebugGet"],
  EmuseuDebug_SetSummary: function (strPtr) {
    var d = EmuseuDebugGet();
    if (!d) { return; }
    d.summary = UTF8ToString(strPtr);
  }
});