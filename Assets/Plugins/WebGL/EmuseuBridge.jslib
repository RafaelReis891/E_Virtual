// Ponte Unity -> React (site emuseu). Recebe o JSON do envelope e entrega em window.emuseu.receive(obj).
mergeInto(LibraryManager.library, {
  EmuseuBridge_SendToReact: function (jsonPtr) {
    var raw = UTF8ToString(jsonPtr);
    try {
      var parsed = JSON.parse(raw);
      if (typeof window !== "undefined" && window.emuseu && typeof window.emuseu.receive === "function") {
        window.emuseu.receive(parsed);
      } else if (typeof console !== "undefined") {
        console.warn("[EmuseuBridge] window.emuseu.receive indisponivel", parsed);
      }
    } catch (err) {
      if (typeof console !== "undefined") {
        console.error("[EmuseuBridge] SendToReact falhou", err, raw);
      }
    }
  },
});