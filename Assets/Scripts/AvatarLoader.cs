using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using GLTF;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;

public class AvatarLoader : MonoBehaviour
{
    private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
    private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");
    private static readonly int MASK_TEX_ID = Shader.PropertyToID("_MaskTex");

    [SerializeField] private AudioSource audioSource;

    private BodyShape? _loadedBodyShape;
    private readonly Dictionary<string, (EntityDefinition entity, GameObject root)> _loadedModels = new();

    private readonly Dictionary<string, (EntityDefinition entity, Texture2D main, Texture2D mask)>
        _loadedFacialFeatures = new();

    private (EntityDefinition entity, AnimationClip anim, AudioClip audio, GameObject prop) _loadedEmote;

    private readonly Dictionary<string, (Texture2D main, Texture2D mask)> _defaultBodyFacialFeatures = new();

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

        var modelLoadTasks = new List<Task<(EntityDefinition entity, GameObject go)>>();
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
        var emoteLoadResult = emoteDefinition != null && emoteDefinition.URN != _loadedEmote.entity?.URN
            ? await GLTFLoader.LoadEmote(bodyShape, emoteDefinition, transform)
            : (null, null, null);

        // NOTE: No awaits from here on!

        var emoteChanged = _loadedEmote.entity?.URN != emoteDefinition?.URN;
        var currentAnimState = GetCurrentClip(_loadedModels.Values.FirstOrDefault().root?.GetComponent<Animation>(),
            _loadedEmote.entity?.URN);
        var previousEmote = _loadedEmote;

        // Clean up previous emote prop
        if (emoteChanged && _loadedEmote.entity != null)
        {
            if (_loadedModels.Remove(_loadedEmote.entity.URN, out var value))
            {
                Destroy(value.root);
            }
        }

        if (emoteChanged)
        {
            _loadedEmote = (emoteDefinition, emoteLoadResult.anim, emoteLoadResult.audio, emoteLoadResult.prop);
        }

        var newModels = modelLoadResults.ToList();
        var newFacialFeatures = facialFeaturesLoadResults.ToList();

        // Remove already loaded models
        foreach (var urn in _loadedModels.Keys.ToList())
        {
            if (!hasBodyShapeChanged && definitions.Any(ed => ed.URN == urn)) continue;

            _loadedModels.Remove(urn, out var value);
            Destroy(value.root);
        }

        // Add new ones
        foreach (var (entity, root) in newModels)
        {
            _loadedModels.Add(entity.URN, (entity, root));
        }

        // And the emote
        if (_loadedEmote.prop != null)
        {
            _loadedModels.Add(_loadedEmote.entity!.URN!, (_loadedEmote.entity, _loadedEmote.prop));
        }

        // Remove already loaded facial features
        foreach (var urn in _loadedFacialFeatures.Keys.ToList())
        {
            if (!hasBodyShapeChanged && definitions.Any(ed => ed.URN == urn)) continue;

            _loadedFacialFeatures.Remove(urn, out var value);

            // TODO: Should we destroy the tex?
            Destroy(value.main);
            Destroy(value.mask);
        }

        // Add new ones
        foreach (var (entity, main, mask) in newFacialFeatures)
        {
            _loadedFacialFeatures.Add(entity.URN, (entity, main, mask));
        }

        // Activate all models, setup colors, and animations
        var animEventAdded = false;
        foreach (var (_, go) in _loadedModels.Values)
        {
            go.SetActive(true);

            // Colors
            var renderers = go.GetComponentsInChildren<Renderer>();
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
            }

            // Animations
            if (!go.TryGetComponent<Animation>(out var anim))
            {
                anim = go.AddComponent<Animation>();
                anim.playAutomatically = false;
                anim.AddClip(CommonAssets.IdleAnimation, "idle");

                // If we added a new object we set its animation to the previous playing one so we can crossfade
                if (previousEmote.entity != null)
                {
                    anim.AddClip(previousEmote.anim, previousEmote.entity.URN);
                }

                anim.Play(currentAnimState.clipName);
                anim[currentAnimState.clipName].time = currentAnimState.time;
                anim.Sample();
            }

            // If there is a new emote to be played
            if (emoteChanged)
            {
                if (_loadedEmote.entity != null)
                {
                    anim.AddClip(_loadedEmote.anim, _loadedEmote.entity.URN);
                }

                // Add audio trigger
                if (_loadedEmote.audio != null && !animEventAdded)
                {
                    // TODO: Should we cleanup old events?
                    animEventAdded = true;
                    anim.GetClip(_loadedEmote.entity.URN).AddEvent(new AnimationEvent
                    {
                        time = 0,
                        functionName = "Play"
                    });
                    var aer = go.AddComponent<AudioEventReceiver>();
                    aer.AudioSource = audioSource;
                }
            }

            // Crossfade
            if (_loadedEmote.entity != null)
            {
                anim.CrossFade(_loadedEmote.entity.URN, 0.3f);
                anim.CrossFadeQueued("idle", 0.3f);
            }
            else
            {
                anim.CrossFade("idle", 0.3f);
            }
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
                if (loadedFeature.entity != null)
                {
                    ffRenderer.material.SetTexture(MAIN_TEX_ID, loadedFeature.main);
                    ffRenderer.material.SetTexture(MASK_TEX_ID, loadedFeature.mask);
                }
                else
                {
                    // Revert to default
                    var defaultFeature = _defaultBodyFacialFeatures[cat];
                    ffRenderer.material.SetTexture(MAIN_TEX_ID, defaultFeature.main);
                    ffRenderer.material.SetTexture(MASK_TEX_ID, defaultFeature.mask);
                }
            }
        }

        _loadedBodyShape = bodyShape;
    }

    private static (string clipName, float time) GetCurrentClip([CanBeNull] Animation anim, string previousEmoteName)
    {
        if (anim == null)
        {
            return ("idle", 0);
        }

        if (previousEmoteName != null && anim.IsPlaying(previousEmoteName))
        {
            return (previousEmoteName, anim[previousEmoteName].time);
        }

        return ("idle", anim["idle"].time);
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