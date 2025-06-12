mergeInto(LibraryManager.library, {
  OnScreenshotTaken: function (strPtr) {
    const base64str = UTF8ToString(strPtr);
    window.parent.postMessage(
      {
        type: 'unity-screenshot',
        data: base64str
      },
      '*'
    );
  }
});