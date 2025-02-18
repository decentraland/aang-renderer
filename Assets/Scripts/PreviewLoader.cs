using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

public class PreviewLoader : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PreviewRotator rotator;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private UIPresenter uiPresenter;
    [SerializeField] private Transform avatarRoot;
    [SerializeField] private Transform wearableRoot;

    private readonly Dictionary<string, GameObject> _categories = new();

    private bool _showingAvatar;
    private string _overrideCategory;

    /// <summary>
    /// Loads the player's avatar with an optional override wearable that will replace
    /// the original wearable in the same category.
    /// </summary>
    public async Awaitable LoadPreview(string profileID, string overrideWearableID)
    {
        Debug.Log($"Loading profile: {profileID}");

        gameObject.SetActive(false);
        uiPresenter.EnableLoader(true);
        Clear();

        var avatar = await APIService.GetAvatar(profileID);
        var avatarColors = new AvatarColors(avatar.eyes.color, avatar.hair.color, avatar.skin.color);
        var bodyShape = avatar.bodyShape;

        // TODO: Too much array copying
        var entitiesToFetch = avatar.wearables.Prepend(bodyShape).ToArray();
        if (!string.IsNullOrEmpty(overrideWearableID))
        {
            entitiesToFetch = entitiesToFetch.Append(overrideWearableID).ToArray();
        }

        var activeEntities = await APIService.GetActiveEntities(entitiesToFetch);

        var overrideEntity = activeEntities.FirstOrDefault(ae => ae.pointers[0] == overrideWearableID);
        _overrideCategory = overrideEntity?.metadata.data.category;
        var wearableDefinitions = activeEntities
            .Select(ae => WearableDefinition.FromActiveEntity(ae, bodyShape))
            // Skip the original wearable and use the override
            .Where(wd => overrideEntity == null || wd.Category != overrideEntity.metadata.data.category ||
                         wd.Pointer == overrideWearableID)
            .ToDictionary(wd => wd.Category);
        var allHides = new HashSet<string>();

        // Figure out what wearables to keep
        foreach (var category in WearablesConstants.CATEGORIES_PRIORITY)
        {
            if (wearableDefinitions.TryGetValue(category, out var wearableDefinition))
            {
                // Apparently there's no difference between hides and replaces
                foreach (var toHide in wearableDefinition.Hides)
                {
                    if (toHide == category) continue; // Safeguard so wearables don't hide themselves

                    wearableDefinitions.Remove(toHide);
                    allHides.Add(toHide);
                }

                foreach (var toReplace in wearableDefinition.Replaces)
                {
                    if (toReplace == category) continue; // Safeguard so wearables don't hide themselves

                    wearableDefinitions.Remove(toReplace);
                    allHides.Add(toReplace);
                }

                // Skin has implicit hides
                if (category == WearablesConstants.Categories.SKIN)
                {
                    foreach (var skinCategory in WearablesConstants.SKIN_IMPLICIT_CATEGORIES)
                    {
                        wearableDefinitions.Remove(skinCategory);
                        allHides.Add(skinCategory);
                    }
                }
            }
        }

        // Load all wearables and body shape
        await Task.WhenAll(wearableDefinitions
            .Select(cwd => LoadWearable(cwd.Key, cwd.Value, avatarColors))
            .ToList());

        // Create a copy of the overridden wearable just because that's easier to manage
        if (!string.IsNullOrEmpty(_overrideCategory))
        {
            var overrideGO = (await InstantiateAsync(_categories[_overrideCategory], new InstantiateParameters()
            {
                parent = wearableRoot,
                worldSpace = false
            }))[0];
            Destroy(overrideGO.GetComponent<Animator>());
        }

        // Hide stuff on body shape
        var bodyGO = avatarRoot.Find(WearablesConstants.Categories.BODY_SHAPE)?.gameObject;
        AvatarHideHelper.HideBodyShape(bodyGO, allHides, wearableDefinitions.Keys.ToHashSet());

        // Center the roots around the meshes
        CenterMeshes(avatarRoot);
        CenterMeshes(wearableRoot);

        // Restart rotator so it re-calculates the bounds
        rotator.RecalculateBounds();

        gameObject.SetActive(true);
        uiPresenter.EnableLoader(false);

        Debug.Log("Loaded all wearables!");
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

        rotator.RecalculateBounds();
    }

    private async Task LoadWearable(string category, WearableDefinition wd, AvatarColors avatarColors)
    {
        Debug.Log($"Loading wearable({category}): {wd.Pointer}");

        if (WearablesConstants.FACIAL_FEATURES.Contains(category))
        {
            // This is a facial feature, only comes as a texture
            Debug.LogError("Facial feature loading not supported.");
        }
        else
        {
            Assert.IsTrue(wd.MainFile.EndsWith(".glb"), "Only GLB files are supported");

            // Normal GLB
            var go = await WearableLoader.LoadGLB(wd.Category, wd.MainFile, wd.Files, avatarColors);

            go.transform.SetParent(avatarRoot, false);
            _categories[category] = go;

            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
        }
    }

    private void CenterMeshes(Transform root)
    {
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
        _categories.Clear();
    }
}