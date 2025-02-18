using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;
using UnityEngine.Assertions;

public class PreviewLoader : MonoBehaviour
{
    [SerializeField] private PreviewRotator rotator;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private UIPresenter uiPresenter;
    
    private readonly Dictionary<string, GameObject> _categories = new();

    private bool _showAvatar = true;
    private string _overrideCategory;
    
    public async Awaitable LoadPreview(string profileID, string overrideWearableID)
    {
        gameObject.SetActive(false);
        uiPresenter.EnableLoader(true);
        
        Clear();
        
        Debug.Log($"Loading profile: {profileID}");

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
                    wearableDefinitions.Remove(toHide);
                    allHides.Add(toHide);
                }

                foreach (var toReplace in wearableDefinition.Replaces)
                {
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
        GameObject bodyGO = null;
        foreach (var (category, wd) in wearableDefinitions)
        {
            Debug.Log($"Loading wearable({category}): {wd.Pointer}");


            if (WearablesConstants.FACIAL_FEATURES.Contains(category))
            {
                // This is a facial feature, only comes as a texture
                Debug.LogError("Facial feature loading not supported.");
                continue;
            }
            else
            {
                Assert.IsTrue(wd.MainFile.EndsWith(".glb"), "Only GLB files are supported");

                // Normal GLB
                var go = await WearableLoader.LoadGLB(wd.Category, wd.MainFile, wd.Files, avatarColors);
                
                go.transform.SetParent(transform, false);
                _categories[category] = go;
                
                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;

                if (category == WearablesConstants.Categories.BODY_SHAPE) bodyGO = go;
            }
        }

        // Hide stuff on body shape
        AvatarHideHelper.HideBodyShape(bodyGO, allHides, wearableDefinitions.Keys.ToHashSet());
        
        // Restart rotator so it re-calculates the bounds
        rotator.RecalculateBounds();

        gameObject.SetActive(true);
        uiPresenter.EnableLoader(false);

        Debug.Log("Loaded all wearables!");
    }

    public void ShowAvatar(bool show)
    {
        if(_showAvatar == show) return;
        
        _showAvatar = show;
        
        foreach (var (category, go) in _categories)
        {
            go.SetActive(_showAvatar || category == _overrideCategory);
        }
        
        // We don't want to animate just the wearable
        EnableAnimation(show);
        
        rotator.RecalculateBounds();
    }
    
    private void EnableAnimation(bool enable)
    {
        var animators = GetComponentsInChildren<Animator>(true);
        foreach (var animator in animators)
        {
            animator.enabled = enable;

            // Restart animator
            if (enable)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }
    }

    private void Clear()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        
        _categories.Clear();
    }
}