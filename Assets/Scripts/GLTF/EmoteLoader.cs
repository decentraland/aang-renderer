using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GLTF
{
    public static class EmoteLoader
    {
        public static async Task<(AnimationClip anim, GameObject prop)> LoadEmote(string mainFile,
            Dictionary<string, string> files)
        {
            var importer = new GltfImport(
                downloadProvider: new BinaryDownloadProvider(files),
                logger: new ConsoleLogger()
            );

            var importSettings = new ImportSettings
            {
                NodeNameMethod = NameImportMethod.OriginalUnique,
                AnisotropicFilterLevel = 0,
                AnimationMethod = AnimationMethod.Legacy,
                GenerateMipMaps = false,
            };

            var fileHash = files[mainFile];

            Debug.Log($"Loading Emote: {mainFile} - {fileHash}");

            var success = await importer.Load(string.Format(APIService.API_CATALYST, fileHash), importSettings);

            if (success)
            {
                var clips = importer.GetAnimationClips();
                Debug.Log($"Loaded emote: {mainFile} with clips: {clips.Length}");

                var avatarClip = clips.First(c => c.name.EndsWith("_Avatar"));
                var propClip = clips.FirstOrDefault(c =>
                    c.name.EndsWith("_Prop", StringComparison.InvariantCultureIgnoreCase));

                if (propClip != null)
                {
                    // We have a prop we need to deal with
                    Debug.Log("Lading emote prop");
                    var parent = new GameObject("emote");
                    await importer.InstantiateMainSceneAsync(parent.transform);

                    Sanitize(parent.transform);

                    var animComponent = parent.AddComponent<Animation>();
                    animComponent.AddClip(propClip, "emote");
                    animComponent.playAutomatically = false;

                    return (avatarClip, parent);
                }

                return (avatarClip, null);
            }

            Debug.LogError($"Failed to load emote: {mainFile}");

            throw new NotSupportedException("Failed to load emote");
        }

        public static async Task<AnimationClip> LoadEmbeddedEmote(string emote)
        {
            var filePath = Path.Combine(Application.streamingAssetsPath, $"{emote}.glb");

            var importer = new GltfImport(
                logger: new ConsoleLogger()
            );

            var success = await importer.Load(filePath);

            if (success)
            {
                var clips = importer.GetAnimationClips();
                Debug.Log($"Loaded emote: {emote} with clips: {clips.Length}");
                return clips[0];
            }

            Debug.LogError($"Failed to load emote: {emote}");

            return null;
        }

        private static void Sanitize(Transform root)
        {
            // TODO: Duplicated code with wearable loader

            // If the wearable has 2 root objects they get placed under a Scene object, and we don't want that
            if (root.childCount == 1 && root.GetChild(0).name == "Scene")
            {
                var sceneChild = root.GetChild(0);

                while (sceneChild.childCount > 0)
                {
                    var child = sceneChild.GetChild(0);
                    child.SetParent(root, true);
                }

                Object.Destroy(sceneChild.gameObject);
            }
        }
    }
}