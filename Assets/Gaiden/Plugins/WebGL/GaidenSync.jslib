// Flushes Unity's in-memory file system to IndexedDB so saves survive a page reload (WebGL only).
mergeInto(LibraryManager.library, {
  GaidenSyncFiles: function () {
    if (typeof FS !== "undefined" && FS.syncfs) {
      FS.syncfs(false, function (err) { if (err) console.warn("[Gaiden] save sync failed", err); });
    }
  }
});
