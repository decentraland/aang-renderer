using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using JetBrains.Annotations;
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
        string overrideCategory, string overrideURN, [CanBeNull] string[] forceRender)
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

                    // If wearable is forced to be rendered, never remove it
                    if (forceRender != null && forceRender.Contains(toHide)) continue;

                    wearables.Remove(toHide);
                    hiddenCategories.Add(toHide);
                }

                // Deal with hands
                // if (ShouldHideHands(wearableDefinition))
                // {
                //     wearables.Remove(WearablesConstants.Categories.HANDS);
                //     hiddenCategories.Add(WearablesConstants.Categories.HANDS);
                // }

                // Skin has implicit hides
                if (category == WearablesConstants.Categories.SKIN)
                {
                    if (overrideCategory is null or WearablesConstants.Categories.SKIN)
                    {
                        foreach (var skinCategory in WearablesConstants.SKIN_IMPLICIT_CATEGORIES)
                        {
                            // If wearable is forced to be rendered, never remove it
                            if (forceRender != null && forceRender.Contains(skinCategory)) continue;

                            wearables.Remove(skinCategory);
                            hiddenCategories.Add(skinCategory);
                        }
                    }
                }
            }
        }

        return hiddenCategories;
    }


    public static HashSet<string> HideWearables(BodyShape bodyShape, List<EntityDefinition> wearables,
        string overrideCategory, string overrideURN, [CanBeNull] string[] forceRender)
    {
        var hiddenCategories = new HashSet<string>();

        // Remove any wearables that hide the override category
        // if (overrideCategory != null)
        // {
        //     var toRemove = wearables
        //         .Where(w => w.Value.Hides.Contains(overrideCategory) || w.Value.Replaces.Contains(overrideCategory) ||
        //                     (w.Key == WearablesConstants.Categories.SKIN &&
        //                      WearablesConstants.SKIN_IMPLICIT_CATEGORIES.Contains(overrideCategory)))
        //         .Where(kvp => kvp.Value.Pointer != overrideURN) // Skip the override urn if it contains its own category
        //         .Select(kvp => kvp.Key)
        //         .ToArray();
        //
        //     foreach (var category in toRemove)
        //     {
        //         wearables.Remove(category);
        //     }
        // }

        foreach (var category in WearablesConstants.CATEGORIES_PRIORITY)
        {
            var rep = wearables.FirstOrDefault(w => w.Category == category)?[bodyShape];

            if (rep != null)
            {
                // Apparently there's no difference between hides and replaces
                foreach (var categoryToHide in rep.Hides)
                {
                    if (categoryToHide == category) continue; // Safeguard so wearables don't hide themselves

                    // If wearable is forced to be rendered, never remove it
                    if (forceRender != null && forceRender.Contains(categoryToHide)) continue;

                    wearables.RemoveAll(ed => ed.Category == categoryToHide);
                    hiddenCategories.Add(categoryToHide);
                }

                // Deal with hands
                if (ShouldHideHands(category, rep))
                {
                    wearables.RemoveAll(ed => ed.Category == WearablesConstants.Categories.HANDS);
                    hiddenCategories.Add(WearablesConstants.Categories.HANDS);
                }

                // Skin has implicit hides
                if (category == WearablesConstants.Categories.SKIN)
                {
                    if (overrideCategory is null or WearablesConstants.Categories.SKIN)
                    {
                        foreach (var skinCategory in WearablesConstants.SKIN_IMPLICIT_CATEGORIES)
                        {
                            // If wearable is forced to be rendered, never remove it
                            if (forceRender != null && forceRender.Contains(skinCategory)) continue;

                            wearables.RemoveAll(ed => ed.Category == skinCategory);
                            hiddenCategories.Add(skinCategory);
                        }
                    }
                }
            }
        }


        return hiddenCategories;
    }


    private static bool ShouldHideHands(string category, EntityDefinition.Representation rep)
    {
        // We apply this rule to hide the hands by default if the wearable is an upper body or hides the upper body
        var isOrHidesUpperBody = category == WearablesConstants.Categories.UPPER_BODY ||
                                 rep.Hides.Contains(WearablesConstants.Categories.UPPER_BODY);

        // The rule is ignored if the wearable contains the removal of this default rule (newer upper bodies since the release of hands)
        var removesHandDefault = rep.RemovesDefaultHiding.Contains(WearablesConstants.Categories.HANDS);

        // Why do we do this? Because old upper bodies contain the base hand mesh, and they might clip with the new handwear items
        return isOrHidesUpperBody && !removesHandDefault;
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

    /// <summary>
    /// Hides parts of the body shape based on which categories are shown and hidden.
    /// </summary>
    /// <param name="bodyShape">The root GO of the body shape</param>
    /// <param name="hiddenCategories">The categories that are being hidden</param>
    /// <param name="loadedCategories">The wearables that are used (equipped)</param>
    public static void HideBodyShape(GameObject bodyShape, HashSet<string> hiddenCategories,
        HashSet<string> loadedCategories)
    {
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
                    renderer.gameObject.SetActive(!(hiddenCategories.Contains(value) || loadedCategories.Contains(value)));
                    isPartMapped = true;
                    break;
                }
            }

            if (!isPartMapped)
                Debug.LogWarning($"{name} has not been set-up as a valid body part");
        }
    }

    /// <summary>
    /// Hides facial features on the body shape
    /// </summary>
    public static void HideBodyShapeFacialFeatures(GameObject bodyShape, bool hideEyes, bool hideEyebrows,
        bool hideMouth)
    {
        var renderers = bodyShape.GetComponentsInChildren<Renderer>(true);

        foreach (var renderer in renderers)
        {
            var name = renderer.name;

            // Support for the old gltf hierarchy for ABs
            if (name.Contains("primitive", StringComparison.OrdinalIgnoreCase))
                name = renderer.transform.parent.name;

            if (hideEyes && name.Contains("eyes", StringComparison.OrdinalIgnoreCase))
            {
                renderer.gameObject.SetActive(false);
            }

            if (hideEyebrows && name.Contains("eyebrows", StringComparison.OrdinalIgnoreCase))
            {
                renderer.gameObject.SetActive(false);
            }

            if (hideMouth && name.Contains("mouth", StringComparison.OrdinalIgnoreCase))
            {
                renderer.gameObject.SetActive(false);
            }
        }
    }
}