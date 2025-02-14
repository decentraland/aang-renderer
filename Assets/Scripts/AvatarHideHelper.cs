using System;
using System.Collections.Generic;
using UnityEngine;

public static class AvatarHideHelper
{
    public static void HideBodyShape(GameObject bodyShape, HashSet<string> hidingList, HashSet<string> usedCategories)
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
                    renderer.gameObject.SetActive(!(hidingList.Contains(value) || usedCategories.Contains(value)));
                    isPartMapped = true;
                    break;
                }
            }

            if (!isPartMapped)
                Debug.LogWarning($"{name} has not been set-up as a valid body part");
        }
    }

}