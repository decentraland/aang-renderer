mergeInto(LibraryManager.library, {
  OnScreenshotTaken: function (strPtr) {
    const base64str = UTF8ToString(strPtr);
    window.parent.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'screenshot',
          payload: base64str
        }
      },
      '*'
    );
  },
  OnLoadComplete: function () {
    window.parent.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          'type': 'loaded',
          'payload': true
        }
      },
      '*'
    );
  }
});