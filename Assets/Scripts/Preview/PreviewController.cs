using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Loading;
using Services;
using UnityEngine;
using UnityEngine.Assertions;
using Utils;

namespace Preview
{
    public class PreviewController : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;

        [SerializeField] private DragRotator dragRotator;
        [SerializeField] private PreviewUIPresenter previewUIPresenter;

        [SerializeField] private AvatarLoader avatarLoader;
        [SerializeField] private WearableLoader wearableLoader;

        [SerializeField] private EmoteAnimationController emoteAnimationController;

        [SerializeField] private GameObject animationReference;
        [SerializeField] private GameObject platform;

        [SerializeField] private float wearablePadding = 0.15f;

        private bool _loading;
        private bool _shouldReload;
        private bool _shouldCleanup;

        private void Start()
        {
            previewUIPresenter.ShowAvatarClicked += OnShowAvatarClicked;
            previewUIPresenter.ShowWearableClicked += OnShowWearableClicked;
            previewUIPresenter.EmoteToggleClicked += OnEmoteToggleClicked;
            previewUIPresenter.ContainerDrag += dragRotator.OnDrag;
            emoteAnimationController.EmoteAnimationEnded += OnEmoteAnimationEnded;

            StartCoroutine(Reload());
        }

        private void OnEmoteAnimationEnded()
        {
            previewUIPresenter.SetAnimationPlaying(false);
        }

        private void OnEmoteToggleClicked(bool playing)
        {
            avatarLoader.StopEmote(!playing, true);
        }

        private void OnShowWearableClicked()
        {
            PlayerPrefs.SetInt("PreviewAvatarShown", 0);

            avatarLoader.gameObject.SetActive(false);
            wearableLoader.gameObject.SetActive(true);

            dragRotator.ResetRotation();
            dragRotator.AllowVertical = true;
        }

        private void OnShowAvatarClicked()
        {
            PlayerPrefs.SetInt("PreviewAvatarShown", 1);

            avatarLoader.gameObject.SetActive(true);
            wearableLoader.gameObject.SetActive(false);

            dragRotator.ResetRotation();
            dragRotator.AllowVertical = false;
        }

        public void InvokeReload()
        {
            _shouldCleanup = false;
            StartCoroutine(Reload());
        }

