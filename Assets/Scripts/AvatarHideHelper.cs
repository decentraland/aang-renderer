using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;

public static class AvatarHideHelper
{
    /// <summary>
    /// Processes the wearables that are currently equipped and hides the ones that should be hidden.
    /// </summary>
    /// <param name="wearables">Currently equipped wearables</param>
    /// <param name="overrideCategory">The category that has been overridden and should never be hidden</param>
    /// <param name="overrideURN"></param>
    /// <returns>A set of all the categories that were hidden.</returns>
    public static HashSet<string> HideWearables(Dictionary<string, WearableDefinition> wearables,
        string overrideCategory, string overrideURN)
    {
        var hiddenCategories = new HashSet<string>();

        // Remove any wearables that hide the override category
        if (overrideCategory != null)
        {
            var toRemove = wearables
                .Where(w => w.Value.Hides.Contains(overrideCategory) || w.Value.Replaces.Contains(overrideCategory) ||
                            (w.Key == WearablesConstants.Categories.SKIN &&
                             WearablesConstants.SKIN_IMPLICIT_CATEGORIES.Contains(overrideCategory)))
                .Where(kvp => kvp.Value.Pointer != overrideURN) // Skip the override urn if it contains its own category
                .Select(kvp => kvp.Key)
                .ToArray();

            foreach (var category in toRemove)
            {
                wearables.Remove(category);
            }
        }

        foreach (var category in WearablesConstants.CATEGORIES_PRIORITY)
        {
            if (wearables.TryGetValue(category, out var wearableDefinition))
            {
                // Apparently there's no difference between hides and replaces
                foreach (var toHide in wearableDefinition.Hides.Union(wearableDefinition.Replaces))
                {
                    if (toHide == category) continue; // Safeguard so wearables don't hide themselves

                    wearables.Remove(toHide);
                    hiddenCategories.Add(toHide);
                }

                // Skin has implicit hides
                if (category == WearablesConstants.Categories.SKIN)
                {
                    if (overrideCategory is null or WearablesConstants.Categories.SKIN)
                    {
                        foreach (var skinCategory in WearablesConstants.SKIN_IMPLICIT_CATEGORIES)
                        {
                            wearables.Remove(skinCategory);
                            hiddenCategories.Add(skinCategory);
                        }
                    }
                }
            }
        }

        return hiddenCategories;
    }

    /// <summary>
    /// Hides parts of the body shape based on which categories are shown and hidden.
    /// </summary>
    /// <param name="bodyShape">The root GO of the body shape</param>
    /// <param name="hiddenCategories">The categories that are being hidden</param>
    /// <param name="wearables">The wearables that are used (equipped)</param>
    public static void HideBodyShape(GameObject bodyShape, HashSet<string> hiddenCategories,
        Dictionary<string, WearableDefinition> wearables)
    {
        // Means that the body shape was hidden
        if (bodyShape == null)
            return;

        var renderers = bodyShape.GetComponentsInChildren<Renderer>(true);

        foreach (var renderer in renderers)
        {
            var name = renderer.name;

            // Support for the old gltf hierarchy for ABs
            if (name.Contains("primitive", StringComparison.OrdinalIgnoreCase))
                name = renderer.transform.parent.name;

            var isPartMapped = false;

            foreach (var (key, value) in WearablesConstants.BODY_PARTS_MAPPING)
            {
                if (name.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    renderer.gameObject.SetActive(!(hiddenCategories.Contains(value) || wearables.ContainsKey(value)));
                    isPartMapped = true;
                    break;
                }
            }

            if (!isPartMapped)
                Debug.LogWarning($"{name} has not been set-up as a valid body part");
        }
    }
}