using System;
using Data;
using UnityEngine;
using UnityEngine.Networking;

public static class APIService
{
    public const string API_CATALYST = "https://peer.decentraland.org/content/contents/{0}";
    
    private const string API_PROFILE = "https://peer.decentraland.org/lambdas/profiles/{0}";
    private const string API_ACTIVE_ENTITIES = "https://peer.decentraland.org/content/entities/active";
    private const string API_MARKETPLACE_ITEM_ID = "https://marketplace-api.decentraland.org/v1/items?contractAddress={0}&itemId={1}";
    private const string API_MARKETPLACE_TOKEN_ID = "https://marketplace-api.decentraland.org/v1/nfts?contractAddress={0}&tokenId={1}";

    public static async Awaitable<ProfileResponse.Avatar.AvatarData> GetAvatar(string profileID)
    {
        var profile = await GetProfile(profileID);
        
        var avatar = profile.avatars[0].avatar;
        
        // Fix colors since they come without alpha
        avatar.eyes.color.a = 1f;
        avatar.hair.color.a = 1f;
        avatar.skin.color.a = 1f;
        
        return avatar;
    }

    public static async Awaitable<MarketplaceItemResponse> GetMarketplaceItemFromID(string contract, string itemID)
    {
        var request = UnityWebRequest.Get(string.Format(API_MARKETPLACE_ITEM_ID, contract, itemID));
        
        await request.SendWebRequest();
        
        // TODO: Error handling

        return JsonUtility.FromJson<MarketplaceItemResponse>(request.downloadHandler.text);
    }
    
    public static async Awaitable<MarketplaceNTFResponse> GetMarketplaceItemFromToken(string contract, string tokenID)
    {
        var request = UnityWebRequest.Get(string.Format(API_MARKETPLACE_TOKEN_ID, contract, tokenID));
        
        await request.SendWebRequest();
        
        // TODO: Error handling
        
        return JsonUtility.FromJson<MarketplaceNTFResponse>(request.downloadHandler.text);
    }

    public static async Awaitable<ProfileResponse> GetProfile(string userID)
    {
        var request = UnityWebRequest.Get(string.Format(API_PROFILE, userID));

        await request.SendWebRequest();

        // TODO: Error handling

        return JsonUtility.FromJson<ProfileResponse>(request.downloadHandler.text);
    }

    public static async Awaitable<ActiveEntity[]> GetActiveEntities(string[] pointers)
    {
        using var request = UnityWebRequest.Post(API_ACTIVE_ENTITIES,
            JsonUtility.ToJson(new ActiveEntitiesRequest(pointers)), "application/json");

        await request.SendWebRequest();

        // TODO: Error handling

        return FromJsonArray<ActiveEntity>(request.downloadHandler.text);
    }

    private static T[] FromJsonArray<T>(string json)
    {
        var wrappedJson = $"{{\"items\":{json}}}";

        Debug.Log(wrappedJson);

        return JsonUtility.FromJson<JsonArrayWrapper<T>>(wrappedJson).items;
    }
    
    [Serializable]
    private class JsonArrayWrapper<T>
    {
        public T[] items;
    }
}