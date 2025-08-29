using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline;
using JetBrains.Annotations;
using Rendering;
using Services;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using Utils;

namespace Loading
{
    public class AvatarLoader : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;

        [FormerlySerializedAs("emoteEventReceiver")] [SerializeField]
        private EmoteAnimationController emoteAnimationController;

        [SerializeField] private Animation avatarAnimation;
        [SerializeField] private Transform avatarRootBone;
        [SerializeField] private Transform[] avatarBones;

        [Header("Highlight"), SerializeField] private bool setsHighlight;
        [SerializeField] private Vector3 highlightCenter = new(0, 0.18f, 0);
        [SerializeField] private Vector2 highlightSize = new(0.57f, 2.3f);

        private BodyShape? _loadedBodyShape;

        private readonly Dictionary<string, LoadedModel> _loadedModels = new();
        private readonly Dictionary<string, LoadedFacialFeature> _loadedFacialFeatures = new();
        private LoadedEmote? _loadedEmote;

        private readonly Dictionary<string, (Texture2D main, Texture2D mask)> _defaultBodyFacialFeatures = new();

        private readonly HashSet<string> _hiddenCategories = new();

        public async Awaitable LoadAvatar(BodyShape bodyShape, IEnumerable<EntityDefinition> wearableDefinitions,
            [CanBeNull] EntityDefinition emoteDefinition, string[] forceRenderCategories, AvatarColors colors)
        {
            var bodyEntity = EntityService.GetBodyEntity(bodyShape);
            var definitions = wearableDefinitions.Prepend(bodyEntity).ToList();

            var hiddenCategories = AvatarUtils.HideWearables(bodyShape, definitions, null, forceRenderCategories);

            var hasBodyShapeChanged = bodyShape != _loadedBodyShape;
            var definitionsToLoad = hasBodyShapeChanged
                ? definitions
                : definitions.Where(ed => !_loadedModels.ContainsKey(ed.URN) && !_loadedFacialFeatures.ContainsKey(ed.URN));

            var modelLoadTasks = new List<Task<LoadedModel>>();
            var facialFeaturesLoadTasks = new List<Task<LoadedFacialFeature>>();

            foreach (var def in definitionsToLoad)
            {
                if (def.Type is EntityType.Body or EntityType.Wearable)
                {
                    var task = GLTFLoader.LoadModel(bodyShape, def, transform);
                    modelLoadTasks.Add(task);
                    if (!AangConfiguration.Instance.ConcurrentLoad) await task;
                }
                else if (def.Type is EntityType.FacialFeature)
                {
                    var task = GLTFLoader.LoadFacialFeature(bodyShape, def);
                    facialFeaturesLoadTasks.Add(task);
                    if (!AangConfiguration.Instance.ConcurrentLoad) await task;
                }
                else
                {
                    throw new NotSupportedException("Trying to load entity type " + def.Type);
                }
            }

            var modelLoadResults = await Task.WhenAll(modelLoadTasks);
            var facialFeaturesLoadResults = await Task.WhenAll(facialFeaturesLoadTasks);
            var emoteLoadResult = emoteDefinition != null && emoteDefinition.URN != _loadedEmote?.Entity.URN
                ? await GLTFLoader.LoadEmote(bodyShape, emoteDefinition, transform)
                : (LoadedEmote?)null;

            Debug.Log("EMOTE LOADED");

            var emoteChanged = _loadedEmote?.Entity.URN != emoteDefinition?.URN;

            // Clean up previous emote prop / audio
            if (emoteChanged && _loadedEmote != null)
            {
                _loadedEmote.Value.Disposable.Dispose();
                Destroy(_loadedEmote.Value.Prop);
            }

            if (emoteChanged)
            {
                _loadedEmote = emoteLoadResult;
            }

            var newModels = modelLoadResults.ToList();
            var newFacialFeatures = facialFeaturesLoadResults.ToList();

            // Remove already loaded models
            foreach (var urn in _loadedModels.Keys.ToList())
            {
                if (!hasBodyShapeChanged && definitions.Any(ed => ed.URN == urn)) continue;

                _loadedModels.Remove(urn, out var value);
                value.Disposable?.Dispose();
                Destroy(value.Root);
            }

            // Add new ones
            foreach (var loadedModel in newModels)
            {
                _loadedModels.Add(loadedModel.Entity.URN, loadedModel);
            }

            // Remove already loaded facial features
            foreach (var urn in _loadedFacialFeatures.Keys.ToList())
            {
                if (!hasBodyShapeChanged && definitions.Any(ed => ed.URN == urn)) continue;

                _loadedFacialFeatures.Remove(urn, out var value);

                Destroy(value.Main);
                Destroy(value.Mask);
            }

            // Add new ones
            foreach (var loadedFacialFeature in newFacialFeatures)
            {
                _loadedFacialFeatures.Add(loadedFacialFeature.Entity.URN, loadedFacialFeature);
            }

            // If body was changed we need to clear the default facial features
            if (hasBodyShapeChanged)
            {
                _defaultBodyFacialFeatures.Clear();
            }

            // Hide stuff on body shape if applicable and setup facial features
            var loadedCategories = _loadedModels.Values.Select(v => v.Entity.Category).ToHashSet();
            Assert.AreEqual(_loadedModels.Count, loadedCategories.Count, "We loaded a category twice");
            var bodyGO = _loadedModels.Values.FirstOrDefault(er => er.Entity.Type == EntityType.Body).Root;
            if (bodyGO != null)
            {
                AvatarUtils.HideBodyShape(bodyGO, hiddenCategories, loadedCategories);
                AvatarUtils.SetupFacialFeatures(bodyGO, colors, _loadedFacialFeatures, _defaultBodyFacialFeatures);
            }

            // Activate all models, setup colors, change root bone for animation
            RendererFeature_AvatarOutline.m_AvatarOutlineRenderers.Clear();
            foreach (var (ed, go, _, outlineRenderers) in _loadedModels.Values)
            {
                go.SetActive(true);
                outlineRenderers.Clear();

                // Colors
                AvatarUtils.SetupColors(go, colors, outlineRenderers, avatarRootBone, avatarBones);

                if (_hiddenCategories.Contains(ed.Category))
                {
                    go.SetActive(false);
                }
            }

            // If there is a new emote to be played
            if (emoteChanged)
            {
                if (_loadedEmote != null)
                {
                    emoteAnimationController.PlayEmote(_loadedEmote.Value);
                }
                else
                {
                    emoteAnimationController.StopEmote(true);
                }
            }

            _loadedBodyShape = bodyShape;

            // Update character bounds for background highlight
            UpdateHighlight();
        }

