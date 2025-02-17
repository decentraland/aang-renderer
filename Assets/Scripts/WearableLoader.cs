using System.Collections.Generic;
using Data;
using GLTFast;
using GLTFast.Logging;
using UnityEngine;

public static class WearableLoader
{
    public static async Awaitable<GameObject> LoadGLB(string category, string mainFile,
        Dictionary<string, string> files, AvatarColors avatarColors)
    { 
        var importer = new GltfImport(
            downloadProvider: new BinaryDownloadProvider(files),
            materialGenerator: new AvatarMaterialGenerator(avatarColors),
            logger: new ConsoleLogger()
        );

        var importSettings = new ImportSettings
        {
            NodeNameMethod = NameImportMethod.OriginalUnique,
            AnisotropicFilterLevel = 0,
            GenerateMipMaps = false,
        };

        Debug.Log("Loading GLB...");
            
        var success = await importer.Load(string.Format(APIService.API_CATALYST, files[mainFile]), importSettings);

        if (success)
        {
            var root = new GameObject(category);

            await importer.InstantiateMainSceneAsync(root.transform);

            return root;
        }

        Debug.LogError("Failed to load GLB");
        return null;
    }
}