        private async Awaitable Reload()
        {
            if (_loading)
            {
                _shouldReload = true;
                return;
            }

            Cleanup();
            previewUIPresenter.ShowLoader(true);
            _loading = true;
            mainCamera.cullingMask = 0; // Render nothing
            avatarLoader.enabled = false; // Disables Update for Outline
            wearableLoader.enabled = false; // Disables Update for Outline

            do
            {
                _shouldReload = false;

                // We store the instance in case it gets recreated by a call to AangConfiguration.RecreateFrom
                var config = AangConfiguration.Instance;

                dragRotator.enabled = false;
                dragRotator.ResetRotation();

                avatarLoader.gameObject.SetActive(true);
                wearableLoader.gameObject.SetActive(true);

                animationReference.SetActive(config.ShowAnimationReference);
                platform.SetActive(config.Mode is PreviewMode.Authentication);
                mainCamera.backgroundColor = config.Background;
                mainCamera.orthographic = config.Projection == "orthographic";
                previewUIPresenter.EnableLoader(!config.DisableLoader);
                mainCamera.GetComponent<PreviewCameraController>().SetMode(config.Mode);

                var hasEmoteOverride = false;
                var hasWearableOverride = false;
                var hasEmoteAudio = false;
                var showingAvatar = false;

                try
                {
                    await EntityService.PreloadBodyEntities();

                    switch (config.Mode)
                    {
                        case PreviewMode.Marketplace:
                            var urns = await LoadUrns(config);
                            Assert.IsTrue(urns.Count == 1, $"Marketplace mode only allows one urn, found: {urns.Count}");
                            var result = await LoadForMarketplace(config.Profile, urns[0], config.Emote);

                            previewUIPresenter.EnableEmoteControls(result.emoteOverride);

                            if (result.validRepresentation)
                            {
                                showingAvatar = PlayerPrefs.GetInt("PreviewAvatarShown", 0) == 1 || result.emoteOverride;
                                previewUIPresenter.SetSwitcherState(
                                    showingAvatar
                                        ? PreviewUIPresenter.SwitcherState.Avatar
                                        : PreviewUIPresenter.SwitcherState.Wearable, result.avatarBodyShape);
                            }
                            else
                            {
                                previewUIPresenter.SetSwitcherState(PreviewUIPresenter.SwitcherState.WearableLocked,
                                    result.avatarBodyShape);
                            }

                            hasEmoteOverride = result.emoteOverride;
                            hasWearableOverride = !hasEmoteOverride;
                            hasEmoteAudio = result.emoteOverrideAudio;
                            break;
                        case PreviewMode.Authentication:
                        case PreviewMode.Profile:
                            showingAvatar = true;
                            await LoadForProfile(config.Profile, config.Emote);
                            break;
                        // case PreviewMode.Builder:
                        //     await LoadForBuilder(config.BodyShape, config.EyeColor, config.HairColor, config.SkinColor,
                        //         await GetUrns(config), config.Emote, config.Base64);
                        //     break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception e)
                {
                    JSBridge.NativeCalls.OnError(e.Message);
                    throw;
                }

                // Wait for 1 frame for animation to kick in before re-centering the object on screen
                await Awaitable.NextFrameAsync();

                if (hasWearableOverride)
                {
                    GameObjectUtils.CenterAndFit(wearableLoader.transform, mainCamera, wearablePadding);
                }
                else if (hasEmoteOverride)
                {
                    GameObjectUtils.CenterAndFit(avatarLoader.transform, mainCamera, wearablePadding);
                }

                avatarLoader.gameObject.SetActive(showingAvatar);
                wearableLoader.gameObject.SetActive(!showingAvatar);

                dragRotator.enabled = true;
                dragRotator.AllowVertical = config.Mode is PreviewMode.Marketplace or PreviewMode.Builder && !showingAvatar;
                dragRotator.EnableAutoRotate = config.Mode is PreviewMode.Marketplace && !hasEmoteOverride;

                previewUIPresenter.EnableEmoteControls(hasEmoteOverride);
                previewUIPresenter.EnableZoom(config.Mode is PreviewMode.Marketplace or PreviewMode.Builder);
                previewUIPresenter.EnableSwitcher(hasWearableOverride);
                previewUIPresenter.EnableAudioControls(hasEmoteAudio);
            } while (_shouldReload);

            previewUIPresenter.ShowLoader(false);
            _loading = false;
            mainCamera.cullingMask = -1; // Render everything
            avatarLoader.enabled = true; // Enables Update for Outline
            wearableLoader.enabled = true;

            if (_shouldCleanup)
            {
                Cleanup();
            }

            JSBridge.NativeCalls.OnLoadComplete();
        }

        private async Awaitable<(bool emoteOverride, bool emoteOverrideAudio, bool validRepresentation, BodyShape
                avatarBodyShape)>
            LoadForMarketplace(string profileID, string urn, string defaultEmote)
        {
            Assert.IsNotNull(profileID);
            Assert.IsNotNull(urn);
            Assert.IsNotNull(defaultEmote);

            var avatar = await APIService.GetAvatar(profileID);
            var avatarBodyShape = avatar.GetBodyShape();
            var avatarColors = avatar.GetAvatarColors();
            var allEntities = await EntityService.GetEntities(avatar.wearables.Append(urn).ToArray());
            var overrideDefinition = allEntities.First(ed => ed.URN == urn);

            bool hasValidRepresentation;
            IEnumerable<EntityDefinition> wearables;
            EntityDefinition emoteDefinition;

            switch (overrideDefinition.Type)
            {
                case EntityType.Emote:
                    emoteDefinition = overrideDefinition;
                    wearables = allEntities.Where(wd => wd.URN != emoteDefinition.URN);
                    hasValidRepresentation = true;
                    break;
                case EntityType.Wearable or EntityType.FacialFeature:
                    emoteDefinition = defaultEmote == "idle" ? null : EntityDefinition.FromEmbeddedEmote(defaultEmote);
                    wearables = allEntities.Where(ed =>
                        ed.Category != overrideDefinition.Category || ed.URN == overrideDefinition.URN);
                    hasValidRepresentation = overrideDefinition.HasRepresentation(avatarBodyShape);
                    break;
                default:
                    throw new NotSupportedException($"Trying to override type: {overrideDefinition.Type}");
            }

            // Load the avatar
            if (hasValidRepresentation)
            {
                await avatarLoader.LoadAvatar(avatarBodyShape, wearables, emoteDefinition,
                    avatar.forceRender.Append(overrideDefinition.Category).ToArray(),
                    avatarColors);
            }

            if (overrideDefinition.Type is EntityType.Wearable or EntityType.FacialFeature)
            {
                await wearableLoader.LoadWearable(overrideDefinition, avatarBodyShape, avatarColors);
            }
            else
            {
                wearableLoader.Cleanup();
            }

            // TODO: This check for audio clip is ugly
            return (overrideDefinition.Type == EntityType.Emote, emoteAnimationController.EmoteAudioClip != null,
                hasValidRepresentation, avatarBodyShape);
        }

        private async Awaitable LoadForProfile(string profileID, string defaultEmote)
        {
            Assert.IsNotNull(profileID);

            var avatar = await APIService.GetAvatar(profileID);
            var entities = await EntityService.GetEntities(avatar.wearables);

            await avatarLoader.LoadAvatar(avatar.GetBodyShape(), entities, EntityDefinition.FromEmbeddedEmote(defaultEmote),
                avatar.forceRender, avatar.GetAvatarColors());
        }

        private async Awaitable<List<string>> LoadUrns(AangConfiguration config)
        {
            if (config.Urns.Count > 0) return config.Urns;

            // If we have a contract and item id or token id we need to fetch the urn first
            if (config.Contract != null && (config.ItemID != null || config.TokenID != null))
            {
                return new List<string>
                {
                    config.ItemID != null
                        ? (await APIService.GetMarketplaceItemFromID(config.Contract, config.ItemID)).data[0].urn
                        : (await APIService.GetMarketplaceItemFromToken(config.Contract, config.TokenID)).data[0].nft
                        .urn
                };
            }

            return null;
        }

        public void Cleanup()
        {
            if (_loading)
            {
                _shouldCleanup = true;
                return;
            }

            _shouldCleanup = false;

            avatarLoader.StopEmote(true, false);
        }
    }
}