        public void TryHideCategory(string category, bool hidden)
        {
            var categoryGO = _loadedModels.Values.FirstOrDefault(c => c.Entity.Category == category).Root;
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
    }

    public readonly struct LoadedModel
    {
        public readonly EntityDefinition Entity;
        public readonly GameObject Root;
        public readonly IDisposable Disposable;
        public readonly List<Renderer> OutlineRenderers;

        public LoadedModel(EntityDefinition entity, GameObject root, IDisposable disposable)
        {
            Entity = entity;
            Root = root;
            Disposable = disposable;
            OutlineRenderers = new List<Renderer>();
        }

        public void Deconstruct(out EntityDefinition entity, out GameObject root, out IDisposable disposable,
            out List<Renderer> outlineRenderers)
        {
            entity = Entity;
            root = Root;
            disposable = Disposable;
            outlineRenderers = OutlineRenderers;
        }
    }

    public readonly struct LoadedFacialFeature
    {
        public readonly EntityDefinition Entity;
        public readonly Texture2D Main;
        public readonly Texture2D Mask;

        public LoadedFacialFeature(EntityDefinition entity, Texture2D main, Texture2D mask)
        {
            Entity = entity;
            Main = main;
            Mask = mask;
        }
    }

    public readonly struct LoadedEmote
    {
        public readonly EntityDefinition Entity;
        public readonly AnimationClip Clip;
        [CanBeNull] public readonly AudioClip Audio;
        [CanBeNull] public readonly GameObject Prop;
        [CanBeNull] public readonly Animation PropAnim;
        public readonly IDisposable Disposable;

        public LoadedEmote(EntityDefinition entity, AnimationClip clip, AudioClip audio, GameObject prop, Animation propAnim, IDisposable disposable)
        {
            Entity = entity;
            Clip = clip;
            Audio = audio;
            Prop = prop;
            PropAnim = propAnim;
            Disposable = disposable;
        }
    }
}