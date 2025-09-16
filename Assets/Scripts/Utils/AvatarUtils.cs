using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using JetBrains.Annotations;
using Loading;
using UnityEngine;

namespace Utils
{
    public static class AvatarUtils
    {
        /// <summary>
        /// Processes the wearables that are currently equipped and hides the ones that should be hidden.
        /// </summary>
        /// <param name="bodyShape"></param>
        /// <param name="wearables">Currently equipped wearables</param>
        /// <param name="overrideCategory">The category that has been overridden and should never be hidden</param>
        /// <param name="forceRender">Which parts we shouldn't hide</param>
        /// <returns>A set of all the categories that were hidden.</returns>
        public static HashSet<string> HideWearables(BodyShape bodyShape, List<EntityDefinition> wearables,
            string overrideCategory, [CanBeNull] string[] forceRender)
        {
            var hiddenCategories = new HashSet<string>();

            foreach (var category in WearableCategories.CATEGORIES_PRIORITY)
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
                        wearables.RemoveAll(ed => ed.Category == WearableCategories.Categories.HANDS);
                        hiddenCategories.Add(WearableCategories.Categories.HANDS);
                    }

                    // Skin has implicit hides
                    if (category == WearableCategories.Categories.SKIN)
                    {
                        if (overrideCategory is null or WearableCategories.Categories.SKIN)
                        {
                            foreach (var skinCategory in WearableCategories.SKIN_IMPLICIT_CATEGORIES)
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
            var isOrHidesUpperBody = category == WearableCategories.Categories.UPPER_BODY ||
                                     rep.Hides.Contains(WearableCategories.Categories.UPPER_BODY);

            // The rule is ignored if the wearable contains the removal of this default rule (newer upper bodies since the release of hands)
            var removesHandDefault = rep.RemovesDefaultHiding.Contains(WearableCategories.Categories.HANDS);

            // Why do we do this? Because old upper bodies contain the base hand mesh, and they might clip with the new handwear items
            return isOrHidesUpperBody && !removesHandDefault;
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
                        renderer.gameObject.SetActive(!(hiddenCategories.Contains(value) ||
                                                        loadedCategories.Contains(value)));
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

        public static void SetupColors(GameObject go, AvatarColors colors,
            List<Renderer> outlineRenderers, Transform avatarRootBone = null, Transform[] avatarBones = null)
        {
            var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var r in renderers)
            {
                if (r.material.name.Contains("skin", StringComparison.OrdinalIgnoreCase))
                {
                    r.material.SetColor(WearablesConstants.Shaders.BASE_COLOR_ID, colors.Skin);
                }
                else if (r.material.name.Contains("hair", StringComparison.OrdinalIgnoreCase))
                {
                    r.material.SetColor(WearablesConstants.Shaders.BASE_COLOR_ID, colors.Hair);
                }

                if (avatarRootBone != null && avatarBones != null)
                {
                    r.rootBone = avatarRootBone;
                    r.bones = avatarBones;
                }

                if (r.material.shader.name == "DCL/DCL_Toon" && r.sharedMaterial.renderQueue is >= 2000 and < 3000)
                {
                    outlineRenderers.Add(r);
                }
            }
        }

        public static void SetupFacialFeatures(GameObject go, AvatarColors colors,
            Dictionary<string, LoadedFacialFeature> loadedFacialFeatures,
            Dictionary<string, (Texture2D main, Texture2D mask)> defaultBodyFacialFeatures)
        {
            // Setup facial features
            foreach (var cat in WearableCategories.FACIAL_FEATURES)
            {
                var ffRenderer = GetFacialFeatureRenderer(cat, go);

                var color = GetFacialFeatureColor(cat, colors);
                ffRenderer.material.SetColor(WearablesConstants.Shaders.BASE_COLOR_ID, color);

                // Save the default ones so we can revert
                if (!defaultBodyFacialFeatures.ContainsKey(cat))
                {
                    defaultBodyFacialFeatures[cat] = (
                        (Texture2D)ffRenderer.material.GetTexture(WearablesConstants.Shaders.MAIN_TEX_ID),
                        (Texture2D)ffRenderer.material.GetTexture(WearablesConstants.Shaders.MASK_TEX_ID));
                }

                var loadedFeature = loadedFacialFeatures.Values.FirstOrDefault(ff => ff.Entity.Category == cat);

                var main = loadedFeature.Entity != null ? loadedFeature.Main : defaultBodyFacialFeatures[cat].main;
                var mask = loadedFeature.Entity != null ? loadedFeature.Mask : defaultBodyFacialFeatures[cat].mask;

                // The default mask for eyes is all white
                if (cat == WearableCategories.Categories.EYES && mask == null)
                {
                    mask = Texture2D.whiteTexture;
                }

                ffRenderer.material.SetTexture(WearablesConstants.Shaders.MAIN_TEX_ID, main);
                ffRenderer.material.SetTexture(WearablesConstants.Shaders.MASK_TEX_ID, mask);
            }
        }

        private static SkinnedMeshRenderer GetFacialFeatureRenderer(string category, GameObject bodyGO)
        {
            var suffix = category switch
            {
                WearableCategories.Categories.EYEBROWS => "Mask_Eyebrows",
                WearableCategories.Categories.EYES => "Mask_Eyes",
                WearableCategories.Categories.MOUTH => "Mask_Mouth",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };

            var meshRenderers = bodyGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            return meshRenderers.FirstOrDefault(mr => mr.name.EndsWith(suffix));
        }

        private static Color GetFacialFeatureColor(string category, AvatarColors colors)
        {
            return category switch
            {
                WearableCategories.Categories.EYEBROWS => colors.Hair,
                WearableCategories.Categories.EYES => colors.Eyes,
                WearableCategories.Categories.MOUTH => colors.Skin,
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }
    }
}