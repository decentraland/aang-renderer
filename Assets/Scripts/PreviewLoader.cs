using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data;
using DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline;
using GLTF;
using JetBrains.Annotations;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;
using Debug = UnityEngine.Debug;

public class PreviewLoader : MonoBehaviour
{
    private const bool DEBUG_ONLY_LOAD_WEARABLE = false;

    private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
    private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");
    private static readonly int MASK_TEX_ID = Shader.PropertyToID("_MaskTex");

    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform avatarRoot;
    [SerializeField] private Transform wearableRoot;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float wearablePadding = 0.1f;

    private readonly Dictionary<string, GameObject> _wearables = new();
    private readonly Dictionary<string, (Texture2D main, Texture2D mask)> _facialFeatures = new();
    private readonly List<Renderer> _outlineRenderers = new();

    private bool _showingAvatar;
    private string _overrideWearableCategory;
    private AnimationClip _emoteAnimation;
    private AudioClip _emoteAudio;

    private Vector3 _defaultAvatarPosition;

    public bool HasEmoteOverride => _overrideWearableCategory == "emote";
    public bool HasEmoteAudio => _emoteAudio != null;
    public bool HasWearableOverride => _overrideWearableCategory != null && !HasEmoteOverride;
    public bool HasValidRepresentation { get; private set; }
    public bool IsAvatarMale { get; private set; }

