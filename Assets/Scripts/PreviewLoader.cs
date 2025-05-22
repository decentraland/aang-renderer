using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using GLTF;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

public class PreviewLoader : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private UIPresenter uiPresenter;
    [SerializeField] private Transform avatarRoot;
    [SerializeField] private Transform wearableRoot;
    [SerializeField] private PreviewRotator previewRotator;
    // [SerializeField] private float targetScreenSize = 0.5f;

    private readonly Dictionary<string, GameObject> _wearables = new();
    private readonly Dictionary<string, (Texture2D main, Texture2D mask)> _facialFeatures = new();

    private bool _showingAvatar;
    private string _overrideCategory;
    private AnimationClip _emoteAnimation;

    /// <summary>
    /// Loads the player's avatar with an optional override wearable that will replace
    /// the original wearable in the same category.
    /// </summary>
    public async Awaitable LoadPreview(string profileID, string overrideID, string defaultEmote = "idle")
    {
        Debug.Log($"Loading profile: {profileID}");

        gameObject.SetActive(false);
        uiPresenter.EnableLoader(true);
        previewRotator.ResetRotation();
        Clear();

        var avatar = await APIService.GetAvatar(profileID);
        var avatarColors = new AvatarColors(avatar.eyes.color, avatar.hair.color, avatar.skin.color);
        var bodyShape = avatar.bodyShape;
        var hasOverride = !string.IsNullOrEmpty(overrideID);

        var entitiesToFetch = avatar.wearables.Prepend(bodyShape);
        if (hasOverride)
        {
            entitiesToFetch = entitiesToFetch.Append(overrideID);
        }

        var activeEntities = await APIService.GetActiveEntities(entitiesToFetch.ToArray());

        var overrideEntity = activeEntities.FirstOrDefault(ae => ae.pointers[0] == overrideID);
        if (overrideEntity != null)
        {
            if (overrideEntity.IsEmote)
            {
                var emoteDefinition = EmoteDefinition.FromActiveEntity(overrideEntity, bodyShape);
                _emoteAnimation = await EmoteLoader.LoadEmote(emoteDefinition.MainFile, emoteDefinition.Files);
                overrideEntity = null;
                hasOverride = false;
            }
            else
            {
                _overrideCategory = overrideEntity?.metadata.data.category;
            }
        }

        // Fallback to default emote
        if (_emoteAnimation == null)
        {
            _emoteAnimation = await EmoteLoader.LoadEmbeddedEmote(defaultEmote);
        }

        var wearableDefinitions = activeEntities
            .Where(ae => !ae.IsEmote) // TODO: Could be better
            .Select(ae => WearableDefinition.FromActiveEntity(ae, bodyShape))
            // Skip the original wearable and use the override
            .Where(wd => overrideEntity == null || wd.Category != overrideEntity.metadata.data.category ||
                         wd.Pointer == overrideID)
            .ToDictionary(wd => wd.Category);

        var hiddenCategories = AvatarHideHelper.HideWearables(wearableDefinitions);

        // Load all wearables and body shape
        await Task.WhenAll(wearableDefinitions
            .Select(cwd => LoadWearable(cwd.Key, cwd.Value, avatarColors))
            .ToList());

        // Create a copy of the overridden wearable just because that's easier to manage
        if (!string.IsNullOrEmpty(_overrideCategory))
        {
            var overrideGO = (await InstantiateAsync(_wearables[_overrideCategory], new InstantiateParameters()
            {
                parent = wearableRoot,
                worldSpace = false
            }))[0];
            Destroy(overrideGO.GetComponent<Animator>());
        }

        // Hide stuff on body shape
        var bodyGO = avatarRoot.Find(WearablesConstants.Categories.BODY_SHAPE)?.gameObject;
        AvatarHideHelper.HideBodyShape(bodyGO, hiddenCategories, wearableDefinitions);

        // Setup facial features
        SetupFacialFeatures(bodyGO);

        // Center the roots around the meshes
        CenterMeshes(avatarRoot);
        if (hasOverride) CenterMeshes(wearableRoot);

        // Switch to avatar view if there's no wearable override
        uiPresenter.EnableSwitcher(hasOverride);
        if (!hasOverride) ShowAvatar(true);

        // Force play animations
        foreach (var (_, go) in _wearables)
        {
            go.GetComponent<Animation>().Play("emote");
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
            animComponent.clip = _emoteAnimation;
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


    private void Clear()
    {
        foreach (Transform child in avatarRoot) Destroy(child.gameObject);
        foreach (Transform child in wearableRoot) Destroy(child.gameObject);
        _wearables.Clear();
    }
}