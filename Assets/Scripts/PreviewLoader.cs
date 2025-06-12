using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data;
using GLTF;
using JetBrains.Annotations;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;
using Debug = UnityEngine.Debug;

public class PreviewLoader : MonoBehaviour
{
    private static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
    private static readonly int MASK_TEX = Shader.PropertyToID("_MaskTex");

    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform avatarRoot;
    [SerializeField] private Transform wearableRoot;
    [SerializeField] private AudioSource audioSource;

    private readonly Dictionary<string, GameObject> _wearables = new();
    private readonly Dictionary<string, (Texture2D main, Texture2D mask)> _facialFeatures = new();

    private bool _showingAvatar;
    private string _overrideWearableCategory;
    private AnimationClip _emoteAnimation;
    private AudioClip _emoteAudio;

    public bool HasEmoteOverride => _overrideWearableCategory == "emote";
    public bool HasEmoteAudio => _emoteAudio != null;
    public bool HasWearableOverride => _overrideWearableCategory != null && !HasEmoteOverride;

    public async Awaitable LoadPreview(PreviewConfiguration config)
    {
        gameObject.SetActive(false);
        Cleanup();

        switch (config.Mode)
        {
            case PreviewMode.Marketplace:
                var urns = await GetUrns(config);
                Assert.IsTrue(urns.Count == 1, $"Marketplace mode only allows one urn, found: {urns.Count}");
                await LoadForMarketplace(config.Profile, urns[0], config.Emote);
                break;
            case PreviewMode.Authentication:
            case PreviewMode.Profile:
                await LoadForProfile(config.Profile, config.Emote);
                break;
            case PreviewMode.Builder:
                await LoadForBuilder(config.BodyShape, config.EyeColor, config.HairColor, config.SkinColor,
                    await GetUrns(config), config.Emote, config.Base64);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        gameObject.SetActive(true);
    }

    private async Awaitable LoadForMarketplace(string profileID, string urn, string defaultEmote)
    {
        Assert.IsNotNull(profileID);
        Assert.IsNotNull(urn);
        Assert.IsNotNull(defaultEmote);

        var avatar = await APIService.GetAvatar(profileID);

        await LoadStuff(avatar.bodyShape, avatar.wearables.ToList(), urn, avatar.eyes.color, avatar.hair.color,
            avatar.skin.color, defaultEmote, null);
    }

    private async Awaitable LoadForProfile(string profileID, string defaultEmote)
    {
        Assert.IsNotNull(profileID);

        var avatar = await APIService.GetAvatar(profileID);

        await LoadStuff(avatar.bodyShape, avatar.wearables.ToList(), null, avatar.eyes.color, avatar.hair.color,
            avatar.skin.color, defaultEmote, null);
    }

    private async Awaitable LoadForBuilder(string bodyShape, Color? eyeColor, Color? hairColor, Color? skinColor,
        List<string> urns, string defaultEmote, [CanBeNull] byte[] base64)
    {
        Assert.IsNotNull(bodyShape);
        Assert.IsTrue(eyeColor.HasValue);
        Assert.IsTrue(hairColor.HasValue);
        Assert.IsTrue(skinColor.HasValue);

        await LoadStuff(bodyShape, urns, null, eyeColor.Value, hairColor.Value, skinColor.Value, defaultEmote, base64);
    }

    private async Awaitable LoadStuff(string bodyShape, List<string> urns, string overrideURN, Color eyeColor,
        Color hairColor, Color skinColor,
        string defaultEmote, byte[] base64)
    {
        var avatarColors = new AvatarColors(eyeColor, hairColor, skinColor);

        urns.Insert(0, bodyShape);
        if (overrideURN != null) urns.Add(overrideURN);

        var activeEntities = (await APIService.GetActiveEntities(urns.ToArray())).ToList();

        if (base64 != null)
        {
            var base64String = Encoding.UTF8.GetString(base64);
            var base64ActiveEntity = JsonUtility.FromJson<Base64ActiveEntity>(base64String).ToActiveEntity();

            if (base64ActiveEntity.IsEmote)
            {
                activeEntities.RemoveAll(ae => ae.IsEmote);
            }
            else
            {
                activeEntities.RemoveAll(ae => ae.metadata.data.category == base64ActiveEntity.metadata.data.category);
            }

            activeEntities.Add(base64ActiveEntity);
        }

        // Load emote first
        var emoteDefinition = activeEntities.Where(ae => ae.IsEmote)
            .Select(ae => EmoteDefinition.FromActiveEntity(ae, bodyShape))
            .FirstOrDefault();
        var emote = emoteDefinition != null
            ? await EmoteLoader.LoadEmote(emoteDefinition)
            : await EmoteLoader.LoadEmbeddedEmote(defaultEmote);

        _emoteAnimation = emote.anim;
        _emoteAudio = emote.audio;
        if (emote.prop)
        {
            emote.prop.transform.SetParent(avatarRoot, false);
            _wearables["emote"] = emote.prop;
        }

        if (overrideURN != null)
        {
            var overrideEntity = activeEntities.First(ae => ae.pointers.Contains(overrideURN));
            _overrideWearableCategory = overrideEntity.IsEmote ? "emote" : overrideEntity.metadata.data.category;
        }

        var hasWearableOverride = _overrideWearableCategory != null && _overrideWearableCategory != "emote";

        var wearableDefinitions = activeEntities
            .Where(ae => !ae.IsEmote)
            .Select(ae => WearableDefinition.FromActiveEntity(ae, bodyShape))
            // Skip the original wearable and use the override if we have one
            .Where(wd => _overrideWearableCategory == null || wd.Category != _overrideWearableCategory ||
                         wd.Pointer == overrideURN)
            .ToDictionary(wd => wd.Category);

        var hiddenCategories = AvatarHideHelper.HideWearables(wearableDefinitions);

        // Load all wearables and body shape
        await Task.WhenAll(wearableDefinitions
            .Select(cwd => LoadWearable(cwd.Key, cwd.Value, avatarColors))
            .ToList());

        // Create a copy of the overridden wearable just because that's easier to manage
        if (hasWearableOverride)
        {
            await InstantiateAsync(_wearables[_overrideWearableCategory], new InstantiateParameters()
            {
                parent = wearableRoot,
                worldSpace = false
            });
        }

        // Hide stuff on body shape
        var bodyGO = avatarRoot.Find(WearablesConstants.Categories.BODY_SHAPE)?.gameObject;
        AvatarHideHelper.HideBodyShape(bodyGO, hiddenCategories, wearableDefinitions);

        // Setup facial features
        SetupFacialFeatures(bodyGO);

        // Center the roots around the meshes
        // CenterMeshes(avatarRoot);
        if (hasWearableOverride) CenterMeshes(wearableRoot);

        // Switch to avatar view if there's no wearable override
        if (!hasWearableOverride) ShowAvatar(true);

        // Audio event 
        audioSource.clip = _emoteAudio;

        // Force play animations
        var eventAdded = false;

        foreach (var (_, go) in _wearables)
        {
            var anim = go.GetComponent<Animation>();

            // Auto play emote audio when animation starts
            if (_emoteAudio != null && !eventAdded)
            {
                eventAdded = true;
                anim.GetClip("emote").AddEvent(new AnimationEvent
                {
                    time = 0,
                    functionName = "Play"
                });
                var aer = go.AddComponent<AudioEventReceiver>();
                aer.AudioSource = audioSource;
            }

            anim.Play("emote");
        }

        Debug.Log("Loaded all wearables!");
    }

    private void SetupFacialFeatures(GameObject bodyGO)
    {
        if (!bodyGO) return;

        // TODO: Shouldn't be here

        var meshRenderers = bodyGO.GetComponentsInChildren<SkinnedMeshRenderer>(false);

        var eyebrows = meshRenderers.FirstOrDefault(mr => mr.name.EndsWith("Eyebrows"));
        var eyes = meshRenderers.FirstOrDefault(mr => mr.name.EndsWith("Eyes"));
        var mouth = meshRenderers.FirstOrDefault(mr => mr.name.EndsWith("Mouth"));

        if (_facialFeatures.TryGetValue(WearablesConstants.Categories.EYEBROWS, out var eyebrowsTex) && eyebrows)
        {
            eyebrows.material.SetTexture(MAIN_TEX, eyebrowsTex.main);
            eyebrows.material.SetTexture(MASK_TEX, eyebrowsTex.mask);
        }

        if (_facialFeatures.TryGetValue(WearablesConstants.Categories.EYES, out var eyesTex) && eyes)
        {
            eyes.material.SetTexture(MAIN_TEX, eyesTex.main);
            eyes.material.SetTexture(MASK_TEX, eyesTex.mask);
        }

        if (_facialFeatures.TryGetValue(WearablesConstants.Categories.MOUTH, out var mouthTex) && mouth)
        {
            mouth.material.SetTexture(MAIN_TEX, mouthTex.main);
            mouth.material.SetTexture(MASK_TEX, mouthTex.mask);
        }
    }

    /// <summary>
    /// Toggles between showing the avatar or just the wearable.
    /// </summary>
    public void ShowAvatar(bool show)
    {
        if (_showingAvatar == show) return;
        _showingAvatar = show;

        avatarRoot.gameObject.SetActive(show);
        wearableRoot.gameObject.SetActive(!show);
    }

    /// <summary>
    /// Starts or stops animation playback.
    /// </summary>
    public void PlayAnimation(bool play)
    {
        foreach (var (_, go) in _wearables)
        {
            var anim = go.GetComponent<Animation>();

            if (play)
            {
                anim.Play("emote");
            }
            else
            {
                anim.Stop();
            }
        }
    }

    private async Task LoadWearable(string category, WearableDefinition wd, AvatarColors avatarColors)
    {
        Debug.Log($"Loading wearable({category}): {wd.Pointer}");

        if (WearablesConstants.FACIAL_FEATURES.Contains(category))
        {
            // This is a facial feature, only comes as a texture
            // TODO: Assert.IsTrue(wd.MainFile.EndsWith(".glb"), "Only GLB files are supported");
            var tex = await WearableLoader.LoadFacialFeature(wd.MainFile, wd.Files);
            if (tex != null)
            {
                _facialFeatures[category] = tex.Value;
            }
            else
            {
                // TODO: Error handling when null?
            }
        }
        else
        {
            Assert.IsTrue(wd.MainFile.EndsWith(".glb"), "Only GLB files are supported");

            // Normal GLB
            var go = await WearableLoader.LoadGLB(wd.Category, wd.MainFile, wd.Files, avatarColors);

            go.transform.SetParent(avatarRoot, false);
            _wearables[category] = go;

            var animComponent = go.AddComponent<Animation>();
            animComponent.playAutomatically = false;
            animComponent.AddClip(_emoteAnimation, "emote");
        }
    }

    private void CenterMeshes(Transform root)
    {
        root.position = Vector3.zero;

        var renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            Debug.LogError("No MeshRenderers found in the child.");
            return;
        }

        // Calculate the combined bounds of all MeshRenderers
        var combinedBounds = renderers[0].bounds;
        foreach (var meshRenderer in renderers)
        {
            combinedBounds.Encapsulate(meshRenderer.bounds);
        }

        // Compute the center offset relative to the child
        var centerOffset = combinedBounds.center - root.position;

        // Apply the offset to the child transform
        root.position -= centerOffset;
    }


    private static async Awaitable<List<string>> GetUrns(PreviewConfiguration config)
    {
        if (config.Urns.Count > 0) return config.Urns;

        // If we have a contract and item id or token id we need to fetch the urn first
        if (config.Contract != null && (config.ItemID != null || config.TokenID != null))
        {
            return new List<string>
            {
                config.ItemID != null
                    ? (await APIService.GetMarketplaceItemFromID(config.Contract, config.ItemID)).data[0].urn
                    : (await APIService.GetMarketplaceItemFromToken(config.Contract, config.TokenID)).data[0].nft
                    .urn
            };
        }

        return null;
    }

    private void Cleanup()
    {
        foreach (Transform child in avatarRoot) Destroy(child.gameObject);
        foreach (Transform child in wearableRoot) Destroy(child.gameObject);
        _wearables.Clear();
    }
}