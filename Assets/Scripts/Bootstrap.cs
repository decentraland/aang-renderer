using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PreviewLoader previewLoader;
    [SerializeField] private Material baseMat;
    [SerializeField] private Material facialFeaturesMat;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private PreviewRotator previewRotator;

    private void Start()
    {
        // Common assets TODO: Improve maybe
        CommonAssets.AvatarMaterial = baseMat;
        CommonAssets.FacialFeaturesMaterial = facialFeaturesMat;

        // Autoload avatar / wearable from parameters
        //var parameters = URLParameters.ParseDefault();
        // var parameters = URLParameters.Parse("https://example.com/?profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0xbebb268219a67a80fe85fc6af9f0ad0ec0dca98c:0");
        // var parameters = URLParameters.Parse("https://example.com/?profile=0x3f574d05ec670fe2c92305480b175654ca512005&contract=0x0d2f515ba568042a6756561ae552090b0ae5c586&item=0");
        // var parameters = URLParameters.Parse("https://example.com/?contract=0x0d2f515ba568042a6756561ae552090b0ae5c586&item=0");
        
        // var parameters = URLParameters.Parse("https://example.com/?profile=0x3f574d05ec670fe2c92305480b175654ca512005&contract=0xee8ae4c668edd43b34b98934d6d2ff82e41e6488&token=1");
        var parameters = URLParameters.Parse("https://example.com/?profile=0x3f574d05ec670fe2c92305480b175654ca512005&contract=0xee8ae4c668edd43b34b98934d6d2ff82e41e6488&item=5");

        if (parameters == null)
        {
            Debug.LogWarning("No parameters found");
            return;
        }
        
        Debug.Log("Loading!");

        mainCamera.backgroundColor = parameters.Background;

        _ = LoadFromParameters(parameters);
    }

    private async Awaitable LoadFromParameters(URLParameters parameters)
    {
        // If we have an URN we load directly
        if (parameters.Urn != null)
        {
            // We have the urn, can load directly
            await LoadAvatar(parameters.Profile, parameters.Urn);
            return;
        }

        // If we have a contract and item id or token id we need to fetch the urn first
        if (parameters.Contract != null && (parameters.ItemID != null || parameters.TokenID != null))
        {
            var urn = parameters.ItemID != null
                ? (await APIService.GetMarketplaceItemFromID(parameters.Contract, parameters.ItemID)).data[0].urn
                : (await APIService.GetMarketplaceItemFromToken(parameters.Contract, parameters.TokenID)).data[0].nft
                .urn;

            // We have the contract and item id, can load directly
            await LoadAvatar(parameters.Profile, urn);
        }
    }

    private Awaitable LoadAvatar(string profileID, string wearableID)
    {

        return previewLoader.LoadPreview(profileID, wearableID);
        
        // // Clear previous avatar
        // avatarRoot.Clear();
        //
        // await AvatarLoader.LoadAvatar(profileID, wearableID);
        //
        // previewRotator.Restart();
        //
        // // Animation
        // var animators = avatarRoot.GetComponentsInChildren<Animator>();
        // foreach (var animator in animators)
        // {
        //     animator.runtimeAnimatorController = animatorController;
        // }
    }
}