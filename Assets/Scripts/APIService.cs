using System;
using Data;
using UnityEngine;
using UnityEngine.Networking;

public static class APIService
{
    private const int RETRY_COUNT = 2;

    private static string EndpointPeer => $"https://peer.decentraland.{Environment}";
    private static string EndpointMarketplace => $"https://marketplace-api.decentraland.{Environment}";
    public static string EndpointCatalyst => $"https://peer.decentraland.{Environment}/content/contents/{{0}}";

    private static string APIProfile => EndpointPeer + "/lambdas/profiles/{0}";
    private static string APIActiveEntities => EndpointPeer + "/content/entities/active";
    private static string APIMarketplaceItemID => EndpointMarketplace + "/v1/items?contractAddress={0}&itemId={1}";
    private static string APIMarketplaceTokenID => EndpointMarketplace + "/v1/nfts?contractAddress={0}&tokenId={1}";

    public static string Environment { get; set; } = "org";

    public static async Awaitable<ProfileResponse.Avatar.AvatarData> GetAvatar(string profileID)
    {
        var profile = await GetWithRetry<ProfileResponse>(APIProfile, profileID);

        var avatar = profile.avatars[0].avatar;

        // Fix colors since they come without alpha
        avatar.eyes.color.a = 1f;
        avatar.hair.color.a = 1f;
        avatar.skin.color.a = 1f;

        // Convert dcl://base-avatars/ format to proper URN format
        for (var i = 0; i < avatar.wearables.Length; i++)
        {
            if (avatar.wearables[i].StartsWith("dcl://base-avatars/"))
            {
                avatar.wearables[i] = "urn:decentraland:off-chain:base-avatars:" +
                                      avatar.wearables[i].Substring("dcl://base-avatars/".Length);
            }
        }

        return avatar;
    }

    public static Awaitable<MarketplaceItemResponse> GetMarketplaceItemFromID(string contract, string itemID) =>
        GetWithRetry<MarketplaceItemResponse>(APIMarketplaceItemID, contract, itemID);


    public static Awaitable<MarketplaceNTFResponse> GetMarketplaceItemFromToken(string contract, string tokenID) =>
        GetWithRetry<MarketplaceNTFResponse>(APIMarketplaceTokenID, contract, tokenID);

    public static Awaitable<ActiveEntity[]> GetActiveEntities(string[] pointers) =>
        PostWithRetryArray<ActiveEntity>(APIActiveEntities, new ActiveEntitiesRequest(pointers));


    private static async Awaitable<T[]> PostWithRetryArray<T>(string url, object data)
    {
        var retries = 0;

        UnityWebRequest request;

        do
        {
            request = UnityWebRequest.Post(url, JsonUtility.ToJson(data), "application/json");
            await request.SendWebRequest();
            retries++;
        } while (request.result != UnityWebRequest.Result.Success && retries <= RETRY_COUNT);

        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new Exception(request.error);
        }

        return FromJsonArray<T>(request.downloadHandler.text);
    }


    private static async Awaitable<T> GetWithRetry<T>(string url, params object[] args)
    {
        var retries = 0;

        UnityWebRequest request;

        do
        {
            request = UnityWebRequest.Get(string.Format(url, args));
            await request.SendWebRequest();
            retries++;
        } while (request.result != UnityWebRequest.Result.Success && retries <= RETRY_COUNT);

        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new Exception(request.error);
        }

        return JsonUtility.FromJson<T>(request.downloadHandler.text);
    }

    private static T[] FromJsonArray<T>(string json)
    {
        return JsonUtility.FromJson<JsonArrayWrapper<T>>($"{{\"items\":{json}}}").items;
    }

    [Serializable]
    private class JsonArrayWrapper<T>
    {
        public T[] items;
    }
}