using System;
using System.Collections.Generic;
using Data;
using UnityEngine;

public static class AvatarHideHelper
{
    /// <summary>
    /// Processes the wearables that are currently equipped and hides the ones that should be hidden.
    /// </summary>
    /// <param name="wearables">Currently equipped wearables</param>
    /// <param name="overrideCategory">The category that has been overridden and should never be hidden</param>
    /// <returns>A set of all the categories that were hidden.</returns>
    public static HashSet<string> HideWearables(Dictionary<string, WearableDefinition> wearables,
        string overrideCategory = null)
    {
        var hiddenCategories = new HashSet<string>();

        foreach (var category in WearablesConstants.CATEGORIES_PRIORITY)
        {
            if (wearables.TryGetValue(category, out var wearableDefinition))
            {
                // Apparently there's no difference between hides and replaces
                foreach (var toHide in wearableDefinition.Hides)
                {
                    if (toHide == category) continue; // Safeguard so wearables don't hide themselves

                    // If something is trying to hide the wearable we're overriding, hide that category instead
                    if (toHide == overrideCategory)
                    {
                        wearables.Remove(category);
                        hiddenCategories.Add(category);
                    }
                    else
                    {
                        wearables.Remove(toHide);
                        hiddenCategories.Add(toHide);
                    }
                }

                foreach (var toReplace in wearableDefinition.Replaces)
                {
                    if (toReplace == category) continue; // Safeguard so wearables don't hide themselves

                    // If something is trying to hide the wearable we're overriding, hide that category instead
                    if (toReplace == overrideCategory)
                    {
                        wearables.Remove(category);
                        hiddenCategories.Add(category);
                    }
                    else
                    {
                        wearables.Remove(toReplace);
                        hiddenCategories.Add(toReplace);
                    }
                }

                // Skin has implicit hides
                if (category == WearablesConstants.Categories.SKIN)
                {
                    foreach (var skinCategory in WearablesConstants.SKIN_IMPLICIT_CATEGORIES)
                    {
                        wearables.Remove(skinCategory);
                        hiddenCategories.Add(skinCategory);
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