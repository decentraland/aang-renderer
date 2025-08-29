using System;
using System.Linq;
using System.Threading.Tasks;
using Data;
using DCL.GLTFast.Wrappers;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Loading
{
    public static class GLTFLoader
    {
        public static async Task<LoadedModel> LoadModel(BodyShape bodyShape, EntityDefinition entityDefinition,
            Transform parent)
        {
            var representation = entityDefinition[bodyShape];

            var importer = new GltfImport(
                downloadProvider: new BinaryDownloadProvider(representation.Files),
                materialGenerator: new ToonMaterialGenerator()
            );

            var importSettings = new ImportSettings
            {
                NodeNameMethod = NameImportMethod.OriginalUnique,
                AnisotropicFilterLevel = 0,
                GenerateMipMaps = false,
                AnimationMethod = AnimationMethod.None,
            };

            var file = representation.Files[representation.MainFile];

            Debug.Log($"Loading GLB: {representation.MainFile} - {file}");

            var success = await importer.Load(file, importSettings);

            if (success)
            {
                var root = new GameObject(entityDefinition.Category);
                root.SetActive(false);
                root.transform.SetParent(parent, false);

                await importer.InstantiateSceneAsync(root.transform);

                Sanitize(root.transform);

                Debug.Log($"GLB loaded: {representation.MainFile}");

                return new LoadedModel(entityDefinition, root, importer);
            }

            throw new Exception($"Failed to load GLB: {representation.MainFile}");
        }

        public static async Task<LoadedFacialFeature> LoadFacialFeature(BodyShape bodyShape,
            EntityDefinition entityDefinition)
        {
            var rep = entityDefinition[bodyShape];

            var mainTexture = await LoadTexture(rep.Files[rep.MainFile]);
            if (!mainTexture) throw new Exception($"Failed to load texture {rep.Files[rep.MainFile]}");

            var maskTexture = rep.Files.Count == 2
                ? await LoadTexture(rep.Files[rep.Files.Keys.First(x => x != rep.MainFile)])
                : null;

            return new LoadedFacialFeature(entityDefinition, mainTexture, maskTexture);

            async Awaitable<Texture2D> LoadTexture(string file)
            {
                using var webRequest = UnityWebRequestTexture.GetTexture(file, true);

                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to load texture {file}: {webRequest.error}");
                    return null;
                }

                return DownloadHandlerTexture.GetContent(webRequest);
            }
        }

        public static async Awaitable<LoadedEmote> LoadEmote(BodyShape bodyShape, EntityDefinition entityDefinition, Transform propParent)
        {
            var rep = entityDefinition[bodyShape];

            var importer = new GltfImport(
                materialGenerator: new DecentralandMaterialGenerator("DCL/Scene", true),
                downloadProvider: new BinaryDownloadProvider(rep.Files)
            );

            var importSettings = new ImportSettings
            {
                NodeNameMethod = NameImportMethod.OriginalUnique,
                AnisotropicFilterLevel = 0,
                AnimationMethod = AnimationMethod.Legacy,
                GenerateMipMaps = false,
            };

            var file = rep.Files[rep.MainFile];

            Debug.Log($"Loading Emote: {rep.MainFile} - {file}");

            var success = await importer.Load(file, importSettings);

            AudioClip audioClip = null;

            // TODO: Clean this up cmon
            var audioFile = rep.Files.FirstOrDefault(kvp =>
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


                using var www = UnityWebRequestMultimedia.GetAudioClip(audioFile.Value, audioType);
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
                var looping = (entityDefinition.Flags & EntityFlags.Looping) != 0;
                Debug.Log($"Loaded emote: {rep.MainFile} with clips: {clips.Length}");

                // Note, some GLB's just don't have an animation that ends with _Avatar, because of course they bloody don't.
                // Even though conventions say they should: https://docs.decentraland.org/creator/emotes/props-and-sounds/#naming-conventions
                // Like this one: urn:decentraland:matic:collections-v2:0xb5e24ada4096b86ce3cf7af5119f19ed6089a80b:0
                var avatarClip = clips.Length == 1 ? clips[0] : clips.First(c => c.name.EndsWith("_Avatar"));
                var propClip = clips.Length == 1
                    ? null
                    : clips.FirstOrDefault(c => c.name.EndsWith("_Prop", StringComparison.InvariantCultureIgnoreCase)) ??
                      clips.FirstOrDefault(c => !c.name.EndsWith("_Avatar"));
                GameObject prop = null;
                Animation propAnim = null;
                
                avatarClip.wrapMode = looping ? WrapMode.Loop : WrapMode.Clamp;

                if (propClip != null)
                {
                    // Avatar animation controls prop clip so we don't auto loop it
                    propClip.wrapMode = WrapMode.Clamp;

                    // We have a prop we need to deal with
                    Debug.Log("Lading emote prop");
                    prop = new GameObject("emote");
                    prop.SetActive(false);
                    prop.transform.SetParent(propParent, false);
                    propAnim = prop.AddComponent<Animation>();
                    propClip.name = entityDefinition.URN;
                    propAnim.AddClip(propClip, entityDefinition.URN);
                    propAnim.clip = propClip;

                    await importer.InstantiateMainSceneAsync(prop.transform);

                    Sanitize(prop.transform);
                }

                return new LoadedEmote(entityDefinition, avatarClip, audioClip, prop, propAnim, importer);
            }

            throw new NotSupportedException($"Failed to load emote: {rep.MainFile}");
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

                while (sceneChild.childCount > 0)
                {
                    var child = sceneChild.GetChild(0);
                    child.SetParent(root, true);
                }

                Object.Destroy(sceneChild.gameObject);
            }

            // var skinnedRenderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
            // var armatureRoot = skinnedRenderer.rootBone.parent;
            // armatureRoot.name = "Armature"; // Force Armature name since legacy animation needs it
            //
            // // Fix for wearables with incorrect hierarchy. Why do we even have conventions?
            // // Offender: urn:decentraland:matic:collections-v2:0xa6a59f7a7b1401670ea09dc5554b55757163e20d:0
            // if (armatureRoot.parent != root)
            // {
            //     var armatureParent = armatureRoot.parent;
            //
            //     while (armatureParent.childCount > 0)
            //     {
            //         var child = armatureParent.GetChild(0);
            //         child.SetParent(root, true);
            //     }
            //
            //     Object.Destroy(armatureParent.gameObject);
            // }
            //
            // // Some emotes like urn:decentraland:matic:collections-v2:0x705652b66a12dcf782b0b3d5673fbf0c1797eba2:3
            // // move the armature??? And not all emotes reset it in animation. Dance does, idle does not.
            // armatureRoot.localPosition = Vector3.zero;
            //
            // foreach (Transform t in root)
            // {
            //     // Some wearables have weird scales so we normalize them
            //     t.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            //
            //     // If there are objects named Armature that aren't the actual armature, rename them
            //     if (t.name == "Armature" && t != armatureRoot)
            //     {
            //         t.name = "Armature_ThatShouldNotBeHere";
            //     }
            // }
        }
    }
}