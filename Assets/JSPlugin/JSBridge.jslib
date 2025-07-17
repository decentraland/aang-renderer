mergeInto(LibraryManager.library, {
  OnScreenshotTaken: function (strPtr) {
    const base64str = UTF8ToString(strPtr)
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'screenshot',
          payload: base64str,
        },
      },
      '*'
    )
  },
  OnLoadComplete: function () {
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'loaded',
          payload: true,
        },
      },
      '*'
    )
  },
  OnError: function (strPtr) {
    const messageStr = UTF8ToString(strPtr)
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'error',
          payload: messageStr,
        },
      },
      '*'
    )
  },
  PreloadURLs: function(strPtr) {
    const csv = UTF8ToString(strPtr);
    const urls = csv.split(',');
     
    for (const url of urls) {
      fetch(url, { cache: 'force-cache' }).catch(() => {});
    }
  }
})
