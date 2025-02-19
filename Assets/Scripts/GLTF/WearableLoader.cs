using System.Collections.Generic;
using Data;
using GLTFast;
using GLTFast.Logging;
using UnityEngine;

namespace GLTF
{
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

            var fileHash = files[mainFile];

            Debug.Log($"Loading GLB: {mainFile} - {fileHash}");

            var success = await importer.Load(string.Format(APIService.API_CATALYST, fileHash), importSettings);

            if (success)
            {
                var root = new GameObject(category);

                await importer.InstantiateSceneAsync(root.transform);

                Sanitize(root.transform);

                Debug.Log($"GLB loaded: {mainFile}");

                return root;
            }

            Debug.LogError($"Failed to load GLB: {mainFile}");
            return null;
        }

        /// <summary>
        /// Examples of urns that need this:
        ///
        /// urn:decentraland:matic:collections-v2:0xee8ae4c668edd43b34b98934d6d2ff82e41e6488:0
        /// urn:decentraland:matic:collections-v2:0xee8ae4c668edd43b34b98934d6d2ff82e41e6488:5
        /// </summary>
        private static void Sanitize(Transform root)
        {
            // If the wearable has 2 root objects they get placed under a Scene object, and we don't want that
            if (root.childCount == 1 && root.GetChild(0).name == "Scene")
            {
                var sceneChild = root.GetChild(0);
                foreach (Transform t in sceneChild)
                {
                    t.SetParent(root, true);
                }

                Object.Destroy(sceneChild.gameObject);
            }

            foreach (Transform t in root)
            {
                // Some wearables have weird scales so we normalize them
                t.localScale = new Vector3(0.01f, 0.01f, 0.01f);

                // Anything other than Armature will break the animations TODO: Find a better way to handle this
                if (t.name.StartsWith("Armature"))
                {
                    t.name = "Armature";
                }
            }
        }
    }
}