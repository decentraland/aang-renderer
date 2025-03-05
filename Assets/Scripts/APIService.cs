using System;
using Data;
using UnityEngine;
using UnityEngine.Networking;

public static class APIService
{
    private const string ENDPOINT_PEER = "https://peer.decentraland.org";
    private const string ENDPOINT_MARKETPLACE = "https://marketplace-api.decentraland.org";

    public const string API_CATALYST = "https://peer.decentraland.org/content/contents/{0}";
    private const string API_PROFILE = ENDPOINT_PEER + "/lambdas/profiles/{0}";
    private const string API_ACTIVE_ENTITIES = ENDPOINT_PEER + "/content/entities/active";
    private const string API_MARKETPLACE_ITEM_ID = ENDPOINT_MARKETPLACE + "/v1/items?contractAddress={0}&itemId={1}";
    private const string API_MARKETPLACE_TOKEN_ID = ENDPOINT_MARKETPLACE + "/v1/nfts?contractAddress={0}&tokenId={1}";

    private const int RETRY_COUNT = 2;

    public static async Awaitable<ProfileResponse.Avatar.AvatarData> GetAvatar(string profileID)
    {
        var profile = await GetWithRetry<ProfileResponse>(API_PROFILE, profileID);

        var avatar = profile.avatars[0].avatar;

        // Fix colors since they come without alpha
        avatar.eyes.color.a = 1f;
        avatar.hair.color.a = 1f;
        avatar.skin.color.a = 1f;

        return avatar;
    }

    public static Awaitable<MarketplaceItemResponse> GetMarketplaceItemFromID(string contract, string itemID) =>
        GetWithRetry<MarketplaceItemResponse>(API_MARKETPLACE_ITEM_ID, contract, itemID);


    public static Awaitable<MarketplaceNTFResponse> GetMarketplaceItemFromToken(string contract, string tokenID) =>
        GetWithRetry<MarketplaceNTFResponse>(API_MARKETPLACE_TOKEN_ID, contract, tokenID);

    public static Awaitable<ActiveEntity[]> GetActiveEntities(string[] pointers) =>
        PostWithRetryArray<ActiveEntity>(API_ACTIVE_ENTITIES, new ActiveEntitiesRequest(pointers));


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