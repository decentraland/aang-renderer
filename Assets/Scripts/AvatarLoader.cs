using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;
using UnityEngine.Assertions;

public static class AvatarLoader
{
    public static async Awaitable LoadAvatar(string profileID, string overrideWearableID)
    {
        Debug.Log($"Loading user: {profileID}");

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
                CommonAssets.AvatarRoot.Attach(category, go);
                go.AddComponent<Animator>();

                if (category == WearablesConstants.Categories.BODY_SHAPE) bodyGO = go;
            }
        }

        // Hide stuff on body shape
        AvatarHideHelper.HideBodyShape(bodyGO, allHides, wearableDefinitions.Keys.ToHashSet());

        Debug.Log("Loaded all wearables!");
    }
}