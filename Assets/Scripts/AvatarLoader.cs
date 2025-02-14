using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;

public static class AvatarLoader
{
    public static async Awaitable LoadAvatar(string userID, string overrideWearableID)
    {
        Debug.Log($"Loading user: {userID}");

        var avatar = await APIService.GetAvatar(userID);
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

            // TODO: Support facial features and stuff
            if (!wd.MainFile.EndsWith(".glb"))
            {
                Debug.LogError("Could not load wearable: " + wd.MainFile);
                continue;
            }

            var go = await GLBLoader.LoadWearable(wd.Category, wd.MainFile, wd.Files, avatarColors);
            go.AddComponent<Animator>();

            if (category == WearablesConstants.Categories.BODY_SHAPE) bodyGO = go;
        }

        // Hide stuff on body shape
        AvatarHideHelper.HideBodyShape(bodyGO, allHides, wearableDefinitions.Keys.ToHashSet());

        Debug.Log("Loaded all wearables!");
    }
}