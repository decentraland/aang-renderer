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
    [SerializeField] private Camera mainCamera;
    [SerializeField] private UIPresenter uiPresenter;
    [SerializeField] private Transform avatarRoot;
    [SerializeField] private Transform wearableRoot;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameObject platform;
    [SerializeField] private PreviewRotator previewRotator;

    private readonly Dictionary<string, GameObject> _wearables = new();
    private readonly Dictionary<string, (Texture2D main, Texture2D mask)> _facialFeatures = new();

    private bool _showingAvatar;
    private string _overrideWearableCategory;
    private AnimationClip _emoteAnimation;
    private AudioClip _emoteAudio;

    public async Awaitable LoadPreview(PreviewConfiguration config)
    {
        switch (config.Mode)
        {
            case PreviewConfiguration.PreviewMode.Marketplace:
                await LoadForMarketplace(config.Profile, await GetUrn(config), config.Emote);
                break;
            case PreviewConfiguration.PreviewMode.Authentication:
                await LoadForProfile(config.Profile, config.Emote, true);
                break;
            case PreviewConfiguration.PreviewMode.Profile:
                await LoadForProfile(config.Profile, config.Emote, false);
                break;
            case PreviewConfiguration.PreviewMode.Builder:
                await LoadForBuilder(config.BodyShape, config.EyeColor, config.HairColor, config.SkinColor, config.Hair,
                    config.FacialHair, config.UpperBody, config.LowerBody, config.Emote, config.Base64);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Awaitable LoadForMarketplace(string profileID, string urn, string defaultEmote)
    {
        Assert.IsNotNull(profileID);
        Assert.IsNotNull(urn);
        Assert.IsNotNull(defaultEmote);

        var avatar = await APIService.GetAvatar(profileID);

        await LoadStuff(avatar.bodyShape, avatar.wearables.ToList(), urn, avatar.eyes.color, avatar.hair.color,
            avatar.skin.color, defaultEmote, null);
        
        // This probably shouldn't be here but it's fiiiine
        if (_overrideWearableCategory == "emote")
        {
            previewRotator.EnableAutoRotate = false;
            previewRotator.ResetRotation();
        }
    }

    private async Awaitable LoadForProfile(string profileID, string defaultEmote, bool showPlatform)
    {
        Assert.IsNotNull(profileID);

        var avatar = await APIService.GetAvatar(profileID);

        await LoadStuff(avatar.bodyShape, avatar.wearables.ToList(), null, avatar.eyes.color, avatar.hair.color,
            avatar.skin.color, defaultEmote, null);

        platform.SetActive(showPlatform);
    }

    private async Awaitable LoadForBuilder(string bodyShape, Color? eyeColor, Color? hairColor, Color? skinColor,
        [CanBeNull] string hair, [CanBeNull] string facialHair, string upperBody, string lowerBody, string defaultEmote,
        [CanBeNull] byte[] base64)
    {
        Assert.IsNotNull(bodyShape);
        Assert.IsTrue(eyeColor.HasValue);
        Assert.IsTrue(hairColor.HasValue);
        Assert.IsTrue(skinColor.HasValue);
        // Assert.IsNotNull(hair);
        // Assert.IsNotNull(facialHair);
        Assert.IsNotNull(upperBody);
        Assert.IsNotNull(lowerBody);

        var urns = new List<string>
        {
            upperBody,
            lowerBody
        };
        if (hair != null) urns.Add(hair);
        if (facialHair != null) urns.Add(facialHair);

        await LoadStuff(bodyShape, urns, null, eyeColor.Value, hairColor.Value, skinColor.Value, defaultEmote, base64);
    }

    private async Awaitable LoadStuff(string bodyShape, List<string> urns, string overrideURN, Color eyeColor,
        Color hairColor, Color skinColor,
        string defaultEmote, byte[] base64)
    {
        Cleanup();

        var avatarColors = new AvatarColors(eyeColor, hairColor, skinColor);

        urns.Insert(0, bodyShape);
        if (overrideURN != null) urns.Add(overrideURN);

        var activeEntities = (await APIService.GetActiveEntities(urns.ToArray())).ToList();

        if (base64 != null)
        {
            var base64String = Encoding.UTF8.GetString(base64);
            var base64ActiveEntity = JsonUtility.FromJson<ActiveEntity>(base64String);

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

        // Adjust platform position
        platform.transform.localPosition = avatarRoot.transform.localPosition;

        // Switch to avatar view if there's no wearable override
        uiPresenter.EnableSwitcher(hasWearableOverride);
        if (!hasWearableOverride) ShowAvatar(true);

        // Audio event 
        audioSource.clip = _emoteAudio;
        //audioSource.Play();

        // Force play animations
        var eventAdded = false;
        foreach (var (_, go) in _wearables)
        {
            var anim = go.GetComponent<Animation>();

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

        gameObject.SetActive(true);
        uiPresenter.EnableLoader(false);

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
            eyebrows.material.SetTexture("_MainTex", eyebrowsTex.main);
            eyebrows.material.SetTexture("_MaskTex", eyebrowsTex.mask);
        }

        if (_facialFeatures.TryGetValue(WearablesConstants.Categories.EYES, out var eyesTex) && eyes)
        {
            eyes.material.SetTexture("_MainTex", eyesTex.main);
            eyes.material.SetTexture("_MaskTex", eyesTex.mask);
        }

        if (_facialFeatures.TryGetValue(WearablesConstants.Categories.MOUTH, out var mouthTex) && mouth)
        {
            mouth.material.SetTexture("_MainTex", mouthTex.main);
            mouth.material.SetTexture("_MaskTex", mouthTex.mask);
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

        // Calculate the scaling factor to maintain screen size
        // var boundsSize = combinedBounds.size;
        // var maxDimension = Mathf.Max(boundsSize.x, boundsSize.y, boundsSize.z);
        //
        // var distance = Vector3.Distance(mainCamera.transform.position, root.position);
        // var fovFactor = 2.0f * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        //
        // var desiredWorldSize = targetScreenSize * distance * fovFactor;
        // var scaleFactor = desiredWorldSize / maxDimension;
        //
        // root.localScale = Vector3.one * scaleFactor;
    }


    private static async Awaitable<string> GetUrn(PreviewConfiguration config)
    {
        if (config.Urn != null) return config.Urn;

        // If we have a contract and item id or token id we need to fetch the urn first
        if (config.Contract != null && (config.ItemID != null || config.TokenID != null))
        {
            return config.ItemID != null
                ? (await APIService.GetMarketplaceItemFromID(config.Contract, config.ItemID)).data[0].urn
                : (await APIService.GetMarketplaceItemFromToken(config.Contract, config.TokenID)).data[0].nft
                .urn;
        }

        return null;
    }


    private void Cleanup()
    {
        platform.SetActive(false);
        gameObject.SetActive(false);
        uiPresenter.EnableLoader(true);

        foreach (Transform child in avatarRoot) Destroy(child.gameObject);
        foreach (Transform child in wearableRoot) Destroy(child.gameObject);
        _wearables.Clear();
    }
}