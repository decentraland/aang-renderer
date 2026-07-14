using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using GLTFast;
using Loading;
using Preview;
using Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Utils;
using Object = UnityEngine.Object;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Assembles an outfit onto the scene's existing avatar skeleton in EDIT MODE, so artists can
    /// see selections in 3D without entering play mode. Reuses the renderer's loading pipeline
    /// (EntityService, GLTFLoader, AvatarUtils, ToonMaterialGenerator) but manages its own object
    /// lifecycle with DestroyImmediate — AvatarLoader itself is play-mode-only.
    ///
    /// Preview objects are HideFlags.DontSave and are cleared automatically before entering play
    /// mode, so the runtime AvatarLoader always starts clean.
    ///
    /// Limitations (by design): static idle pose — no emotes, spring bones or outline. Play mode
    /// remains the ground truth for animation and capture.
    /// </summary>
    [InitializeOnLoad]
    public static class EditModeAvatarPreview
    {
        /// <summary>
        /// All preview objects live under this container. Tracking dictionaries are static and
        /// die on every domain reload while the objects survive in the scene — the container
        /// makes those orphans discoverable so applies never stack duplicates.
        /// </summary>
        private const string PREVIEW_CONTAINER_NAME = "__OutfitStudio_EditPreview";

        private static readonly Dictionary<string, LoadedModel> LOADED_MODELS = new();
        private static readonly Dictionary<string, LoadedFacialFeature> LOADED_FACIAL_FEATURES = new();
        private static readonly Dictionary<string, (Texture2D main, Texture2D mask)> DEFAULT_BODY_FACIAL_FEATURES = new();

        private static BodyShape? _loadedBodyShape;
        private static int _applySequence;
        private static bool _deferAgentSet;
        private static bool _framedOnce;

        // The Preview rig is saved inactive in the scene (Bootstrap activates it at play time).
        // We activate what we need for the edit-mode preview and restore it on Clear.
        private static readonly List<GameObject> ACTIVATED_OBJECTS = new();

        public static bool HasPreview => LOADED_MODELS.Count > 0;

        static EditModeAvatarPreview()
        {
            // The runtime loader must start from a clean rig
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Clear();
            };
            EditorSceneManager.sceneClosing += (_, _) => Clear();
        }

        /// <summary>
        /// Loads/diffs the outfit onto the preview rig. Safe to call repeatedly; overlapping calls
        /// are serialized via a sequence counter (stale ones abort).
        /// </summary>
        public static async void Apply(OutfitDefinition outfit, Action<string, bool> status)
        {
            if (Application.isPlaying) return;

            var sequence = ++_applySequence;

            try
            {
                // --- Locate the preview rig (scene must be open)
                var previewController = Object.FindFirstObjectByType<PreviewController>(FindObjectsInactive.Include);
                if (previewController == null)
                {
                    status("Open Assets/Scenes/Main.unity first", true);
                    return;
                }

                var pcSerialized = new SerializedObject(previewController);
                var avatarLoader = pcSerialized.FindProperty("avatarLoader").objectReferenceValue as AvatarLoader;
                if (avatarLoader == null)
                {
                    status("PreviewController has no AvatarLoader wired", true);
                    return;
                }

                // The rig lives under an inactive root in edit mode — activate it (restored on Clear)
                EnsureActiveInHierarchy(previewController.transform);
                EnsureActiveInHierarchy(avatarLoader.transform);

                var loaderSerialized = new SerializedObject(avatarLoader);
                var avatarRootBone = loaderSerialized.FindProperty("avatarRootBone").objectReferenceValue as Transform;
                var avatarAnimation = loaderSerialized.FindProperty("avatarAnimation").objectReferenceValue as Animation;
                var bonesProperty = loaderSerialized.FindProperty("avatarBones");
                var avatarBones = new Transform[bonesProperty.arraySize];
                for (var i = 0; i < avatarBones.Length; i++)
                    avatarBones[i] = bonesProperty.GetArrayElementAtIndex(i).objectReferenceValue as Transform;

                // The skeleton may live under its own inactive branch
                if (avatarRootBone != null) EnsureActiveInHierarchy(avatarRootBone);
                if (avatarAnimation != null) EnsureActiveInHierarchy(avatarAnimation.transform);

                EnsureEditModeSetup();

                status("Loading outfit (edit mode)...", false);

                // --- Resolve entities
                var bodyShape = outfit.bodyShape.Equals(WearablesConstants.BODY_SHAPE_FEMALE,
                    StringComparison.OrdinalIgnoreCase)
                    ? BodyShape.Female
                    : BodyShape.Male;

                await EntityService.PreloadBodyEntities();

                var requestedUrns = outfit.urns.Select(URNUtils.SanitizeURN).ToArray();
                var urnEntities = await EntityService.GetEntities(requestedUrns);

                if (sequence != _applySequence) return;

                // Entities the catalyst couldn't resolve (e.g. third-party/linked wearables)
                // are skipped with a warning instead of failing the whole preview
                var resolvedUrns = urnEntities.Select(e => e.URN).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var unresolved = requestedUrns.Where(urn => !resolvedUrns.Contains(urn)).ToList();
                if (unresolved.Count > 0)
                {
                    Debug.LogWarning($"[OutfitStudio] Could not resolve entities for: {string.Join(", ", unresolved)}");
                }

                // Slot dedup + body-shape validity (mirrors PreviewController.LoadForBuilder,
                // but skips invalid representations instead of letting the loader throw)
                var slots = new Dictionary<string, EntityDefinition>();
                foreach (var entity in urnEntities.Where(e => e.Type != EntityType.Emote))
                {
                    if (!entity.HasRepresentation(bodyShape))
                    {
                        status($"Skipped {entity.URN[(entity.URN.LastIndexOf(':') + 1)..]}: no {bodyShape} representation", true);
                        continue;
                    }

                    slots[entity.Category] = entity;
                }

                // Draft (builder) items — base64 wins per category, same as LoadForBuilder.
                // Draft emotes are play-mode-only (edit mode is a static pose) and skipped here.
                foreach (var base64 in outfit.base64Items)
                {
                    try
                    {
                        var entity = EntityDefinition.FromBase64(OutfitDefinition.DecodeBase64(base64));

                        if (entity.Type == EntityType.Emote) continue;

                        if (!entity.HasRepresentation(bodyShape))
                        {
                            status($"Skipped draft {entity.URN}: no {bodyShape} representation", true);
                            continue;
                        }

                        slots[entity.Category] = entity;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[OutfitStudio] Failed to parse draft item: {e.Message}");
                    }
                }

                var definitions = new List<EntityDefinition> { EntityService.GetBodyEntity(bodyShape) };
                definitions.AddRange(slots.Values);

                var hiddenCategories = AvatarUtils.HideWearables(bodyShape, definitions, Array.Empty<string>());

                // --- Diff current preview state
                if (_loadedBodyShape != null && _loadedBodyShape != bodyShape)
                {
                    // Body change invalidates everything (incl. default facial textures).
                    // Clear() bumps the sequence (aborting other in-flight applies) and restores
                    // the rig's inactive state — re-arm and re-activate for THIS apply.
                    Clear();
                    sequence = ++_applySequence;
                    EnsureActiveInHierarchy(previewController.transform);
                    EnsureActiveInHierarchy(avatarLoader.transform);
                    if (avatarRootBone != null) EnsureActiveInHierarchy(avatarRootBone);
                    if (avatarAnimation != null) EnsureActiveInHierarchy(avatarAnimation.transform);
                }

                var container = GetOrCreateContainer(avatarLoader.transform);

                // Sweep orphans from before a domain reload (dicts empty, objects survived)
                if (LOADED_MODELS.Count == 0 && container.childCount > 0)
                {
                    for (var i = container.childCount - 1; i >= 0; i--)
                    {
                        Object.DestroyImmediate(container.GetChild(i).gameObject);
                    }
                }

                foreach (var urn in LOADED_MODELS.Keys.ToList())
                {
                    if (definitions.All(d => d.URN != urn)) RemoveModel(urn);
                }

                foreach (var urn in LOADED_FACIAL_FEATURES.Keys.ToList())
                {
                    if (definitions.All(d => d.URN != urn)) RemoveFacialFeature(urn);
                }

                // --- Load missing pieces (sequential is fine for edit-mode preview)
                foreach (var definition in definitions)
                {
                    if (LOADED_MODELS.ContainsKey(definition.URN) ||
                        LOADED_FACIAL_FEATURES.ContainsKey(definition.URN)) continue;

                    switch (definition.Type)
                    {
                        case EntityType.Body or EntityType.Wearable:
                        {
                            var loaded = await GLTFLoader.LoadModel(bodyShape, definition, container);

                            if (sequence != _applySequence)
                            {
                                loaded.Disposable?.Dispose();
                                if (loaded.Root != null) Object.DestroyImmediate(loaded.Root);
                                return;
                            }

                            SetDontSaveRecursive(loaded.Root);
                            LOADED_MODELS[definition.URN] = loaded;
                            break;
                        }
                        case EntityType.FacialFeature:
                        {
                            var loaded = await GLTFLoader.LoadFacialFeature(bodyShape, definition);
                            if (sequence != _applySequence) return;
                            LOADED_FACIAL_FEATURES[definition.URN] = loaded;
                            break;
                        }
                    }
                }

                _loadedBodyShape = bodyShape;

                // --- Assemble (mirrors AvatarLoader.LoadAvatar post-load steps)
                var colors = new AvatarColors(outfit.eyeColor, outfit.hairColor, outfit.skinColor);
                var loadedCategories = LOADED_MODELS.Values.Select(m => m.Entity.Category).ToHashSet();
                var bodyGO = LOADED_MODELS.Values.FirstOrDefault(m => m.Entity.Type == EntityType.Body).Root;

                if (bodyGO != null)
                {
                    AvatarUtils.HideBodyShape(bodyGO, hiddenCategories, loadedCategories);
                    AvatarUtils.SetupFacialFeatures(bodyGO, colors, LOADED_FACIAL_FEATURES,
                        DEFAULT_BODY_FACIAL_FEATURES);
                }

                var outlineDump = new List<Renderer>(); // outline feature doesn't run in edit mode
                foreach (var (entity, go, _, _) in LOADED_MODELS.Values)
                {
                    go.SetActive(true);
                    AvatarUtils.SetupWearable(go, colors, outlineDump, avatarRootBone, avatarBones);

                    if (hiddenCategories.Contains(entity.Category))
                    {
                        go.SetActive(false);
                    }
                }

                SampleIdlePose(avatarAnimation);
                RepaintViews(bodyGO);

                status(unresolved.Count > 0
                        ? $"Preview updated — {slots.Count} wearables, {unresolved.Count} unresolved (see console)"
                        : $"Preview updated (edit mode) — {slots.Count} wearables",
                    unresolved.Count > 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                status($"Edit-mode preview failed: {e.Message}", true);
            }
        }

        /// <summary>Destroys all preview objects. The scene skeleton itself is untouched.</summary>
        public static void Clear()
        {
            _applySequence++; // aborts any in-flight apply

            foreach (var loaded in LOADED_MODELS.Values)
            {
                loaded.Disposable?.Dispose();
                if (loaded.Root != null) Object.DestroyImmediate(loaded.Root);
            }

            LOADED_MODELS.Clear();

            // Name-based sweep catches orphans whose tracking state died in a domain reload —
            // critical before play mode so the runtime loader starts on a clean rig
            foreach (var loader in Object.FindObjectsByType<AvatarLoader>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var container = loader.transform.Find(PREVIEW_CONTAINER_NAME);
                if (container != null) Object.DestroyImmediate(container.gameObject);
            }

            foreach (var urn in LOADED_FACIAL_FEATURES.Keys.ToList())
            {
                RemoveFacialFeature(urn);
            }

            // Default face textures belong to the (now destroyed) body importer — just forget them
            DEFAULT_BODY_FACIAL_FEATURES.Clear();
            _loadedBodyShape = null;

            // Restore the rig objects we activated so the scene returns to its saved state
            // (Bootstrap decides what's active at play time)
            foreach (var go in ACTIVATED_OBJECTS)
            {
                if (go != null) go.SetActive(false);
            }

            ACTIVATED_OBJECTS.Clear();

            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static Transform GetOrCreateContainer(Transform parent)
        {
            var existing = parent.Find(PREVIEW_CONTAINER_NAME);
            if (existing != null) return existing;

            var go = new GameObject(PREVIEW_CONTAINER_NAME) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>
        /// Activates every inactive ancestor (and the object itself), remembering them so
        /// <see cref="Clear"/> can restore the original state.
        /// </summary>
        private static void EnsureActiveInHierarchy(Transform target)
        {
            for (var t = target; t != null; t = t.parent)
            {
                if (t.gameObject.activeSelf) continue;

                t.gameObject.SetActive(true);
                ACTIVATED_OBJECTS.Add(t.gameObject);
            }
        }

        private static void RemoveModel(string urn)
        {
            if (!LOADED_MODELS.Remove(urn, out var loaded)) return;

            loaded.Disposable?.Dispose();
            if (loaded.Root != null) Object.DestroyImmediate(loaded.Root);
        }

        private static void RemoveFacialFeature(string urn)
        {
            if (!LOADED_FACIAL_FEATURES.Remove(urn, out var loaded)) return;

            if (loaded.Main != null) Object.DestroyImmediate(loaded.Main);
            if (loaded.Mask != null) Object.DestroyImmediate(loaded.Mask);
        }

        /// <summary>
        /// Play mode gets these from Bootstrap.Start; in edit mode we pull the same serialized
        /// references off the Bootstrap component and set the glTFast defer agent explicitly
        /// (the lazily-created default agent doesn't tick outside play mode).
        /// </summary>
        private static void EnsureEditModeSetup()
        {
            if (CommonAssets.AvatarMaterial == null || CommonAssets.FacialFeaturesMaterial == null)
            {
                var bootstrap = Object.FindFirstObjectByType<Bootstrap>(FindObjectsInactive.Include);
                if (bootstrap == null)
                {
                    throw new InvalidOperationException("No Bootstrap in the scene — open Assets/Scenes/Main.unity");
                }

                var serialized = new SerializedObject(bootstrap);
                CommonAssets.AvatarMaterial = serialized.FindProperty("baseMat").objectReferenceValue as Material;
                CommonAssets.FacialFeaturesMaterial =
                    serialized.FindProperty("facialFeaturesMat").objectReferenceValue as Material;
            }

            if (!_deferAgentSet)
            {
                GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent());
                _deferAgentSet = true;
            }
        }

        /// <summary>
        /// Samples the skeleton's "Idle" clip at t=0 so the preview isn't stuck in bind pose.
        /// Note: this moves scene skeleton bones (may mark the scene dirty) — harmless, play mode
        /// re-animates them anyway.
        /// </summary>
        private static void SampleIdlePose(Animation avatarAnimation)
        {
            if (avatarAnimation == null) return;

            var idleClip = avatarAnimation.GetClip("Idle");
            if (idleClip == null) return;

            idleClip.SampleAnimation(avatarAnimation.gameObject, 0f);
        }

        private static void SetDontSaveRecursive(GameObject root)
        {
            if (root == null) return;

            root.hideFlags |= HideFlags.DontSave;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags |= HideFlags.DontSave;
            }
        }

        private static void RepaintViews(GameObject bodyGO)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();

            if (_framedOnce || bodyGO == null) return;
            _framedOnce = true;

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                var center = bodyGO.transform.position + Vector3.up * 1f;
                sceneView.Frame(new Bounds(center, new Vector3(1.5f, 2.4f, 1.5f)), false);
            }
        }
    }
}
