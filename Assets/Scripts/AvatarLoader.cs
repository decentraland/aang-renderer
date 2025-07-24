using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline;
using GLTF;
using JetBrains.Annotations;
using Rendering;
using Services;
using UnityEngine;
using UnityEngine.Assertions;

public class AvatarLoader : MonoBehaviour
{
    private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
    private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");
    private static readonly int MASK_TEX_ID = Shader.PropertyToID("_MaskTex");

    private const string IDLE_CLIP_NAME = "Idle_Male";

    [SerializeField] private Camera mainCamera;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animation avatarAnimation;
    [SerializeField] private Transform avatarRootBone;
    [SerializeField] private Transform[] avatarBones;

    [Header("Highlight"), SerializeField] private bool setsHighlight;
    [SerializeField] private Vector3 highlightCenter = new(0, 0.18f, 0);
    [SerializeField] private Vector2 highlightSize = new(0.57f, 2.3f);

    private BodyShape? _loadedBodyShape;

    private readonly Dictionary<string, (EntityDefinition entity, GameObject root, IDisposable disposable,
            List<Renderer> outlineRenderers)>
        _loadedModels = new();

    private readonly Dictionary<string, (EntityDefinition entity, Texture2D main, Texture2D mask)>
        _loadedFacialFeatures = new();

    private (EntityDefinition entity, AnimationClip anim, AudioClip audio, GameObject prop, IDisposable disposable)?
        _loadedEmote;

    private readonly Dictionary<string, (Texture2D main, Texture2D mask)> _defaultBodyFacialFeatures = new();

    private readonly HashSet<string> _hiddenCategories = new();

