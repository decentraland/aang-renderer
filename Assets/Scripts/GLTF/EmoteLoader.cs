using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Logging;
using UnityEngine;

namespace GLTF
{
    public static class EmoteLoader
    {
        public static async Task<AnimationClip> LoadEmote(string mainFile, Dictionary<string, string> files)
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

                var clip = clips[0];
                return clip;
            }

            Debug.LogError($"Failed to load emote: {mainFile}");

            return null;
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
    }
}