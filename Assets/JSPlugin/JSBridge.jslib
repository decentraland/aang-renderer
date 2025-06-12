mergeInto(LibraryManager.library, {

  OnScreenshotTaken: function (strPtr) {
    const base64str = UTF8ToString(strPtr);
    const link = document.createElement('a');
    link.href = 'data:image/png;base64,' + base64str;
    link.download = 'screenshot.png';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

});