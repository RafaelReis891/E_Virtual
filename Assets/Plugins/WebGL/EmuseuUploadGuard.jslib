// Complemento do EmuseuBridge.jslib (arquivo novo): solta o pointer lock do navegador e tira o foco do canvas
// enquanto o modal de upload do React esta aberto (ver EmuseuUploadModalGuard.cs).
mergeInto(LibraryManager.library, {
  EmuseuUploadGuard_ReleasePointer: function () {
    try {
      if (typeof document !== "undefined" && document.exitPointerLock) document.exitPointerLock();
    } catch (err) {
      if (typeof console !== "undefined") console.warn("[EmuseuUploadGuard] exitPointerLock falhou", err);
    }
  },
  EmuseuUploadGuard_BlurCanvas: function () {
    try {
      if (typeof document === "undefined") return;
      var a = document.activeElement;
      if (a && a.tagName === "CANVAS" && typeof a.blur === "function") a.blur();
    } catch (err) {
      if (typeof console !== "undefined") console.warn("[EmuseuUploadGuard] blur do canvas falhou", err);
    }
  },
});