    public async Awaitable LoadAvatar(BodyShape bodyShape, IEnumerable<EntityDefinition> wearableDefinitions,
        [CanBeNull] EntityDefinition emoteDefinition, string[] forceRenderUrns, AvatarColors colors)
    {
        var bodyEntity = EntityService.GetBodyEntity(bodyShape);
        var definitions = wearableDefinitions.Prepend(bodyEntity).ToList();

        var hiddenCategories = AvatarHideHelper.HideWearables(bodyShape, definitions, null, null, forceRenderUrns);

        var hasBodyShapeChanged = bodyShape != _loadedBodyShape;
        var definitionsToLoad = hasBodyShapeChanged
            ? definitions
            : definitions.Where(ed => !_loadedModels.ContainsKey(ed.URN) && !_loadedFacialFeatures.ContainsKey(ed.URN));

        var modelLoadTasks = new List<Task<(EntityDefinition entity, GameObject go, IDisposable disposable)>>();
        var facialFeaturesLoadTasks = new List<Task<(EntityDefinition entity, Texture2D main, Texture2D mask)>>();

        foreach (var def in definitionsToLoad)
        {
            if (def.Type is EntityType.Body or EntityType.Wearable)
            {
                modelLoadTasks.Add(GLTFLoader.LoadModel(bodyShape, def, transform));
            }
            else if (def.Type is EntityType.FacialFeature)
            {
                facialFeaturesLoadTasks.Add(GLTFLoader.LoadFacialFeature(bodyShape, def));
            }
            else
            {
                throw new NotSupportedException("Trying to load entity type " + def.Type);
            }
        }

        var modelLoadResults = await Task.WhenAll(modelLoadTasks);
        var facialFeaturesLoadResults = await Task.WhenAll(facialFeaturesLoadTasks);
        var emoteLoadResult = emoteDefinition != null && emoteDefinition.URN != _loadedEmote?.entity.URN
            ? await GLTFLoader.LoadEmote(bodyShape, emoteDefinition, transform)
            : ((AnimationClip anim, AudioClip audio, GameObject prop, IDisposable disposable)?)null;

        var emoteChanged = _loadedEmote?.entity.URN != emoteDefinition?.URN;

        // Clean up previous emote prop
        if (emoteChanged && _loadedEmote != null)
        {
            if (_loadedModels.Remove(_loadedEmote.Value.entity.URN, out var value))
            {
                Destroy(value.root);
            }
        }

        if (emoteChanged)
        {
            _loadedEmote = (emoteDefinition, emoteLoadResult!.Value.anim, emoteLoadResult.Value.audio,
                emoteLoadResult.Value.prop, emoteLoadResult.Value.disposable);
        }

        var newModels = modelLoadResults.ToList();
        var newFacialFeatures = facialFeaturesLoadResults.ToList();

        // Remove already loaded models
        foreach (var urn in _loadedModels.Keys.ToList())
        {
            if (!hasBodyShapeChanged && definitions.Any(ed => ed.URN == urn)) continue;

            _loadedModels.Remove(urn, out var value);
            value.disposable?.Dispose();
            Destroy(value.root);
        }

        // Add new ones
        foreach (var tuple in newModels)
        {
            _loadedModels.Add(tuple.entity.URN, (tuple.entity, tuple.go, tuple.disposable, new List<Renderer>()));
        }

        // And the emote prop
        if (_loadedEmote?.prop != null)
        {
            _loadedModels.Add(_loadedEmote.Value.entity.URN!,
                (_loadedEmote.Value.entity, _loadedEmote.Value.prop, _loadedEmote.Value.disposable,
                    new List<Renderer>()));
        }

        // Remove already loaded facial features
        foreach (var urn in _loadedFacialFeatures.Keys.ToList())
        {
            if (!hasBodyShapeChanged && definitions.Any(ed => ed.URN == urn)) continue;

            _loadedFacialFeatures.Remove(urn, out var value);

            Destroy(value.main);
            Destroy(value.mask);
        }

        // Add new ones
        foreach (var (entity, main, mask) in newFacialFeatures)
        {
            _loadedFacialFeatures.Add(entity.URN, (entity, main, mask));
        }

        // If body was changed we need to clear the default facial features
        if (hasBodyShapeChanged)
        {
            _defaultBodyFacialFeatures.Clear();
        }

        // Hide stuff on body shape if applicable and setup facial features
        var loadedCategories = _loadedModels.Values.Select(v => v.entity.Category).ToHashSet();
        Assert.AreEqual(_loadedModels.Count, loadedCategories.Count, "We loaded a category twice");
        var bodyGO = _loadedModels.Values.FirstOrDefault(er => er.entity.Type == EntityType.Body).root;
        if (bodyGO != null)
        {
            AvatarHideHelper.HideBodyShape(bodyGO, hiddenCategories, loadedCategories);

            // Setup facial features
            foreach (var cat in WearablesConstants.FACIAL_FEATURES)
            {
                var ffRenderer = GetFacialFeatureRenderer(cat, bodyGO);

                var color = GetFacialFeatureColor(cat, colors);
                ffRenderer.material.SetColor(BASE_COLOR_ID, color);

                // Save the default ones so we can revert
                if (!_defaultBodyFacialFeatures.ContainsKey(cat))
                {
                    _defaultBodyFacialFeatures[cat] = ((Texture2D)ffRenderer.material.GetTexture(MAIN_TEX_ID),
                        (Texture2D)ffRenderer.material.GetTexture(MASK_TEX_ID));
                }

                var loadedFeature = _loadedFacialFeatures.Values.FirstOrDefault(ff => ff.entity.Category == cat);

                var main = loadedFeature.entity != null ? loadedFeature.main : _defaultBodyFacialFeatures[cat].main;
                var mask = loadedFeature.entity != null ? loadedFeature.mask : _defaultBodyFacialFeatures[cat].mask;

                // The default mask for eyes is all white
                if (cat == WearablesConstants.Categories.EYES && mask == null)
                {
                    mask = Texture2D.whiteTexture;
                }

                ffRenderer.material.SetTexture(MAIN_TEX_ID, main);
                ffRenderer.material.SetTexture(MASK_TEX_ID, mask);
            }
        }

        // Activate all models, setup colors, change root bone for animation
        RendererFeature_AvatarOutline.m_AvatarOutlineRenderers.Clear();
        foreach (var (ed, go, _, outlineRenderers) in _loadedModels.Values)
        {
            go.SetActive(true);
            outlineRenderers.Clear();

            // Colors
            var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var r in renderers)
            {
                if (r.material.name.Contains("skin", StringComparison.OrdinalIgnoreCase))
                {
                    r.material.SetColor(BASE_COLOR_ID, colors.Skin);
                }
                else if (r.material.name.Contains("hair", StringComparison.OrdinalIgnoreCase))
                {
                    r.material.SetColor(BASE_COLOR_ID, colors.Hair);
                }

                r.rootBone = avatarRootBone;
                r.bones = avatarBones;

                if (r.material.shader.name == "DCL/DCL_Toon" && r.sharedMaterial.renderQueue is >= 2000 and < 3000)
                {
                    outlineRenderers.Add(r);
                }
            }

            if (_hiddenCategories.Contains(ed.Category))
            {
                go.SetActive(false);
            }
        }

