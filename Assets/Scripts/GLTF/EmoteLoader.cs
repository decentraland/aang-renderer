using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Data;
using DCL.GLTFast.Wrappers;
using GLTFast;
using GLTFast.Logging;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace GLTF
{
    public static class EmoteLoader
    {
        public static async Task<(AnimationClip anim, AudioClip audio, GameObject prop)> LoadEmote(EmoteDefinition emoteDefinition)
        {
            var importer = new GltfImport(
                materialGenerator: new DecentralandMaterialGenerator("DCL/Scene", true),
                downloadProvider: new BinaryDownloadProvider(emoteDefinition.Files),
                logger: new ConsoleLogger()
            );

            var importSettings = new ImportSettings
            {
                NodeNameMethod = NameImportMethod.OriginalUnique,
                AnisotropicFilterLevel = 0,
                AnimationMethod = AnimationMethod.Legacy,
                GenerateMipMaps = false,
            };

            var fileHash = emoteDefinition.Files[emoteDefinition.MainFile];

            Debug.Log($"Loading Emote: {emoteDefinition.MainFile} - {fileHash}");

            var success = await importer.Load(string.Format(APIService.API_CATALYST, fileHash), importSettings);

            AudioClip audioClip = null;

            // TODO: Clean this up cmon
            var audioFile = emoteDefinition.Files.FirstOrDefault(kvp =>
                kvp.Key.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
            if (audioFile.Key != null)
            {
                Debug.Log($"Loading audio clip: {audioFile.Key} - {audioFile.Value}");

                var audioType = AudioType.UNKNOWN;


                if (audioFile.Key.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    audioType = AudioType.MPEG;

                if (audioFile.Key.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    audioType = AudioType.WAV;

                if (audioFile.Key.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                    audioType = AudioType.OGGVORBIS;


                using var www = UnityWebRequestMultimedia.GetAudioClip(
                    string.Format(APIService.API_CATALYST, audioFile.Value), audioType);
                await www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.Log(www.error);
                }
                else
                {
                    audioClip = DownloadHandlerAudioClip.GetContent(www);
                }
            }

            if (success)
            {
                var clips = importer.GetAnimationClips();
                Debug.Log($"Loaded emote: {emoteDefinition.MainFile} with clips: {clips.Length}");

                // Note, some GLB's just don't have an animation that ends with _Avatar, because of course they bloody don't.
                // Even though conventions say they should: https://docs.decentraland.org/creator/emotes/props-and-sounds/#naming-conventions
                // Like this one: urn:decentraland:matic:collections-v2:0xb5e24ada4096b86ce3cf7af5119f19ed6089a80b:0
                var avatarClip = clips.Length == 1 ? clips[0] : clips.First(c => c.name.EndsWith("_Avatar"));
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

                    return (avatarClip, audioClip, parent);
                }

                return (avatarClip, audioClip, null);
            }
            
            throw new NotSupportedException($"Failed to load emote: {emoteDefinition.MainFile}");
        }

        public static async Task<(AnimationClip anim, AudioClip audio, GameObject prop)> LoadEmbeddedEmote(string emote)
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
                return (clips[0], null, null);
            }
            
            throw new NotSupportedException($"Failed to load emote: {emote}");
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