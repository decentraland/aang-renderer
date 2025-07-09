using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace UI
{
    public class RemoteTextureService : MonoBehaviour
    {
        private const int CONCURRENT_REQUESTS = 5;

        private readonly Dictionary<string, Texture2D> _cachedTextures = new();

        private readonly LinkedList<string> _requestQueue = new();
        private readonly HashSet<string> _requests = new();
        private readonly Dictionary<string, (Action<Texture2D> success, Action error)> _responseListeners = new();

        private static RemoteTextureService _instance;

        public static RemoteTextureService Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("RemoteTextureService");
                    _instance = go.AddComponent<RemoteTextureService>();
                }

                return _instance;
            }
        }

        public void PreloadTexture(string url)
        {
            _requestQueue.AddLast(url);
            CheckQueue();
        }

        public void RequestTexture(string url, Action<Texture2D> callback, Action error = null, bool addLast = true)
        {
            // Check if we have a cached version already
            if (_cachedTextures.TryGetValue(url, out var cachedTexture))
            {
                callback(cachedTexture);
                return;
            }

            // Check if the request is running
            if (_requests.Contains(url))
            {
                _responseListeners.Add(url, (callback, error));
                return;
            }

            // Check if the url is queued and if so remove it so it will be added at the front of the line
            if (_requestQueue.Contains(url))
            {
                _requestQueue.Remove(url);
            }

            // Add request to front of queue
            if (addLast)
            {
                _requestQueue.AddLast(url);
            }
            else
            {
                _requestQueue.AddFirst(url);
            }

            _responseListeners.Add(url, (callback, error));

            CheckQueue();
        }

        public void RemoveListener(string url)
        {
            _responseListeners.Remove(url);
        }

        private void CheckQueue()
        {
            while (_requests.Count < CONCURRENT_REQUESTS && _requestQueue.Count > 0)
            {
                var url = _requestQueue.First.Value;
                _requestQueue.RemoveFirst();

                StartCoroutine(LoadImage(url));
                //var coroutine = _coroutineRunner.StartCoroutine(request);
            }
        }

        private async Awaitable LoadImage(string url)
        {
            _requests.Add(url);

            //await Awaitable.BackgroundThreadAsync();

            Debug.Log($"Loading texture URL: {url}");
            var request = UnityWebRequestTexture.GetTexture(url);

            await request.SendWebRequest();

            //await Awaitable.MainThreadAsync();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Texture loaded: {url}");
                var tex = ((DownloadHandlerTexture)request.downloadHandler).texture;
                _cachedTextures.Add(url, tex);

                if (_responseListeners.TryGetValue(url, out var listener))
                {
                    listener.success(tex);
                    _responseListeners.Remove(url);
                }
            }
            else
            {
                Debug.LogError($"Failed to load texture: {url} - {request.error}");
                if (_responseListeners.TryGetValue(url, out var listener))
                {
                    listener.error();
                    _responseListeners.Remove(url);
                }
            }

            _requests.Remove(url);

            CheckQueue();
        }
    }
}