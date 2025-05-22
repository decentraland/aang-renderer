using System.Diagnostics;
using GLTF;
using GLTFast;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PreviewLoader previewLoader;
    [SerializeField] private Material baseMat;
    [SerializeField] private Material facialFeaturesMat;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private PreviewRotator previewRotator;

    // ReSharper disable once AsyncVoidMethod
    private async void Start()
    {
        // Common assets TODO: Improve maybe
        CommonAssets.AvatarMaterial = baseMat;
        CommonAssets.FacialFeaturesMaterial = facialFeaturesMat;

        // Sets uninterrupted defer agent for fastest loading
        GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent());

        // Let's make it a bit smoother
        Application.targetFrameRate = 60;

        // Autoload avatar / wearable from parameters
        //var parameters = URLParameters.ParseDefault() ?? URLParameters.Parse("https://example.com/?profile=default1");
        
        // Miha avatar
        // var parameters = URLParameters.Parse("https://example.com/?profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0xbebb268219a67a80fe85fc6af9f0ad0ec0dca98c:0");
        
        // Emote
        var parameters = URLParameters.Parse("https://example.com/?profile=0x3f574d05ec670fe2c92305480b175654ca512005&contract=0xb5e24ada4096b86ce3cf7af5119f19ed6089a80b&item=0");

        mainCamera.backgroundColor = parameters.Background;

        await LoadFromParameters(parameters);
    }

    private async Awaitable LoadFromParameters(URLParameters parameters)
    {
        var sw = Stopwatch.StartNew();
        
        Debug.Log("Loading from parameters");

        // If we have a contract and item id or token id we need to fetch the urn first
        if (parameters.Contract != null && (parameters.ItemID != null || parameters.TokenID != null))
        {
            var urn = parameters.ItemID != null
                ? (await APIService.GetMarketplaceItemFromID(parameters.Contract, parameters.ItemID)).data[0].urn
                : (await APIService.GetMarketplaceItemFromToken(parameters.Contract, parameters.TokenID)).data[0].nft
                .urn;

            // We have the contract and item id, can load directly
            await previewLoader.LoadPreview(parameters.Profile, urn, parameters.Emote);
        }
        else
        {
            // If we have an URN or nothing we load directly
            await previewLoader.LoadPreview(parameters.Profile, parameters.Urn, parameters.Emote);
        }

        sw.Stop();
        Debug.Log($"Loaded in {sw.ElapsedMilliseconds}ms");
    }
}