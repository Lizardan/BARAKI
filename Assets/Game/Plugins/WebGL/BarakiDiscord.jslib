mergeInto('LibraryManager').library = {
  Baraki_GetDiscordSessionJson: function () {
    var payload = (typeof window !== 'undefined' && window.__BARAKI_DISCORD_SESSION__)
      ? JSON.stringify(window.__BARAKI_DISCORD_SESSION__)
      : '';
    var length = lengthBytesUTF8(payload) + 1;
    var buffer = _malloc(length);
    stringToUTF8(payload, buffer, length);
    return buffer;
  }
};