        // If there is a new emote to be played
        if (emoteChanged && _loadedEmote != null)
        {
            avatarAnimation.AddClip(_loadedEmote.Value.anim, _loadedEmote.Value.entity.URN);

            // Add audio trigger
            if (_loadedEmote.Value.audio != null)
            {
                // TODO: Should we cleanup old events?
                avatarAnimation.GetClip(_loadedEmote.Value.entity.URN).AddEvent(new AnimationEvent
                {
                    time = 0,
                    functionName = "Play"
                });
            }
        }

        // Crossfade
        if (_loadedEmote != null)
        {
            avatarAnimation.CrossFade(_loadedEmote.Value.entity.URN, 0.3f);
            avatarAnimation.CrossFadeQueued(IDLE_CLIP_NAME, 0.3f);
        }
        else
        {
            avatarAnimation.CrossFade(IDLE_CLIP_NAME, 0.3f);
        }

        // TODO: Remove stale emotes

        _loadedBodyShape = bodyShape;

        // Update character bounds for background highlight
        UpdateHighlight();
    }

    public void TryHideCategory(string category, bool hidden)
    {
        var categoryGO = _loadedModels.Values.FirstOrDefault(c => c.entity.Category == category).root;
        categoryGO?.SetActive(!hidden);

        if (hidden)
        {
            _hiddenCategories.Add(category);
        }
        else
        {
            _hiddenCategories.Remove(category);
        }
    }

    private void Update()
    {
        foreach (var (_, root, _, outlineRenderers) in _loadedModels.Values)
        {
            if (root.activeInHierarchy)
            {
                RendererFeature_AvatarOutline.m_AvatarOutlineRenderers.AddRange(outlineRenderers);
            }
        }

        // Update character bounds every frame for dynamic positioning
        if (setsHighlight && _loadedModels.Count > 0)
        {
            UpdateHighlight();
        }
    }

    private void UpdateHighlight()
    {
        var worldCenter = transform.TransformPoint(highlightCenter);
        var worldSize = Vector2.Scale(highlightSize, transform.lossyScale);

        // TODO: Optimize, this can be done in 2 calls
        var leftSide = mainCamera.WorldToViewportPoint(worldCenter + mainCamera.transform.right * (worldSize.x / 2f));
        var rightSide = mainCamera.WorldToViewportPoint(worldCenter - mainCamera.transform.right * (worldSize.x / 2f));
        var topSide = mainCamera.WorldToViewportPoint(worldCenter + mainCamera.transform.up * (worldSize.y / 2f));
        var bottomSide = mainCamera.WorldToViewportPoint(worldCenter - mainCamera.transform.up * (worldSize.y / 2f));

        var vpCenter = mainCamera.WorldToViewportPoint(worldCenter);

        var viewportWidth = rightSide.x - leftSide.x;
        var viewportHeight = topSide.y - bottomSide.y;

        BackgroundRendererFeature.HighlightBounds = new Bounds(
            new Vector3(vpCenter.x, vpCenter.y),
            new Vector2(viewportWidth, viewportHeight));
    }

    private SkinnedMeshRenderer GetFacialFeatureRenderer(string category, GameObject bodyGO)
    {
        var suffix = category switch
        {
            WearablesConstants.Categories.EYEBROWS => "Mask_Eyebrows",
            WearablesConstants.Categories.EYES => "Mask_Eyes",
            WearablesConstants.Categories.MOUTH => "Mask_Mouth",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };

        var meshRenderers = bodyGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        return meshRenderers.FirstOrDefault(mr => mr.name.EndsWith(suffix));
    }

    private Color GetFacialFeatureColor(string category, AvatarColors colors)
    {
        return category switch
        {
            WearablesConstants.Categories.EYEBROWS => colors.Hair,
            WearablesConstants.Categories.EYES => colors.Eyes,
            WearablesConstants.Categories.MOUTH => colors.Skin,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };
    }
}