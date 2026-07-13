using System;
using System.Collections.Generic;
using System.Text;
using Services;
using UnityEngine;
using UnityEngine.Networking;

namespace OutfitStudio
{
    /// <summary>
    /// Browses the Decentraland marketplace catalog (GET /v1/items).
    ///
    /// Callback-based (instead of Awaitable) so it works both in play mode and in the editor
    /// (the Outfit Studio window browses the catalog without entering play mode).
    /// Uses the same environment switch (org/zone) as <see cref="APIService"/>.
    /// </summary>
    public static class CatalogService
    {
        private static string EndpointItems =>
            $"https://marketplace-api.decentraland.{APIService.Environment}/v1/items";

        public static void Search(CatalogQuery query, Action<CatalogPage> onSuccess, Action<string> onError)
        {
            var url = BuildUrl(query);
            var request = UnityWebRequest.Get(url);
            var operation = request.SendWebRequest();

            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke($"{request.error} ({url})");
                        return;
                    }

                    var page = JsonUtility.FromJson<CatalogPage>(request.downloadHandler.text);

                    if (page?.data == null)
                    {
                        onError?.Invoke($"Unexpected catalog response ({url})");
                        return;
                    }

                    onSuccess?.Invoke(page);
                }
                catch (Exception e)
                {
                    onError?.Invoke(e.Message);
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        private static string BuildUrl(CatalogQuery query)
        {
            var sb = new StringBuilder(EndpointItems);

            sb.AppendFormat("?first={0}&skip={1}", query.First, query.Skip);

            if (query.Urns is { Length: > 0 })
            {
                // Direct URN lookup ignores the browse filters
                foreach (var urn in query.Urns)
                    sb.AppendFormat("&urn={0}", UnityWebRequest.EscapeURL(urn));

                return sb.ToString();
            }

            if (!string.IsNullOrEmpty(query.Category))
                sb.AppendFormat("&category={0}", query.Category);
            if (!string.IsNullOrEmpty(query.Search))
                sb.AppendFormat("&search={0}", UnityWebRequest.EscapeURL(query.Search));
            if (!string.IsNullOrEmpty(query.WearableCategory))
                sb.AppendFormat("&wearableCategory={0}", query.WearableCategory);
            if (!string.IsNullOrEmpty(query.EmoteCategory))
                sb.AppendFormat("&emoteCategory={0}", query.EmoteCategory);
            if (!string.IsNullOrEmpty(query.Rarity))
                sb.AppendFormat("&rarity={0}", query.Rarity);
            if (!string.IsNullOrEmpty(query.Gender))
            {
                if (query.Category == "emote")
                    sb.AppendFormat("&emoteGender={0}", query.Gender);
                else
                    sb.AppendFormat("&wearableGender={0}", query.Gender);
            }

            if (!string.IsNullOrEmpty(query.SortBy))
                sb.AppendFormat("&sortBy={0}", query.SortBy);

            return sb.ToString();
        }
    }
}