    private void Awake()
    {
        _defaultAvatarPosition = avatarRoot.localPosition;
    }

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
            avatar.skin.color, avatar.forceRender, defaultEmote, null);
    }

    private async Awaitable LoadForProfile(string profileID, string defaultEmote)
    {
        Assert.IsNotNull(profileID);

        var avatar = await APIService.GetAvatar(profileID);

        await LoadStuff(avatar.bodyShape, avatar.wearables.ToList(), null, avatar.eyes.color, avatar.hair.color,
            avatar.skin.color, avatar.forceRender, defaultEmote, null);
    }

    private async Awaitable LoadForBuilder(string bodyShape, Color? eyeColor, Color? hairColor, Color? skinColor,
        List<string> urns, string defaultEmote, [CanBeNull] List<byte[]> base64)
    {
        Assert.IsNotNull(bodyShape);
        Assert.IsTrue(eyeColor.HasValue);
        Assert.IsTrue(hairColor.HasValue);
        Assert.IsTrue(skinColor.HasValue);

        await LoadStuff(bodyShape, urns, null, eyeColor.Value, hairColor.Value, skinColor.Value, null, defaultEmote,
            base64);
    }

    private async Awaitable LoadStuff(string bodyShape, List<string> urns, string overrideURN, Color eyeColor,
        Color hairColor, Color skinColor, [CanBeNull] string[] forceRender, string defaultEmote,
        [CanBeNull] List<byte[]> base64)
    {
        IsAvatarMale = bodyShape == "urn:decentraland:off-chain:base-avatars:BaseMale";

        var avatarColors = new AvatarColors(eyeColor, hairColor, skinColor);

        urns.Insert(0, bodyShape);
        if (overrideURN != null) urns.Add(overrideURN);

        var activeEntities = (await APIService.GetActiveEntities(urns.ToArray())).ToList();

        // Verify we received all urns
        foreach (var urn in urns.Where(urn =>
                     activeEntities.All(ae => !urn.StartsWith(ae.pointers[0], StringComparison.OrdinalIgnoreCase))))
        {
            Debug.LogError($"URN {urn} not found");
        }

        if (base64 != null)
        {
            foreach (var b64 in base64)
            {
                var base64String = Encoding.UTF8.GetString(b64);
                var base64ActiveEntity = JsonUtility.FromJson<Base64ActiveEntity>(base64String).ToActiveEntity();

                if (base64ActiveEntity.IsEmote)
                {
                    activeEntities.RemoveAll(ae => ae.IsEmote);
                }
                else
                {
                    activeEntities.RemoveAll(ae =>
                        ae.metadata.data.category == base64ActiveEntity.metadata.data.category);
                }

                activeEntities.Add(base64ActiveEntity);
            }
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

        HasValidRepresentation =
            hasWearableOverride && wearableDefinitions.All(wd => wd.Value.HasValidRepresentation);

        var bodyShapeDefinition = wearableDefinitions["body_shape"]; // In case we need it for a facial feature
        var hiddenCategories =
            AvatarHideHelper.HideWearables(wearableDefinitions, _overrideWearableCategory, overrideURN, forceRender);

        // Load all wearables and body shape
        await Task.WhenAll(wearableDefinitions
            .Where(cwd => !DEBUG_ONLY_LOAD_WEARABLE || cwd.Key == _overrideWearableCategory)
            .Select(cwd => LoadWearable(cwd.Key, cwd.Value, avatarColors))
            .ToList());

        // Create a copy of the overridden wearable just because that's easier to manage
        if (hasWearableOverride)
        {
            if (_wearables.TryGetValue(_overrideWearableCategory, out var wearable))
            {
                await InstantiateAsync(wearable, new InstantiateParameters
                {
                    parent = wearableRoot,
                    worldSpace = false
                });
            }
            else
            {
                // It's a facial feature

                // Load the body TODO: We don't need to load the body again, could reuse existing one
                var bodyShapeGO = await WearableLoader.LoadGLB(bodyShapeDefinition.Category,
                    bodyShapeDefinition.MainFile, bodyShapeDefinition.Files, avatarColors);

                // Hide everything except the head
                AvatarHideHelper.HideBodyShape(bodyShapeGO, new HashSet<string>
                {
                    WearablesConstants.Categories.UPPER_BODY,
                    WearablesConstants.Categories.LOWER_BODY,
                    WearablesConstants.Categories.HANDS,
                    WearablesConstants.Categories.FEET
                }, new Dictionary<string, WearableDefinition>());

                AvatarHideHelper.HideBodyShapeFacialFeatures(bodyShapeGO,
                    _overrideWearableCategory != WearablesConstants.Categories.EYES,
                    _overrideWearableCategory != WearablesConstants.Categories.EYEBROWS,
                    _overrideWearableCategory != WearablesConstants.Categories.MOUTH
                );

                SetupFacialFeatures(bodyShapeGO, avatarColors);

                bodyShapeGO.transform.SetParent(wearableRoot, false);
                bodyShapeGO.transform.localRotation = Quaternion.Euler(-15, 0, 0); // Tilt the head back
            }
        }

        // Hide stuff on body shape
        var bodyGO = avatarRoot.Find(WearablesConstants.Categories.BODY_SHAPE)?.gameObject;
        AvatarHideHelper.HideBodyShape(bodyGO, hiddenCategories, wearableDefinitions);

        // Setup facial features
        SetupFacialFeatures(bodyGO, avatarColors);

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

    public void Recenter()
    {
        // Center the roots around the meshes
        if (HasWearableOverride) CenterAndFit(wearableRoot);
        if (HasEmoteOverride) CenterAndFit(avatarRoot);
    }

    private void Update()
    {
        RendererFeature_AvatarOutline.m_AvatarOutlineRenderers.AddRange(_outlineRenderers);
    }

    private void SetupFacialFeatures(GameObject bodyGO, AvatarColors avatarColors)
    {
        if (!bodyGO) return;

        var meshRenderers = bodyGO.GetComponentsInChildren<SkinnedMeshRenderer>(false);

        var eyebrows = meshRenderers.FirstOrDefault(mr => mr.name.EndsWith("Mask_Eyebrows"));
        var eyes = meshRenderers.FirstOrDefault(mr => mr.name.EndsWith("Mask_Eyes"));
        var mouth = meshRenderers.FirstOrDefault(mr => mr.name.EndsWith("Mask_Mouth"));

        if (_facialFeatures.TryGetValue(WearablesConstants.Categories.EYEBROWS, out var eyebrowsTex) && eyebrows)
        {
            eyebrows.material.SetTexture(MAIN_TEX_ID, eyebrowsTex.main);
            eyebrows.material.SetTexture(MASK_TEX_ID, eyebrowsTex.mask);
            eyebrows.material.SetColor(BASE_COLOR_ID, avatarColors.Hair);
        }

        if (_facialFeatures.TryGetValue(WearablesConstants.Categories.EYES, out var eyesTex) && eyes)
        {
            eyes.material.SetTexture(MAIN_TEX_ID, eyesTex.main);
            eyes.material.SetTexture(MASK_TEX_ID, eyesTex.mask);
            eyes.material.SetColor(BASE_COLOR_ID, avatarColors.Eyes);
        }

        if (_facialFeatures.TryGetValue(WearablesConstants.Categories.MOUTH, out var mouthTex) && mouth)
        {
            mouth.material.SetTexture(MAIN_TEX_ID, mouthTex.main);
            mouth.material.SetTexture(MASK_TEX_ID, mouthTex.mask);
            mouth.material.SetColor(BASE_COLOR_ID, avatarColors.Skin);
        }
    }

    /// <summary>
    /// Toggles between showing the avatar or just the wearable.
    /// </summary>
    public void ShowAvatar(bool show)
    {
        avatarRoot.gameObject.SetActive(show);
        wearableRoot.gameObject.SetActive(!show);

        _outlineRenderers.Clear();

        var allRenderers = gameObject.GetComponentsInChildren<Renderer>();
        foreach (var r in allRenderers)
        {
            if (r.material.shader.name == "DCL/DCL_Toon" && r.sharedMaterial.renderQueue is >= 2000 and < 3000)
            {
                _outlineRenderers.Add(r);
            }
        }
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
            Assert.IsTrue(wd.MainFile.EndsWith(".glb") || wd.MainFile.EndsWith(".gltf"),
                "Only GLB files are supported");

            // Normal GLB
            var go = await WearableLoader.LoadGLB(wd.Category, wd.MainFile, wd.Files, avatarColors);

            go.transform.SetParent(avatarRoot, false);
            _wearables[category] = go;

            var animComponent = go.AddComponent<Animation>();
            animComponent.playAutomatically = false;
            animComponent.AddClip(_emoteAnimation, "emote");
        }
    }

    private void CenterAndFit(Transform root)
    {
        // Gather combined bounds of all Renderers under root
        var renders = root.GetComponentsInChildren<Renderer>();
        if (renders.Length == 0) return;
        var combined = renders[0].bounds;
        for (var i = 1; i < renders.Length; i++)
            combined.Encapsulate(renders[i].bounds);

        // Make it a cube
        var maxSize = Mathf.Max(combined.size.x, Mathf.Max(combined.size.y, combined.size.z));
        combined = new Bounds(combined.center, Vector3.one * maxSize);

        // Get local center of bounds and move them parent position (0, 0, 0 unless something changes)
        var localCenter = root.InverseTransformPoint(combined.center);
        combined.center = root.parent.position;

        // Desired object size in world units with padding
        var size = combined.size; // * (1f + wearablePadding);

        float scaleFactor;
        if (mainCamera.orthographic)
        {
            // World-window dimensions for orthographic camera
            var orthoHeight = mainCamera.orthographicSize * 2f;
            var orthoWidth = orthoHeight * mainCamera.aspect;
            var orthoMin = Mathf.Min(orthoWidth, orthoHeight);
            scaleFactor = orthoMin / size.x;
        }
        else
        {
            // Distance from camera to object after centering
            var distance = Vector3.Distance(mainCamera.transform.position, combined.center);

            // Camera frustum size at that distance
            var frustumHeight = 2f * distance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var frustumWidth = frustumHeight * mainCamera.aspect;
            var frustumMin = Mathf.Min(frustumWidth, frustumHeight);
            scaleFactor = frustumMin * (1f - wearablePadding * 2f) / size.x;
        }

        // Apply uniform scaling and adjust position on root
        root.localScale *= scaleFactor;
        root.localPosition = Vector3.Scale(-localCenter, root.localScale);
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

    public void Cleanup()
    {
        _outlineRenderers.Clear();
        RendererFeature_AvatarOutline.m_AvatarOutlineRenderers.Clear();

        _overrideWearableCategory = null;
        _emoteAnimation = null;
        _emoteAudio = null;

        audioSource.Stop();
        audioSource.clip = null;

        foreach (Transform child in avatarRoot) Destroy(child.gameObject);
        foreach (Transform child in wearableRoot) Destroy(child.gameObject);
        _wearables.Clear();

        avatarRoot.gameObject.SetActive(true);
        wearableRoot.gameObject.SetActive(true);

        avatarRoot.localPosition = _defaultAvatarPosition;
        avatarRoot.localScale = Vector3.one;
        wearableRoot.localPosition = Vector3.zero;
        wearableRoot.localScale = Vector3.one;
    }
}