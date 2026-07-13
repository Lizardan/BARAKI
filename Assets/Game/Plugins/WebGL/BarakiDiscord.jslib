mergeInto(LibraryManager.library, {
  Baraki_GetDiscordSessionJson: function () {
    var payload = '';
    try {
      if (typeof window !== 'undefined' && window.__BARAKI_DISCORD_SESSION__) {
        payload = JSON.stringify(window.__BARAKI_DISCORD_SESSION__);
      }
    } catch (e) {
      payload = '';
    }

    var bufferSize = lengthBytesUTF8(payload) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(payload, buffer, bufferSize);
    return buffer;
  },
});
