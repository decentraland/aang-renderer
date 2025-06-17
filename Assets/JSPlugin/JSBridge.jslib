mergeInto(LibraryManager.library, {
  OnScreenshotTaken: function (strPtr) {
    const base64str = UTF8ToString(strPtr);
    window.parent.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'unity-screenshot',
          payload: base64str
        }
      },
      '*'
    );
  },
  OnRenderCompleted: function () {
    window.parent.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          'type': 'ready',
          'payload': true
        }
      },
      '*'
    );
  }
});