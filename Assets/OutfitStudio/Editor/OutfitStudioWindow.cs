using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Data;
using Newtonsoft.Json.Linq;
using Preview;
using Services;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Utils;
using Loading;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Artist tool for composing an outfit from marketplace wearables, posing the avatar with an
    /// emote and capturing stills / video. Browsing and preset editing work in edit mode; the live
    /// preview and capture drive the existing renderer (builder mode) in play mode.
    /// </summary>
    public class OutfitStudioWindow : EditorWindow
    {
        private const int PAGE_SIZE = 36;
        private const int THUMB_SIZE = 90;

        // Cap on tag-matched items collected from the catalyst lambdas endpoint per search (see
        // RunSearch) - a discovery-only pass, not the full result set, so this can stay well below
        // FETCH_CAP without losing practical recall.
        private const int TAG_SEARCH_CAP = 500;

        // The live marketplace-api ignores sortBy entirely (verified: every documented value -
        // newest, recently_listed, recently_sold, cheapest, most_expensive - returns items in the
        // exact same server order, prices/dates included). So there's no way to get a correct sort
        // via pagination; instead we fetch every item matching the current filters (up to this cap)
        // in one go and sort the whole set client-side. A true, uncapped global sort isn't practical
        // for a broad, unfiltered browse (e.g. ~11k wearables) without fetching the entire catalog,
        // so results are labelled "first N of total" whenever the cap is hit.
        private const int FETCH_CAP = 3000;
        private const float NECK_LOOK_SHARE = 0.4f; // fraction of the look-at turn given to the neck vs. the head

        private static readonly List<string> WEARABLE_SLOTS = new()
        {
            "any", "upper_body", "lower_body", "feet", "hands_wear", "hat", "helmet", "hair",
            "facial_hair", "eyewear", "earring", "tiara", "top_head", "mask", "skin",
            "eyes", "eyebrows", "mouth"
        };

        private static readonly List<string> EMOTE_CATEGORIES = new()
        {
            "any", "dance", "poses", "fun", "greetings", "reactions", "stunt", "horror", "miscellaneous"
        };

        // Wearable categories that make up a face/body look rather than an outfit item. Browsed from
        // the Avatar tab; picks there are editor-only preview overrides (see _previewFaceUrns) and are
        // deliberately never added to outfit.urns, so they never end up in a share code or preset.
        private static readonly List<string> FACE_SLOTS = new()
        {
            "eyes", "eyebrows", "mouth", "hair", "facial_hair"
        };

        private static readonly Dictionary<string, string> FACE_SLOT_LABELS = new()
        {
            ["eyes"] = "Eyes",
            ["eyebrows"] = "Eyebrows",
            ["mouth"] = "Mouth",
            ["hair"] = "Hair",
            ["facial_hair"] = "Facial Hair"
        };

        // Curated Decentraland base-avatar (off-chain) options per face-feature slot — first stage
        // deliberately skips the marketplace here: these off-chain URNs aren't resolvable via
        // CatalogService (marketplace-api only serves collection items), and the artist can still
        // reach marketplace hair/etc. through the Wearables tab. Mirrors the same curated set the
        // in-game avatar Configurator ships with (Assets/Scripts/Configurator/ConfiguratorController.cs,
        // faceCategories, serialized on the OutfitStudio scene).
        private static readonly Dictionary<string, string[]> DEFAULT_FACE_URNS = new()
        {
            ["hair"] = new[]
            {
                "urn:decentraland:off-chain:base-avatars:standard_hair",
                "urn:decentraland:off-chain:base-avatars:casual_hair_01",
                "urn:decentraland:off-chain:base-avatars:semi_afro",
                "urn:decentraland:off-chain:base-avatars:modern_hair",
                "urn:decentraland:off-chain:base-avatars:hair_anime_01",
                "urn:decentraland:off-chain:base-avatars:hair_undere",
                "urn:decentraland:off-chain:base-avatars:keanu_hair",
                "urn:decentraland:off-chain:base-avatars:shoulder_bob_hair",
                "urn:decentraland:off-chain:base-avatars:hair_f_oldie_02",
                "urn:decentraland:off-chain:base-avatars:cool_hair",
                "urn:decentraland:off-chain:base-avatars:tall_front_01",
                "urn:decentraland:off-chain:base-avatars:pony_tail",
                "urn:decentraland:off-chain:base-avatars:rasta",
                "urn:decentraland:off-chain:base-avatars:casual_hair_02",
                "urn:decentraland:off-chain:base-avatars:curtained_hair",
                "urn:decentraland:off-chain:base-avatars:semi_bold",
                "urn:decentraland:off-chain:base-avatars:curly_hair",
                "urn:decentraland:off-chain:base-avatars:double_bun",
                "urn:decentraland:off-chain:base-avatars:punk"
            },
            ["eyes"] = new[]
            {
                "urn:decentraland:off-chain:base-avatars:f_eyes_00",
                "urn:decentraland:off-chain:base-avatars:eyes_00",
                "urn:decentraland:off-chain:base-avatars:eyes_01",
                "urn:decentraland:off-chain:base-avatars:f_eyes_10",
                "urn:decentraland:off-chain:base-avatars:f_eyes_01",
                "urn:decentraland:off-chain:base-avatars:eyes_04",
                "urn:decentraland:off-chain:base-avatars:eyes_07",
                "urn:decentraland:off-chain:base-avatars:eyes_08",
                "urn:decentraland:off-chain:base-avatars:eyes_21",
                "urn:decentraland:off-chain:base-avatars:eyes_16",
                "urn:decentraland:off-chain:base-avatars:eyes_20",
                "urn:decentraland:off-chain:base-avatars:eyes_15",
                "urn:decentraland:off-chain:base-avatars:eyes_03",
                "urn:decentraland:off-chain:base-avatars:eyes_22",
                "urn:decentraland:off-chain:base-avatars:f_eyes_05",
                "urn:decentraland:off-chain:base-avatars:f_eyes_06",
                "urn:decentraland:off-chain:base-avatars:eyes_11",
                "urn:decentraland:off-chain:base-avatars:f_eyes_02",
                "urn:decentraland:off-chain:base-avatars:f_eyes_04",
                "urn:decentraland:off-chain:base-avatars:f_eyes_08"
            },
            ["eyebrows"] = new[]
            {
                "urn:decentraland:off-chain:base-avatars:eyebrows_00",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_00",
                "urn:decentraland:off-chain:base-avatars:eyebrows_01",
                "urn:decentraland:off-chain:base-avatars:eyebrows_02",
                "urn:decentraland:off-chain:base-avatars:eyebrows_04",
                "urn:decentraland:off-chain:base-avatars:eyebrows_05",
                "urn:decentraland:off-chain:base-avatars:eyebrows_07",
                "urn:decentraland:off-chain:base-avatars:eyebrows_09",
                "urn:decentraland:off-chain:base-avatars:eyebrows_11",
                "urn:decentraland:off-chain:base-avatars:eyebrows_12",
                "urn:decentraland:off-chain:base-avatars:eyebrows_14",
                "urn:decentraland:off-chain:base-avatars:eyebrows_15",
                "urn:decentraland:off-chain:base-avatars:eyebrows_17",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_02",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_03",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_04",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_05",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_06",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_07",
                "urn:decentraland:off-chain:base-avatars:eyebrows_8"
            },
            ["mouth"] = new[]
            {
                "urn:decentraland:off-chain:base-avatars:f_mouth_00",
                "urn:decentraland:off-chain:base-avatars:f_mouth_01",
                "urn:decentraland:off-chain:base-avatars:f_mouth_02",
                "urn:decentraland:off-chain:base-avatars:f_mouth_03",
                "urn:decentraland:off-chain:base-avatars:f_mouth_04",
                "urn:decentraland:off-chain:base-avatars:f_mouth_05",
                "urn:decentraland:off-chain:base-avatars:f_mouth_06",
                "urn:decentraland:off-chain:base-avatars:f_mouth_07",
                "urn:decentraland:off-chain:base-avatars:f_mouth_08",
                "urn:decentraland:off-chain:base-avatars:mouth_00",
                "urn:decentraland:off-chain:base-avatars:mouth_01",
                "urn:decentraland:off-chain:base-avatars:mouth_02",
                "urn:decentraland:off-chain:base-avatars:mouth_03",
                "urn:decentraland:off-chain:base-avatars:mouth_04",
                "urn:decentraland:off-chain:base-avatars:mouth_05",
                "urn:decentraland:off-chain:base-avatars:mouth_06",
                "urn:decentraland:off-chain:base-avatars:mouth_07",
                "urn:decentraland:off-chain:base-avatars:mouth_09",
                "urn:decentraland:off-chain:base-avatars:mouth_10",
                "urn:decentraland:off-chain:base-avatars:mouth_11"
            },
            ["facial_hair"] = new[]
            {
                "urn:decentraland:off-chain:base-avatars:balbo_beard",
                "urn:decentraland:off-chain:base-avatars:beard",
                "urn:decentraland:off-chain:base-avatars:chin_beard",
                "urn:decentraland:off-chain:base-avatars:french_beard",
                "urn:decentraland:off-chain:base-avatars:full_beard",
                "urn:decentraland:off-chain:base-avatars:goatee_beard",
                "urn:decentraland:off-chain:base-avatars:granpa_beard",
                "urn:decentraland:off-chain:base-avatars:handlebar",
                "urn:decentraland:off-chain:base-avatars:horseshoe_beard",
                "urn:decentraland:off-chain:base-avatars:lincoln_beard",
                "urn:decentraland:off-chain:base-avatars:mustache_short_beard",
                "urn:decentraland:off-chain:base-avatars:old_mustache_beard",
                "urn:decentraland:off-chain:base-avatars:short_boxed_beard"
            }
        };

        private static readonly List<string> RARITIES = new()
        {
            "any", "common", "uncommon", "rare", "epic", "legendary", "exotic", "mythic", "unique"
        };

        private static readonly List<string> GENDERS = new() { "any", "male", "female", "unisex" };

        // Labels match the Decentraland marketplace's own Sort dropdown, in the same order, so the
        // two tools read the same way. "Name" is a local-only convenience option the marketplace
        // doesn't have.
        private static readonly List<string> SORT_OPTIONS = new()
        {
            "Newest", "Recently Listed", "Recently Sold", "Cheapest", "Most Expensive", "Name"
        };

        private static readonly Dictionary<string, string> SORT_API_VALUES = new()
        {
            ["Newest"] = "newest",
            ["Recently Listed"] = "recently_listed",
            ["Recently Sold"] = "recently_sold",
            ["Cheapest"] = "cheapest",
            ["Most Expensive"] = "most_expensive",
            ["Name"] = "name"
        };

        private static readonly List<string> EMBEDDED_EMOTES = new()
        {
            "idle", "clap", "dab", "dance", "fashion", "fashion-2", "fashion-3", "fashion-4",
            "love", "money", "fist-pump", "head-explode"
        };

        // Shown in the "Embedded" popup whenever outfit.emote isn't one of EMBEDDED_EMOTES (a pose,
        // a marketplace/draft emote URN, ...). Without this, the popup silently fell back to
        // showing "idle" (index 0) while a pose was actually loaded/playing — reselecting "idle"
        // from that state is a no-op (same value = no change event), so nothing reloaded and the
        // transport buttons kept controlling the stale pose instead of the emote the popup claimed
        // to have selected. Having a distinct sentinel means any real embedded-emote pick is always
        // a genuine value change.
        private const string EMBEDDED_EMOTE_NONE = "— pose/other selected —";

        private static readonly List<string> EMBEDDED_EMOTE_CHOICES =
            new[] { EMBEDDED_EMOTE_NONE }.Concat(EMBEDDED_EMOTES).ToList();

        // Single-frame screenshot poses, kept fully inside the tool folder (Assets/OutfitStudio/Poses/)
        // so nothing spills into the rest of the repo. They still ride the stock embedded-emote path
        // with ZERO renderer changes: the emote name is resolved as Path.Combine(streamingAssetsPath,
        // name + ".glb"), so a name that walks back out of StreamingAssets with ".." lands in the tool
        // folder — "../OutfitStudio/Poses/<file>" → <project>/Assets/OutfitStudio/Poses/<file>.glb.
        // The ".." is normalised by the OS/URI when the loader opens the file (same bare-path handling
        // the StreamingAssets emotes already rely on). Editor-only (poses aren't in production builds).
        private const string POSES_DIR_UNDER_ASSETS = "OutfitStudio/Poses";       // for the file scan
        private const string POSES_EMBEDDED_PREFIX = "../OutfitStudio/Poses";      // relative to StreamingAssets

        // Default folder the "Save current…" card-colour-preset dialog points at (presets can live
        // anywhere - they're discovered project-wide by type).
        private const string CARD_PRESETS_DIR = "Assets/OutfitStudio/CardPresets";

        private static readonly Dictionary<string, Color> RARITY_COLORS = new()
        {
            ["common"] = new Color(0.67f, 0.79f, 0.85f),
            ["uncommon"] = new Color(1.00f, 0.65f, 0.40f),
            ["rare"] = new Color(0.34f, 0.87f, 0.62f),
            ["epic"] = new Color(0.44f, 0.62f, 1.00f),
            ["legendary"] = new Color(0.63f, 0.40f, 0.90f),
            ["exotic"] = new Color(0.88f, 0.94f, 0.43f),
            ["mythic"] = new Color(1.00f, 0.43f, 0.86f),
            ["unique"] = new Color(1.00f, 0.75f, 0.25f)
        };

        // Persisted state (survives domain reload / play mode transitions)
        [SerializeField] private OutfitDefinition outfit = new();
        [SerializeField] private bool autoApply = true;
        [SerializeField] private bool applyOnPlay;
        [SerializeField] private int envIndex;
        [SerializeField] private int captureWidth = 2048;
        [SerializeField] private int captureHeight = 2048;
        [SerializeField] private int captureFrameRate = 30;
        [SerializeField] private bool transparentBackground = true;
        [SerializeField] private string outputFolder = OutfitCapture.DEFAULT_OUTPUT_FOLDER;
        [SerializeField] private float turntableDuration = 6f;
        [SerializeField] private float rotationSnapAngle;
        [SerializeField] private bool cleanGameView = true;

        // Browser state (session only)
        private readonly CatalogQuery _query = new();
        private CatalogItem[] _fetchedItems = Array.Empty<CatalogItem>(); // raw, unsorted, current filters
        private CatalogItem[] _sortedResults = Array.Empty<CatalogItem>(); // _fetchedItems, sorted for display
        private int _fetchedTotal; // server-reported total for the current filters (may exceed FETCH_CAP)
        private int _displayOffset; // position of the current page within _sortedResults
        private int _searchSequence;

        // urn -> catalog item, used to resolve slot/name/thumbnail for outfit rows
        private readonly Dictionary<string, CatalogItem> _knownItems = new();

        // Avatar tab: face-feature slot -> urn. Editor-only preview overrides — merged onto the
        // preview outfit in BuildPreviewOutfit(), never into outfit.urns, so they never reach a
        // share code or a saved preset.
        private readonly Dictionary<string, string> _previewFaceUrns = new();
        private EntityDefinition[] _faceEntities = Array.Empty<EntityDefinition>();
        private string _faceCategory = FACE_SLOTS[0];
        private int _faceSearchSequence;
        private VisualElement _faceGrid;
        private Button[] _faceCategoryButtons;

        private static readonly Dictionary<string, Texture2D> THUMBNAIL_CACHE = new();
        private static readonly HashSet<string> THUMBNAILS_IN_FLIGHT = new();

        // UI references
        private VisualElement _grid;
        private VisualElement _avatarPane;
        private VisualElement _browserContent;
        private VisualElement _debugPane;
        private TextField _configField;
        private Label _pageLabel;
        private Button _prevButton, _nextButton;
        private Button _invertSortButton;
        private bool _invertSort;
        private VisualElement _slotsContainer;
        private Label _poseLabel;
        private Label _rotationLabel;
        private PopupField<string> _emotePopup;
        private TextField _shareCodeField;
        private Label _statusLabel;
        private Button _playButton;
        private Button _videoButton;
        private Slider _emoteSlider;
        private PopupField<string> _bodyShapePopup;
        private ColorField _skinField, _hairField, _eyeField;
        private IVisualElementScheduledItem _pendingApply;

        public const string STUDIO_SCENE_PATH = "Assets/OutfitStudio/Scenes/OutfitStudio.unity";

        [MenuItem("Decentraland/Outfit Studio")]
        public static void Open()
        {
            var window = GetWindow<OutfitStudioWindow>("Outfit Studio");
            window.minSize = new Vector2(760, 480);
        }

        /// <summary>
        /// Opens the dedicated studio scene (a stripped copy of Main.unity with set dressing —
        /// see IMPLEMENTATION.md). The tool works in whichever scene is open; this is a shortcut.
        /// </summary>
        [MenuItem("Decentraland/Open Outfit Studio Scene")]
        public static void OpenStudioScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[OutfitStudio] Exit play mode before switching scenes");
                return;
            }

            if (!System.IO.File.Exists(STUDIO_SCENE_PATH))
            {
                Debug.LogError($"[OutfitStudio] Studio scene not found at {STUDIO_SCENE_PATH}");
                return;
            }

            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(STUDIO_SCENE_PATH);
            }
        }

        private void OnEnable()
        {
            APIService.Environment = envIndex == 1 ? "zone" : "org";
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode when applyOnPlay:
                    applyOnPlay = false;
                    // Give Bootstrap a moment to parse the debug config and kick off its initial load
                    rootVisualElement.schedule.Execute(Apply).StartingIn(1000);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    OutfitCapture.StopVideo();
                    UpdatePlayModeUI();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    UpdatePlayModeUI();
                    break;
            }
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            root.Add(BuildToolbar());

            var split = new TwoPaneSplitView(0, 380, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            split.Add(BuildBrowserPane());
            split.Add(BuildOutfitPane());
            root.Add(split);

            _statusLabel = new Label("Ready");
            _statusLabel.style.paddingLeft = 6;
            _statusLabel.style.paddingBottom = 2;
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(_statusLabel);

            HydrateKnownItems();
            RefreshSlots();
            RefreshShareCode();
            UpdatePlayModeUI();
            RunSearch();

            // PreviewController re-enables the overlay controls after every reload,
            // so Clean View re-enforces suppression on a cadence instead of one-shot
            root.schedule.Execute(EnforceCleanGameView).Every(500);
        }

        // ---------------------------------------------------------------- Game overlay suppression

        /// <summary>
        /// Hides the renderer's built-in play-mode overlay (debug panel, zoom, switcher, emote
        /// controls). The loader spinner and the drag surface (the Controls element itself,
        /// which carries the DragManipulator) are left untouched.
        /// </summary>
        private void EnforceCleanGameView()
        {
            if (!Application.isPlaying || !cleanGameView) return;

            var root = FindOverlayRoot();
            if (root == null) return;

            SetOverlayElementVisible(root, "DebugPanel", false);
            SetOverlayElementVisible(root, "ZoomControls", false);
            SetOverlayElementVisible(root, "Switcher", false);
            SetOverlayElementVisible(root, "EmoteControls", false);
        }

        private void RestoreGameOverlay()
        {
            if (!Application.isPlaying) return;

            var root = FindOverlayRoot();
            if (root == null) return;

            // Mirror the presenter: the debug panel is editor-only
            SetOverlayElementVisible(root, "DebugPanel", Application.isEditor);

            // Zoom/switcher/emote visibility is mode-dependent — a reload lets
            // PreviewController re-apply the canonical states
            SendToJSBridge("Reload", autoReload: false);
        }

        private static VisualElement FindOverlayRoot()
        {
            var presenter = FindAnyObjectByType<PreviewUIPresenter>();
            return presenter == null ? null : presenter.GetComponent<UIDocument>()?.rootVisualElement;
        }

        private static void SetOverlayElementVisible(VisualElement root, string name, bool visible)
        {
            var element = root.Q(name);
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------------------------------------------------------- Toolbar

        private VisualElement BuildToolbar()
        {
            var bar = new Toolbar();

            var envPopup = new PopupField<string>(new List<string> { "prod (org)", "dev (zone)" }, envIndex);
            envPopup.RegisterValueChangedCallback(_ =>
            {
                envIndex = envPopup.index;
                APIService.Environment = envIndex == 1 ? "zone" : "org";
                ResetAndSearch();
            });
            bar.Add(envPopup);

            bar.Add(new ToolbarSpacer { style = { flexGrow = 1 } });

            var cleanViewToggle = new ToolbarToggle { text = "Clean View", value = cleanGameView };
            cleanViewToggle.tooltip = "Hide the renderer's built-in overlay (debug panel, zoom, switcher) in play mode";
            cleanViewToggle.RegisterValueChangedCallback(evt =>
            {
                cleanGameView = evt.newValue;
                if (!cleanGameView) RestoreGameOverlay();
            });
            bar.Add(cleanViewToggle);

            var autoToggle = new ToolbarToggle { text = "Auto apply", value = autoApply };
            autoToggle.RegisterValueChangedCallback(evt => autoApply = evt.newValue);
            bar.Add(autoToggle);

            var applyButton = new ToolbarButton(Apply) { text = "Apply" };
            bar.Add(applyButton);

            var clearButton = new ToolbarButton(() =>
            {
                if (Application.isPlaying) return;
                EditModeAvatarPreview.Clear();
                SetStatus("Edit-mode preview cleared");
            }) { text = "Clear Preview" };
            bar.Add(clearButton);

            _playButton = new Button(EnterPlayAndApply) { text = "▶ Enter Play" };
            _playButton.style.marginLeft = 4;
            bar.Add(_playButton);

            return bar;
        }

        private void UpdatePlayModeUI()
        {
            if (_playButton == null) return;
            _playButton.style.display = Application.isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void EnterPlayAndApply()
        {
            if (Application.isPlaying) return;
            applyOnPlay = true;
            EditorApplication.EnterPlaymode();
        }

        // ---------------------------------------------------------------- Browser pane

        private VisualElement BuildBrowserPane()
        {
            var pane = new VisualElement { style = { minWidth = 300 } };

            // Tabs
            var tabs = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4, marginLeft = 4 } };
            var avatarTab = new Button { text = "Avatar" };
            var wearablesTab = new Button { text = "Wearables" };
            var emotesTab = new Button { text = "Emotes / Poses" };
            var debugTab = new Button { text = "Debug" };

            void SelectTab(string tab)
            {
                avatarTab.SetEnabled(tab != "avatar");
                wearablesTab.SetEnabled(tab != "wearable");
                emotesTab.SetEnabled(tab != "emote");
                debugTab.SetEnabled(tab != "debug");

                var isAvatar = tab == "avatar";
                var isDebug = tab == "debug";
                _avatarPane.style.display = isAvatar ? DisplayStyle.Flex : DisplayStyle.None;
                _browserContent.style.display = isAvatar || isDebug ? DisplayStyle.None : DisplayStyle.Flex;
                _debugPane.style.display = isDebug ? DisplayStyle.Flex : DisplayStyle.None;

                if (isAvatar || isDebug) return;

                _query.Category = tab;
                _query.WearableCategory = null;
                _query.EmoteCategory = null;
                ResetAndSearch();
            }

            avatarTab.clicked += () => SelectTab("avatar");
            wearablesTab.clicked += () => SelectTab("wearable");
            emotesTab.clicked += () => SelectTab("emote");
            debugTab.clicked += () => SelectTab("debug");
            wearablesTab.SetEnabled(false); // default active tab
            tabs.Add(avatarTab);
            tabs.Add(wearablesTab);
            tabs.Add(emotesTab);
            tabs.Add(debugTab);
            pane.Add(tabs);

            _avatarPane = BuildAvatarPane();
            _avatarPane.style.display = DisplayStyle.None;
            pane.Add(_avatarPane);

            _browserContent = new VisualElement { style = { flexGrow = 1 } };
            pane.Add(_browserContent);
            _debugPane = BuildDebugPane();
            _debugPane.style.display = DisplayStyle.None;
            pane.Add(_debugPane);

            // Search
            var search = new ToolbarSearchField { style = { marginLeft = 4, marginTop = 4, width = Length.Percent(95) } };
            IVisualElementScheduledItem pendingSearch = null;
            search.RegisterValueChangedCallback(evt =>
            {
                _query.Search = evt.newValue;
                pendingSearch?.Pause();
                pendingSearch = search.schedule.Execute(ResetAndSearch);
                pendingSearch.StartingIn(500);
            });
            _browserContent.Add(search);

            // Filters
            var filters = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginLeft = 4 } };

            var slotPopup = new PopupField<string>("Slot", WEARABLE_SLOTS, 0);
            slotPopup.RegisterValueChangedCallback(_ =>
            {
                if (_query.Category == "emote")
                    _query.EmoteCategory = slotPopup.value == "any" ? null : slotPopup.value;
                else
                    _query.WearableCategory = slotPopup.value == "any" ? null : slotPopup.value;
                ResetAndSearch();
            });
            filters.Add(slotPopup);

            var rarityPopup = new PopupField<string>("Rarity", RARITIES, 0);
            rarityPopup.RegisterValueChangedCallback(_ =>
            {
                _query.Rarity = rarityPopup.value == "any" ? null : rarityPopup.value;
                ResetAndSearch();
            });
            filters.Add(rarityPopup);

            var genderPopup = new PopupField<string>("Body", GENDERS, 0);
            genderPopup.RegisterValueChangedCallback(_ =>
            {
                _query.Gender = genderPopup.value == "any" ? null : genderPopup.value;
                ResetAndSearch();
            });
            filters.Add(genderPopup);

            var sortPopup = new PopupField<string>("Sort", SORT_OPTIONS, 0);
            sortPopup.RegisterValueChangedCallback(_ =>
            {
                _query.SortBy = SORT_API_VALUES[sortPopup.value];
                _displayOffset = 0;
                ApplySortAndRebuild(); // already have every matching item fetched; no need to re-query
            });
            filters.Add(sortPopup);

            _invertSortButton = new Button(() =>
            {
                _invertSort = !_invertSort;
                UpdateInvertSortButton();
                _displayOffset = 0;
                ApplySortAndRebuild();
            })
            {
                style = { width = 20, marginLeft = 2 }
            };
            UpdateInvertSortButton();
            filters.Add(_invertSortButton);

            // Swap slot filter choices when the tab changes
            wearablesTab.clicked += () => { slotPopup.choices = WEARABLE_SLOTS; slotPopup.index = 0; };
            emotesTab.clicked += () => { slotPopup.choices = EMOTE_CATEGORIES; slotPopup.index = 0; };

            _browserContent.Add(filters);

            // Results grid
            var scroll = new ScrollView { style = { flexGrow = 1 } };
            _grid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, paddingLeft = 4, paddingTop = 4 }
            };
            scroll.Add(_grid);
            _browserContent.Add(scroll);

            // Pagination
            var pager = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, paddingBottom = 4 } };
            // Paging is purely local: every matching item (up to FETCH_CAP) is already in memory.
            _prevButton = new Button(() => { _displayOffset = Mathf.Max(0, _displayOffset - PAGE_SIZE); RebuildGrid(); }) { text = "◀" };
            _nextButton = new Button(() => { _displayOffset += PAGE_SIZE; RebuildGrid(); }) { text = "▶" };
            _pageLabel = new Label("") { style = { unityTextAlign = TextAnchor.MiddleCenter, marginLeft = 8, marginRight = 8 } };
            pager.Add(_prevButton);
            pager.Add(_pageLabel);
            pager.Add(_nextButton);
            _browserContent.Add(pager);

            return pane;
        }

        // ---------------------------------------------------------------- Avatar pane

        /// <summary>
        /// Body shape, colors and face features (eyes/eyebrows/mouth/hair/facial_hair) in one
        /// place, mirroring the marketplace's own avatar editor. Body shape and colors write straight
        /// to <c>outfit</c> (shareable, same as before — just relocated here from the Outfit pane).
        /// Face features are deliberately NOT part of <c>outfit</c>: they're stored in
        /// <see cref="_previewFaceUrns"/> and only ever merged in for local preview/capture (see
        /// <see cref="BuildPreviewOutfit"/>), so a share code or saved preset never carries them.
        /// </summary>
        private VisualElement BuildAvatarPane()
        {
            var pane = new ScrollView { style = { flexGrow = 1, paddingLeft = 6, paddingRight = 6, paddingTop = 4 } };

            pane.Add(Header("Body"));

            _bodyShapePopup = new PopupField<string>("Body shape", new List<string> { "Male", "Female" },
                outfit.bodyShape == WearablesConstants.BODY_SHAPE_FEMALE ? 1 : 0);
            _bodyShapePopup.RegisterValueChangedCallback(_ =>
            {
                outfit.bodyShape = _bodyShapePopup.index == 1
                    ? WearablesConstants.BODY_SHAPE_FEMALE
                    : WearablesConstants.BODY_SHAPE_MALE;
                RefreshFaceGrid(); // face options are body-shape specific (male/female variants)
                RefreshShareCode();
                ScheduleApply();
            });
            pane.Add(_bodyShapePopup);

            pane.Add(Header("Colors"));
            _skinField = ColorRow(pane, "Skin", outfit.skinColor, c => outfit.skinColor = c);
            _hairField = ColorRow(pane, "Hair", outfit.hairColor, c => outfit.hairColor = c);
            _eyeField = ColorRow(pane, "Eyes", outfit.eyeColor, c => outfit.eyeColor = c);

            pane.Add(Header("Face Features"));
            pane.Add(new Label("Preview only — not included in the share code or outfit preset.")
            {
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Italic,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 4
                }
            });

            var categoryRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            _faceCategoryButtons = new Button[FACE_SLOTS.Count];
            for (var i = 0; i < FACE_SLOTS.Count; i++)
            {
                var slot = FACE_SLOTS[i];
                var button = new Button(() => SelectFaceCategory(slot))
                {
                    text = FACE_SLOT_LABELS[slot],
                    style = { marginRight = 2, marginBottom = 2 }
                };
                _faceCategoryButtons[i] = button;
                categoryRow.Add(button);
            }
            pane.Add(categoryRow);

            pane.Add(new Button(() =>
            {
                _previewFaceUrns.Remove(_faceCategory);
                RefreshFaceGrid();
                ScheduleApply();
            }) { text = "Clear selection", style = { marginTop = 2, marginBottom = 4 } });

            // No nested ScrollView here: the pane itself already scrolls, and a ScrollView inside a
            // ScrollView left the outer one unable to size to its content, clipping the bottom of the
            // panel instead of scrolling to it.
            _faceGrid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, paddingTop = 4 }
            };
            pane.Add(_faceGrid);

            UpdateFaceCategoryButtons();
            RunFaceSearch();

            return pane;
        }

        private void SelectFaceCategory(string slot)
        {
            _faceCategory = slot;
            UpdateFaceCategoryButtons();
            RunFaceSearch();
        }

        private void UpdateFaceCategoryButtons()
        {
            for (var i = 0; i < FACE_SLOTS.Count; i++)
                _faceCategoryButtons[i].SetEnabled(FACE_SLOTS[i] != _faceCategory);
        }

        /// <summary>
        /// Resolves the current category's curated URNs via the Catalyst entities endpoint (the
        /// only source that can serve off-chain base-avatar items — see DEFAULT_FACE_URNS). Async
        /// void, same pattern EditModeAvatarPreview.Apply already uses for editor-only await chains.
        /// </summary>
        private async void RunFaceSearch()
        {
            if (_faceGrid == null) return;

            var sequence = ++_faceSearchSequence;
            SetStatus($"Loading {FACE_SLOT_LABELS[_faceCategory]}...");

            EntityDefinition[] entities;
            try
            {
                entities = await EntityService.GetEntities((string[])DEFAULT_FACE_URNS[_faceCategory].Clone());
            }
            catch (Exception e)
            {
                if (sequence == _faceSearchSequence)
                    SetStatus($"Failed to load {FACE_SLOT_LABELS[_faceCategory]}: {e.Message}", true);
                return;
            }

            if (sequence != _faceSearchSequence) return;

            _faceEntities = entities;
            RefreshFaceGrid();
            SetStatus($"{_faceEntities.Length} {FACE_SLOT_LABELS[_faceCategory]} options");
        }

        private BodyShape CurrentBodyShape() =>
            outfit.bodyShape.Equals(WearablesConstants.BODY_SHAPE_FEMALE, StringComparison.OrdinalIgnoreCase)
                ? BodyShape.Female
                : BodyShape.Male;

        private void RefreshFaceGrid()
        {
            if (_faceGrid == null) return;

            _faceGrid.Clear();

            var bodyShape = CurrentBodyShape();
            var selectedUrn = _previewFaceUrns.GetValueOrDefault(_faceCategory);

            // Only options with a representation for the currently-selected body shape are shown —
            // this list mixes male and female-specific variants (the "f_"-prefixed URNs), and picking
            // one without a matching representation would just get silently skipped at apply time.
            foreach (var entity in _faceEntities.Where(e => e.HasRepresentation(bodyShape)))
            {
                _faceGrid.Add(BuildFaceTile(entity, entity.URN == selectedUrn));
            }
        }

        private VisualElement BuildFaceTile(EntityDefinition entity, bool selected)
        {
            var label = FriendlyName(entity.URN);

            var tile = new VisualElement
            {
                tooltip = label,
                style =
                {
                    width = THUMB_SIZE + 8,
                    marginRight = 4,
                    marginBottom = 4,
                    paddingTop = 4,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingBottom = 2,
                    backgroundColor = new Color(0, 0, 0, 0.25f)
                }
            };

            if (selected)
            {
                tile.style.borderTopWidth = tile.style.borderBottomWidth =
                    tile.style.borderLeftWidth = tile.style.borderRightWidth = 2;
                tile.style.borderTopColor = tile.style.borderBottomColor =
                    tile.style.borderLeftColor = tile.style.borderRightColor = Color.white;
            }

            var image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = THUMB_SIZE, height = THUMB_SIZE }
            };
            tile.Add(image);

            var nameLabel = new Label(label)
            {
                style =
                {
                    fontSize = 10,
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            tile.Add(nameLabel);

            // Entities don't carry a display name (unlike marketplace CatalogItems) — only a
            // thumbnail, same as WearableItemElement (the in-game Configurator's own tile).
            LoadThumbnail(entity.Thumbnail, tex =>
            {
                if (tex != null) image.image = tex;
            });

            tile.RegisterCallback<ClickEvent>(_ => OnFaceFeatureClicked(entity));

            return tile;
        }

        private static string FriendlyName(string urn)
        {
            var suffix = urn[(urn.LastIndexOf(':') + 1)..];
            return string.Join(' ', suffix.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        }

        private void OnFaceFeatureClicked(EntityDefinition entity)
        {
            _previewFaceUrns[_faceCategory] = entity.URN;
            RefreshFaceGrid();
            ScheduleApply();
            SetStatus($"Preview: {FriendlyName(entity.URN)} ({FACE_SLOT_LABELS[_faceCategory]}) — editor-only, not shared");
        }

        /// <summary>
        /// The outfit actually rendered for preview/capture: <c>outfit</c> plus any local face-feature
        /// overrides, with a conflicting real outfit item in the same slot dropped (one item per slot,
        /// same rule <see cref="OnItemClicked"/> already applies for ordinary equips). Returns
        /// <c>outfit</c> itself untouched when there's nothing to merge, so the common case allocates
        /// nothing.
        /// </summary>
        private OutfitDefinition BuildPreviewOutfit()
        {
            if (_previewFaceUrns.Count == 0) return outfit;

            var preview = outfit.Clone();
            var overriddenSlots = _previewFaceUrns.Keys.ToHashSet();
            preview.urns.RemoveAll(urn =>
                _knownItems.TryGetValue(urn, out var known) && overriddenSlots.Contains(known.Slot));
            preview.urns.AddRange(_previewFaceUrns.Values);
            return preview;
        }

        /// <summary>
        /// Replicates the renderer's built-in play-mode debug overlay (PreviewUIPresenter's
        /// DebugPanel) so it can live in the window instead of covering the Game view.
        /// </summary>
        private VisualElement BuildDebugPane()
        {
            var pane = new ScrollView { style = { flexGrow = 1, paddingLeft = 6, paddingRight = 6, paddingTop = 4 } };

            pane.Add(new Label($"Renderer version: {Application.version}")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 }
            });

            pane.Add(new Label("These actions drive the play-mode renderer via JSBridge — enter play mode to use them.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6 }
            });

            // --- JSBridge invoke (mirrors MethodNameDropdown/Parameter/InvokeButton)
            pane.Add(Header("Invoke JSBridge method"));

            var methodNames = typeof(JSBridge)
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name)
                .ToList();

            var methodPopup = new PopupField<string>("Method", methodNames, 0);
            pane.Add(methodPopup);

            var parameterField = new TextField("Parameter");
            pane.Add(parameterField);

            pane.Add(new Button(() =>
            {
                SendToJSBridge(methodPopup.value, parameterField.value);
                SetStatus($"Invoked {methodPopup.value}");
            }) { text = "Invoke" });

            // --- URL presets (mirrors URLDropdown; same list, hoisted to a shared static)
            pane.Add(Header("Load from URL preset"));

            var presets = PreviewUIPresenter.DEBUG_URL_PRESETS;
            var presetPopup = new PopupField<string>(presets.Select(p => p.name).ToList(), 0);
            presetPopup.RegisterValueChangedCallback(evt =>
            {
                var selected = presets.Find(p => p.name == evt.newValue);
                if (selected.url == null) return;

                SendToJSBridge("ParseFromString", selected.url);
                SetStatus($"Loaded preset: {selected.name}");
            });
            pane.Add(presetPopup);

            // --- Outline debug. SMAA erodes the outline's thin stroke toward whatever's behind it, so
            // it picks up the card background. Outline width/color are shader-tuning knobs (Capture
            // pane); widen the stroke there so it survives AA. This selector A/Bs the camera AA live.
            pane.Add(Header("Outline Debug"));
            pane.Add(new Label("Antialiasing override — play mode only, not persisted. " +
                               "Outline width and color are under Shader Tuning.")
            {
                style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Italic, whiteSpace = WhiteSpace.Normal, marginBottom = 4 }
            });

            var aaOptions = new List<string> { "Scene Default", "None", "FXAA", "SMAA", "TAA" };
            var aaPopup = new PopupField<string>("Anti-aliasing", aaOptions, 0)
            {
                tooltip = "Scene Default is SMAA — the likely source of the outline being tinted by the card background."
            };
            aaPopup.RegisterValueChangedCallback(evt =>
            {
                StudioCardFrame.DebugAntialiasing = evt.newValue switch
                {
                    "None" => AntialiasingMode.None,
                    "FXAA" => AntialiasingMode.FastApproximateAntialiasing,
                    "SMAA" => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    "TAA" => AntialiasingMode.TemporalAntiAliasing,
                    _ => (AntialiasingMode?)null
                };
                SetStatus(Application.isPlaying
                    ? $"Antialiasing set to {evt.newValue}"
                    : "Antialiasing override will apply once you're in play mode", false);
            });
            pane.Add(aaPopup);

            // --- Misc debug actions
            pane.Add(Header("Actions"));

            var actionsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };

            actionsRow.Add(new Button(() =>
            {
                var config = AangConfiguration.Instance.ToString();
                Debug.Log(config);
                _configField.value = config;
            }) { text = "Print Config" });

            actionsRow.Add(new Button(() =>
            {
                SendToJSBridge("SetProfile", $"default{UnityEngine.Random.Range(1, 160)}");
                SetStatus("Loading random profile...");
            }) { text = "Random Profile" });

            actionsRow.Add(new Button(() => WithCamera(c => c.ZoomIn())) { text = "Zoom In" });
            actionsRow.Add(new Button(() => WithCamera(c => c.ZoomOut())) { text = "Zoom Out" });

            pane.Add(actionsRow);

            _configField = new TextField { multiline = true, isReadOnly = true };
            _configField.style.whiteSpace = WhiteSpace.Normal;
            _configField.style.marginTop = 4;
            pane.Add(_configField);

            // --- Load from Collection (draft UUID via signed builder-api, or published 0x contract)
            pane.Add(Header("Load from Collection"));

            _identityStatusLabel = new Label { style = { whiteSpace = WhiteSpace.Normal } };
            pane.Add(_identityStatusLabel);

            var identityField = new TextField("Identity JSON") { isPasswordField = true };
            identityField.tooltip = "Paste your Decentraland identity from builder.decentraland.org " +
                                    "(devtools > Application > Local Storage). Stored in EditorPrefs only.";
            pane.Add(identityField);

            var identityButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            identityButtons.Add(new Button(() =>
            {
                try
                {
                    _identity = BuilderIdentity.Parse(identityField.value);
                    _identity.Save();
                    identityField.value = string.Empty;
                    RefreshIdentityStatus();
                    SetStatus("Identity saved");
                }
                catch (Exception e)
                {
                    SetStatus($"Invalid identity: {e.Message}", true);
                }
            }) { text = "Save Identity" });
            identityButtons.Add(new Button(() =>
            {
                BuilderIdentity.Clear();
                _identity = null;
                RefreshIdentityStatus();
                SetStatus("Identity cleared");
            }) { text = "Clear" });
            pane.Add(identityButtons);

            var collectionRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            _collectionIdField = new TextField("Collection ID") { style = { flexGrow = 1 } };
            _collectionIdField.tooltip = "Draft collection UUID (needs identity) or published 0x contract address";
            collectionRow.Add(_collectionIdField);
            collectionRow.Add(new Button(LoadCollection) { text = "Load" });
            pane.Add(collectionRow);

            _collectionGrid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 4 }
            };
            pane.Add(_collectionGrid);

            var collectionPager = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, marginTop = 2 }
            };
            _collectionPrevButton = new Button(() => { _collectionSkip = Mathf.Max(0, _collectionSkip - PAGE_SIZE); ShowCollectionPage(); }) { text = "◀" };
            _collectionNextButton = new Button(() => { _collectionSkip += PAGE_SIZE; ShowCollectionPage(); }) { text = "▶" };
            _collectionPageLabel = new Label("") { style = { unityTextAlign = TextAnchor.MiddleCenter, marginLeft = 8, marginRight = 8 } };
            collectionPager.Add(_collectionPrevButton);
            collectionPager.Add(_collectionPageLabel);
            collectionPager.Add(_collectionNextButton);
            collectionPager.style.display = DisplayStyle.None;
            _collectionPager = collectionPager;
            pane.Add(collectionPager);

            RefreshIdentityStatus();

            return pane;
        }

        // ---------------------------------------------------------------- Load from Collection

        private BuilderIdentity _identity;
        private Label _identityStatusLabel;
        private TextField _collectionIdField;
        private VisualElement _collectionGrid;
        private VisualElement _collectionPager;
        private Button _collectionPrevButton, _collectionNextButton;
        private Label _collectionPageLabel;
        private List<BuilderCollectionService.DraftItem> _draftItems;
        private int _collectionSkip;

        private void RefreshIdentityStatus()
        {
            _identity ??= BuilderIdentity.Load();

            if (_identityStatusLabel == null) return;

            if (_identity == null)
            {
                _identityStatusLabel.text = "No identity saved — needed for draft (UUID) collections only.";
            }
            else
            {
                var state = _identity.IsExpired ? "EXPIRED" : "valid";
                _identityStatusLabel.text = $"Identity: {_identity.WalletAddress} — {state} until {_identity.Expiration:yyyy-MM-dd}";
            }
        }

        private void LoadCollection()
        {
            var id = _collectionIdField.value?.Trim();

            if (string.IsNullOrEmpty(id))
            {
                SetStatus("Enter a collection ID", true);
                return;
            }

            SetStatus("Loading collection...");
            _draftItems = null;
            _collectionSkip = 0;

            if (id.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // Published collection: unauthenticated marketplace catalog, server-paged
                LoadPublishedCollectionPage(id);
            }
            else
            {
                RefreshIdentityStatus();
                BuilderCollectionService.LoadDraftCollection(id, _identity,
                    items =>
                    {
                        _draftItems = items;
                        ShowCollectionPage();
                        SetStatus($"Collection loaded: {items.Count} items");
                    },
                    error => SetStatus(error, true));
            }
        }

        private void LoadPublishedCollectionPage(string contractAddress)
        {
            var query = new CatalogQuery
            {
                ContractAddress = contractAddress,
                Category = null, // collections can mix wearables and emotes
                First = PAGE_SIZE,
                Skip = _collectionSkip
            };

            CatalogService.Search(query,
                page =>
                {
                    _collectionGrid.Clear();
                    foreach (var item in page.data)
                    {
                        _collectionGrid.Add(BuildTile(item, OnItemClicked)); // published items equip via the normal URN flow
                    }

                    var from = _collectionSkip + 1;
                    var to = _collectionSkip + page.data.Length;
                    _collectionPageLabel.text = page.total > 0 ? $"{from}–{to} of {page.total}" : "no items";
                    _collectionPrevButton.SetEnabled(_collectionSkip > 0);
                    _collectionNextButton.SetEnabled(to < page.total);
                    _collectionPager.style.display = DisplayStyle.Flex;
                    SetStatus($"Collection loaded: {page.total} items");
                },
                error => SetStatus($"Catalog error: {error}", true));
        }

        private void ShowCollectionPage()
        {
            // Published (0x) collections page server-side
            if (_draftItems == null)
            {
                var id = _collectionIdField.value?.Trim();
                if (!string.IsNullOrEmpty(id) && id.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    LoadPublishedCollectionPage(id);
                return;
            }

            _collectionGrid.Clear();

            foreach (var item in _draftItems.Skip(_collectionSkip).Take(PAGE_SIZE))
            {
                _collectionGrid.Add(BuildDraftTile(item));
            }

            var from = _collectionSkip + 1;
            var to = Mathf.Min(_collectionSkip + PAGE_SIZE, _draftItems.Count);
            _collectionPageLabel.text = _draftItems.Count > 0 ? $"{from}–{to} of {_draftItems.Count}" : "no items";
            _collectionPrevButton.SetEnabled(_collectionSkip > 0);
            _collectionNextButton.SetEnabled(to < _draftItems.Count);
            _collectionPager.style.display = DisplayStyle.Flex;
        }

        private VisualElement BuildDraftTile(BuilderCollectionService.DraftItem item)
        {
            var tile = new VisualElement
            {
                tooltip = $"{item.Name}\n{item.Rarity} · {item.Category} · {item.Type} (draft)",
                style =
                {
                    width = THUMB_SIZE + 8,
                    marginRight = 4,
                    marginBottom = 4,
                    paddingTop = 4,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingBottom = 2,
                    backgroundColor = new Color(0, 0, 0, 0.25f),
                    borderBottomWidth = 3,
                    borderBottomColor = RARITY_COLORS.GetValueOrDefault(item.Rarity ?? "", Color.gray)
                }
            };

            var image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = THUMB_SIZE, height = THUMB_SIZE }
            };
            tile.Add(image);

            tile.Add(new Label(item.Name)
            {
                style =
                {
                    fontSize = 10,
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            });

            LoadThumbnail(item.ThumbnailUrl, tex =>
            {
                if (tex != null) image.image = tex;
            });

            tile.RegisterCallback<ClickEvent>(_ => EquipDraft(item));

            return tile;
        }

        private void EquipDraft(BuilderCollectionService.DraftItem item)
        {
            if (item.Type == "emote")
            {
                RemoveDraftEmote();
                outfit.base64Items.Add(item.Base64Entity);
                outfit.emote = "idle"; // the base64 emote takes pose priority in builder mode
                _poseLabel.text = $"Pose: {item.Name} (draft)";
                SyncEmotePopup();
                SetStatus($"Pose set: {item.Name} (draft, play mode only)");
            }
            else
            {
                // One item per slot: displace both draft and catalog occupants of this category
                outfit.base64Items.RemoveAll(base64 =>
                {
                    var (_, category, isEmote) = DescribeDraft(base64);
                    return !isEmote && category == item.Category;
                });
                outfit.urns.RemoveAll(urn =>
                    _knownItems.TryGetValue(urn, out var known) && known.Slot == item.Category);

                outfit.base64Items.Add(item.Base64Entity);
                SetStatus($"Equipped {item.Name} ({item.Category}, draft)");
                RefreshSlots();
            }

            RefreshShareCode();
            ScheduleApply();
        }

        /// <summary>
        /// Same invocation contract as the overlay's Invoke button: SendMessage to the JSBridge
        /// GameObject, then auto-Reload unless the method manages loading itself.
        /// </summary>
        private void SendToJSBridge(string method, string parameter = null, bool autoReload = true)
        {
            if (!Application.isPlaying)
            {
                SetStatus("Enter play mode first", true);
                return;
            }

            var bridge = GameObject.Find("JSBridge");
            if (bridge == null)
            {
                SetStatus("JSBridge object not found in the scene", true);
                return;
            }

            if (string.IsNullOrEmpty(parameter))
                bridge.SendMessage(method);
            else
                bridge.SendMessage(method, parameter);

            if (autoReload && method != "Reload" && method != "TakeScreenshot" && method != "Cleanup")
            {
                bridge.SendMessage("Reload");
            }
        }

        private void WithCamera(Action<PreviewCameraController> action)
        {
            if (!Application.isPlaying)
            {
                SetStatus("Enter play mode first", true);
                return;
            }

            var cameraController = FindAnyObjectByType<PreviewCameraController>();
            if (cameraController != null) action(cameraController);
        }

        private void ResetAndSearch()
        {
            _displayOffset = 0;
            RunSearch();
        }

        private void RunSearch()
        {
            if (_grid == null) return;

            SetStatus("Searching catalog...");

            var sequence = ++_searchSequence; // guard against out-of-order responses

            void OnError(string error)
            {
                if (sequence != _searchSequence) return;
                SetStatus($"Catalog error: {error}", true);
            }

            CatalogService.SearchAll(_query, FETCH_CAP,
                (items, total) =>
                {
                    if (sequence != _searchSequence) return;

                    if (string.IsNullOrEmpty(_query.Search))
                    {
                        _fetchedItems = items;
                        _fetchedTotal = total;
                        ApplySortAndRebuild();
                        return;
                    }

                    AugmentWithTagMatches(items, sequence, OnError);
                },
                OnError);
        }

        /// <summary>
        /// marketplace-api's own <c>search</c> param (already applied by the caller's CatalogQuery)
        /// only matches item name/description, with no concept of tags - so a query like "jacket"
        /// misses an item named "Black Jacket" that's tagged "Jacket" but doesn't say so in its name.
        /// The catalyst lambdas endpoint (<see cref="CatalystTextSearchService"/>) indexes tags, so
        /// it's used here purely to find items marketplace-api's name search missed. Those extra
        /// items are built directly from the lambdas payload (name/thumbnail/rarity/slot/bodyShapes)
        /// rather than hydrated through marketplace-api's URN lookup - that lookup only resolves
        /// collections-v2 (Polygon) URNs and silently returns nothing for legacy collections-v1
        /// (Ethereum) items, which are exactly the kind of older item this tag search tends to
        /// surface. The current slot/rarity/gender filters are re-applied to the extras client-side,
        /// since they only ever went through the lambdas query, not marketplace-api's own filtering.
        /// </summary>
        private void AugmentWithTagMatches(CatalogItem[] nameMatches, int sequence, Action<string> onError)
        {
            CatalystTextSearchService.SearchItems(_query.Category, _query.Search, TAG_SEARCH_CAP,
                tagMatches =>
                {
                    if (sequence != _searchSequence) return;

                    var knownUrns = nameMatches.Select(i => i.urn).ToHashSet();
                    var extras = tagMatches.Where(i => !knownUrns.Contains(i.urn) && MatchesActiveFilters(i));

                    var merged = nameMatches.Concat(extras).ToArray();
                    _fetchedItems = merged;
                    _fetchedTotal = merged.Length;
                    ApplySortAndRebuild();
                },
                onError);
        }

        /// <summary>
        /// Re-applies the slot/rarity/gender filters an ordinary marketplace-api browse would already
        /// have enforced server-side (see CatalogService.BuildUrl) - needed only for tag-matched items
        /// built from the lambdas payload, which was never filtered by any of these. Gender is
        /// approximated from bodyShapes (matches the live API's own observed behavior: "male"/"female"
        /// match items serving that shape at all, "unisex" requires both).
        /// </summary>
        private bool MatchesActiveFilters(CatalogItem item)
        {
            var wearableSlot = _query.Category == "emote" ? _query.EmoteCategory : _query.WearableCategory;
            if (!string.IsNullOrEmpty(wearableSlot) && item.Slot != wearableSlot) return false;

            if (!string.IsNullOrEmpty(_query.Rarity) && item.rarity != _query.Rarity) return false;

            if (!string.IsNullOrEmpty(_query.Gender))
            {
                var bodyShapes = item.data?.wearable?.bodyShapes ?? item.data?.emote?.bodyShapes;
                var hasMale = bodyShapes?.Contains("BaseMale") ?? false;
                var hasFemale = bodyShapes?.Contains("BaseFemale") ?? false;
                var matchesGender = _query.Gender switch
                {
                    "male" => hasMale,
                    "female" => hasFemale,
                    "unisex" => hasMale && hasFemale,
                    _ => true
                };
                if (!matchesGender) return false;
            }

            return true;
        }

        private void UpdateInvertSortButton()
        {
            _invertSortButton.text = _invertSort ? "↑" : "↓";
            _invertSortButton.tooltip = _invertSort
                ? "Sort direction inverted (e.g. Newest shows oldest first) - click to restore"
                : "Click to invert sort direction (e.g. Newest → oldest first)";
        }

        private void ApplySortAndRebuild()
        {
            _sortedResults = SortForDisplay(_fetchedItems, _query.SortBy, _invertSort).ToArray();
            RebuildGrid();

            var capped = _fetchedTotal > _sortedResults.Length;
            SetStatus(capped
                ? $"{_sortedResults.Length} of {_fetchedTotal} items (sort limited to the first {FETCH_CAP})"
                : $"{_sortedResults.Length} items");
        }

        private void RebuildGrid()
        {
            _grid.Clear();

            foreach (var item in _sortedResults.Skip(_displayOffset).Take(PAGE_SIZE))
            {
                _grid.Add(BuildTile(item, OnItemClicked));
            }

            var shown = Mathf.Clamp(_sortedResults.Length - _displayOffset, 0, PAGE_SIZE);
            var from = shown > 0 ? _displayOffset + 1 : 0;
            var to = _displayOffset + shown;
            _pageLabel.text = _sortedResults.Length > 0 ? $"{from}–{to} of {_sortedResults.Length}" : "no results";
            _prevButton.SetEnabled(_displayOffset > 0);
            _nextButton.SetEnabled(to < _sortedResults.Length);
        }

        /// <summary>
        /// The live marketplace-api ignores <c>sortBy</c> entirely (verified: newest, recently_listed,
        /// recently_sold, cheapest and most_expensive all return items in the exact same server order,
        /// prices/dates included) — so this sorts client-side instead, over every item matching the
        /// current filters (fetched up to FETCH_CAP by <see cref="RunSearch"/>), not just one page.
        /// Values match the real marketplace sortBy enum; "name" is local-only.
        ///
        /// <paramref name="invert"/> flips the natural direction of whichever option is selected (e.g.
        /// "Newest" + invert shows the oldest items first) — the only way to reach the tail of a sort,
        /// since the marketplace itself has no "oldest"/"least expensive"-style option of its own.
        /// Items lacking the relevant value (not on sale, never sold) always trail last regardless of
        /// direction, so inverting "Cheapest" doesn't flood the top with unpriced items.
        /// </summary>
        private static IEnumerable<CatalogItem> SortForDisplay(CatalogItem[] items, string sortBy, bool invert) =>
            sortBy switch
            {
                "name" => invert
                    ? items.OrderByDescending(i => i.name, StringComparer.OrdinalIgnoreCase)
                    : items.OrderBy(i => i.name, StringComparer.OrdinalIgnoreCase),
                "cheapest" => OrderByPrice(items, descending: invert),
                "most_expensive" => OrderByPrice(items, descending: !invert),
                "recently_listed" => OrderByTimestamp(items, i => i.updatedAt, descending: !invert),
                "recently_sold" => OrderByTimestamp(items, i => i.soldAt, descending: !invert),
                _ => OrderByTimestamp(items, i => i.createdAt, descending: !invert), // "newest"
            };

        private static IEnumerable<CatalogItem> OrderByPrice(CatalogItem[] items, bool descending)
        {
            var onSale = items.Where(i => i.isOnSale);
            var priced = descending
                ? onSale.OrderByDescending(i => double.TryParse(i.price, out var price) ? price : 0)
                : onSale.OrderBy(i => double.TryParse(i.price, out var price) ? price : 0);
            return priced.Concat(items.Where(i => !i.isOnSale));
        }

        private static IEnumerable<CatalogItem> OrderByTimestamp(CatalogItem[] items,
            Func<CatalogItem, string> selector, bool descending)
        {
            bool HasValue(CatalogItem i) => long.TryParse(selector(i), out _);
            var withValue = items.Where(HasValue);
            var ordered = descending
                ? withValue.OrderByDescending(i => long.Parse(selector(i)))
                : withValue.OrderBy(i => long.Parse(selector(i)));
            return ordered.Concat(items.Where(i => !HasValue(i)));
        }

        private VisualElement BuildTile(CatalogItem item, Action<CatalogItem> onClick)
        {
            var tile = new VisualElement
            {
                tooltip = $"{item.name}\n{item.rarity} · {item.Slot}",
                style =
                {
                    width = THUMB_SIZE + 8,
                    marginRight = 4,
                    marginBottom = 4,
                    paddingTop = 4,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingBottom = 2,
                    backgroundColor = new Color(0, 0, 0, 0.25f),
                    borderBottomWidth = 3,
                    borderBottomColor = RARITY_COLORS.GetValueOrDefault(item.rarity ?? "", Color.gray)
                }
            };

            var image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = THUMB_SIZE, height = THUMB_SIZE }
            };
            tile.Add(image);

            var label = new Label(item.name)
            {
                style =
                {
                    fontSize = 10,
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            tile.Add(label);

            // No panel-attachment check: cached textures invoke the callback synchronously,
            // before the tile is added to the grid, and setting .image while detached is fine
            LoadThumbnail(item.thumbnail, tex =>
            {
                if (tex != null) image.image = tex;
            });

            tile.RegisterCallback<ClickEvent>(_ => onClick(item));

            return tile;
        }

        private static void LoadThumbnail(string url, Action<Texture2D> callback)
        {
            if (string.IsNullOrEmpty(url))
            {
                callback(null);
                return;
            }

            if (THUMBNAIL_CACHE.TryGetValue(url, out var cached))
            {
                // Unity-null means the texture was destroyed since caching — re-download
                if (cached != null)
                {
                    callback(cached);
                    return;
                }

                THUMBNAIL_CACHE.Remove(url);
            }

            var request = UnityWebRequestTexture.GetTexture(url);
            var operation = request.SendWebRequest();
            THUMBNAILS_IN_FLIGHT.Add(url);

            operation.completed += _ =>
            {
                THUMBNAILS_IN_FLIGHT.Remove(url);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    THUMBNAIL_CACHE[url] = texture;
                    callback(texture);
                }
                else
                {
                    callback(null);
                }

                request.Dispose();
            };
        }

        private void OnItemClicked(CatalogItem item)
        {
            _knownItems[item.urn] = item;

            if (item.category == "emote")
            {
                outfit.emote = item.urn;
                RemoveDraftEmote();
                _poseLabel.text = $"Pose: {item.name}";
                SyncEmotePopup();
                RefreshShareCode();
                SetStatus($"Pose set: {item.name}");

                // Play mode: animate ONLY the currently-loaded avatar (which may be a Random
                // Profile from the Debug tab), same as the pose buttons / Embedded popup — don't
                // force a reload of the custom Builder outfit just to change the emote. Edit mode
                // still routes through the full Apply.
                if (Application.isPlaying)
                    ApplyPoseOnly(outfit.emote);
                else
                    ScheduleApply();
                return;
            }

            var slot = item.Slot;

            // One wearable per slot: drop anything we know occupies the same category
            outfit.urns.RemoveAll(urn =>
                _knownItems.TryGetValue(urn, out var known) && known.Slot == slot);
            outfit.urns.Remove(item.urn);
            outfit.urns.Add(item.urn);

            SetStatus($"Equipped {item.name} ({slot})");
            RefreshSlots();

            RefreshShareCode();
            ScheduleApply();
        }

        // ---------------------------------------------------------------- Outfit pane

        private VisualElement BuildOutfitPane()
        {
            var pane = new ScrollView { style = { paddingLeft = 6, paddingRight = 6, paddingTop = 4 } };

            // --- Shader (selection persists via StudioAvatarShaderSwitcher and re-applies after
            // every avatar reload, edit and play mode, until another shader is picked). The 3 selector
            // buttons stay visible for quick access; only the tuning panel is tucked into a
            // collapsible "Shader Settings" foldout (matching the Card frame section below).
            pane.Add(Header("Shader"));

            var shaderRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var shaderButtons = new Button[3];
            var shaderLabels = new[] { "DCL_Toon", "DCL_Toon_Studio", "DCL_Stylized_PBR" };

            // Tuning panel (rebuilt per selected shader; empty for the stock DCL_Toon)
            var shaderTuning = new VisualElement();

            void RefreshShaderButtons()
            {
                var current = (int)StudioAvatarShaderSwitcher.Mode;
                for (var i = 0; i < shaderButtons.Length; i++)
                    shaderButtons[i].SetEnabled(i != current); // disabled = selected, same as the tabs
            }

            for (var i = 0; i < shaderButtons.Length; i++)
            {
                var mode = (StudioShaderMode)i;
                shaderButtons[i] = new Button(() =>
                {
                    StudioAvatarShaderSwitcher.Mode = mode;
                    RefreshShaderButtons();
                    BuildShaderTuning(shaderTuning);
                }) { text = shaderLabels[i], style = { flexGrow = 1 } };
                shaderRow.Add(shaderButtons[i]);
            }

            RefreshShaderButtons();
            pane.Add(shaderRow);

            var shaderFold = new Foldout { text = "Shader Settings", value = false, style = { marginTop = 4 } };
            BuildShaderTuning(shaderTuning);
            shaderFold.Add(shaderTuning);
            pane.Add(shaderFold);

            // --- Card Frame (Fortnite-style item-card composite; studio-scene only, captured for free)
            BuildCardFrame(pane);

            // --- Outfit (body shape and colors live on the Avatar tab now)
            pane.Add(Header("Outfit"));

            _slotsContainer = new VisualElement();
            pane.Add(_slotsContainer);

            // --- Pose
            pane.Add(Header("Pose"));

            _poseLabel = new Label($"Pose: {outfit.emote}");
            pane.Add(_poseLabel);

            // Quick-pose buttons — one per single-frame GLB in StreamingAssets/poses/, auto-discovered.
            var poseGrid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 2, marginBottom = 2 }
            };
            BuildPoseButtons(poseGrid);
            pane.Add(poseGrid);

            _emotePopup = new PopupField<string>("Embedded", EMBEDDED_EMOTE_CHOICES, 0);
            SyncEmotePopup();
            _emotePopup.RegisterValueChangedCallback(_ =>
            {
                // Selecting the sentinel isn't a real emote choice; only "idle" would land back
                // here anyway, so just treat it the same way.
                outfit.emote = _emotePopup.value == EMBEDDED_EMOTE_NONE ? "idle" : _emotePopup.value;
                RemoveDraftEmote(); // an equipped draft emote would override the pose
                _poseLabel.text = $"Pose: {outfit.emote}";
                RefreshShareCode();

                // Play mode: animate ONLY the currently-loaded avatar (which may be a Random
                // Profile from the Debug tab), same as the pose buttons — don't force a reload of
                // the custom Builder outfit just to change the animation. Edit mode still routes
                // through the full Apply (animations aren't sampled onto the edit-mode skeleton).
                if (Application.isPlaying)
                    ApplyPoseOnly(outfit.emote);
                else
                    ScheduleApply();
            });
            pane.Add(_emotePopup);

            var transport = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            transport.Add(new Button(() => WithPreview(pc => pc.PlayEmote())) { text = "▶" });
            transport.Add(new Button(() => WithPreview(pc => pc.PauseEmote())) { text = "❚❚" });
            transport.Add(new Button(() => WithPreview(pc => pc.StopEmote())) { text = "■" });

            _emoteSlider = new Slider(0f, 1f) { style = { flexGrow = 1, marginLeft = 6 } };
            _emoteSlider.RegisterValueChangedCallback(evt => WithPreview(pc =>
            {
                pc.PauseEmote();
                pc.GoToEmote(evt.newValue);
            }));
            transport.Add(_emoteSlider);
            pane.Add(transport);

            // Keep the scrub slider range in sync with the loaded emote
            pane.schedule.Execute(() =>
            {
                if (!Application.isPlaying) return;
                var pc = FindPreviewController();
                if (pc == null) return;
                var length = pc.GetEmoteLength();
                if (length > 0f) _emoteSlider.highValue = length;
            }).Every(500);

            // --- Share code
            pane.Add(Header("Share code"));

            _shareCodeField = new TextField { multiline = true };
            _shareCodeField.style.whiteSpace = WhiteSpace.Normal;
            pane.Add(_shareCodeField);

            var shareButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            shareButtons.Add(new Button(() =>
            {
                EditorGUIUtility.systemCopyBuffer = outfit.ToShareCode();
                SetStatus("Share code copied to clipboard");
            }) { text = "Copy" });
            shareButtons.Add(new Button(() =>
            {
                LoadOutfit(OutfitDefinition.FromShareCode(_shareCodeField.value));
                SetStatus("Outfit loaded from share code");
            }) { text = "Load from code" });
            pane.Add(shareButtons);

            // --- Presets
            pane.Add(Header("Presets"));

            var presetField = new ObjectField("Preset") { objectType = typeof(OutfitPreset) };
            pane.Add(presetField);

            var presetButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            presetButtons.Add(new Button(() =>
            {
                if (presetField.value is not OutfitPreset preset)
                {
                    SetStatus("Select a preset first", true);
                    return;
                }

                LoadOutfit(preset.outfit.Clone());
                SetStatus($"Preset loaded: {preset.name}");
            }) { text = "Load" });

            presetButtons.Add(new Button(() =>
            {
                if (presetField.value is not OutfitPreset preset)
                {
                    SetStatus("Select a preset to overwrite (or use Save As)", true);
                    return;
                }

                preset.outfit = outfit.Clone();
                EditorUtility.SetDirty(preset);
                AssetDatabase.SaveAssets();
                SetStatus($"Preset saved: {preset.name}");
            }) { text = "Save" });

            presetButtons.Add(new Button(() =>
            {
                var path = EditorUtility.SaveFilePanelInProject("Save Outfit Preset", "OutfitPreset", "asset",
                    "Choose where to save the outfit preset");
                if (string.IsNullOrEmpty(path)) return;

                var preset = CreateInstance<OutfitPreset>();
                preset.outfit = outfit.Clone();
                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();
                presetField.value = preset;
                SetStatus($"Preset created: {path}");
            }) { text = "Save As..." });
            pane.Add(presetButtons);

            // --- Capture
            pane.Add(Header("Capture"));

            pane.Add(new Label("Rotation"));
            var rotationRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            rotationRow.Add(new Button(() => SnapRotate(15f)) { text = "<", style = { width = 30 } });
            _rotationLabel = new Label($"{rotationSnapAngle:0}°")
                { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleCenter } };
            rotationRow.Add(_rotationLabel);
            rotationRow.Add(new Button(() => SnapRotate(-15f)) { text = ">", style = { width = 30 } });
            pane.Add(rotationRow);

            pane.Add(new Button(LookAtCamera) { text = "Look at Camera" });

            var sizeRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var widthField = new IntegerField("Size") { value = captureWidth, style = { flexGrow = 1 } };
            widthField.RegisterValueChangedCallback(evt => captureWidth = Mathf.Clamp(evt.newValue, 64, 8192));
            var heightField = new IntegerField("x") { value = captureHeight, style = { flexGrow = 1 } };
            heightField.RegisterValueChangedCallback(evt => captureHeight = Mathf.Clamp(evt.newValue, 64, 8192));
            sizeRow.Add(widthField);
            sizeRow.Add(heightField);
            pane.Add(sizeRow);

            var transparentToggle = new Toggle("Transparent background") { value = transparentBackground };
            transparentToggle.RegisterValueChangedCallback(evt => transparentBackground = evt.newValue);
            pane.Add(transparentToggle);

            var fpsField = new IntegerField("Video FPS") { value = captureFrameRate };
            fpsField.RegisterValueChangedCallback(evt => captureFrameRate = Mathf.Clamp(evt.newValue, 10, 60));
            pane.Add(fpsField);

            var folderRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var folderField = new TextField("Output folder") { value = outputFolder, style = { flexGrow = 1 } };
            folderField.RegisterValueChangedCallback(evt => outputFolder = evt.newValue);
            folderRow.Add(folderField);
            folderRow.Add(new Button(() =>
            {
                var chosen = EditorUtility.OpenFolderPanel("Capture output folder", outputFolder, "");
                if (!string.IsNullOrEmpty(chosen))
                {
                    outputFolder = chosen;
                    folderField.SetValueWithoutNotify(chosen);
                }
            }) { text = "..." });
            pane.Add(folderRow);

            pane.Add(new Button(CaptureStill) { text = "📷  Capture Still" });

            _videoButton = new Button(ToggleVideo) { text = "⏺  Start Video" };
            pane.Add(_videoButton);

            pane.Add(new Button(RecordEmote) { text = "🎬  Record Emote (full length)" });

            var turntableRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var turntableButton = new Button(RecordTurntable) { text = "🔄  Record Turntable", style = { flexGrow = 1 } };
            var durationField = new FloatField("s") { value = turntableDuration, style = { width = 70 } };
            durationField.RegisterValueChangedCallback(evt => turntableDuration = Mathf.Clamp(evt.newValue, 1f, 60f));
            turntableRow.Add(turntableButton);
            turntableRow.Add(durationField);
            pane.Add(turntableRow);

            return pane;
        }

        // Rebuilds the live tuning sliders for the currently selected shader. Values are stored
        // and applied by StudioAvatarShaderSwitcher (the knob list is its single source of truth),
        // so a change pushes onto every avatar material immediately, in edit and play mode.
        private void BuildShaderTuning(VisualElement container)
        {
            container.Clear();

            var mode = StudioAvatarShaderSwitcher.Mode;
            var knobs = StudioAvatarShaderSwitcher.KnobsFor(mode);
            if (knobs.Length == 0)
            {
                container.Add(new Label("Stock shader — no tunable properties.")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Italic, marginTop = 4, opacity = 0.7f }
                });
                return;
            }

            // Matcap selector — the metal reflection texture bound to stylized-metal materials.
            // Both studio shaders use it; the list comes from the loaded MatcapPresets library.
            var matcapNames = StudioAvatarShaderSwitcher.GetMatcapNames();
            if (matcapNames.Length == 0)
            {
                container.Add(new Label("Matcap: library not loaded yet — load an outfit first.")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Italic, marginTop = 4, opacity = 0.7f }
                });
            }
            else
            {
                var active = StudioAvatarShaderSwitcher.ActiveMatcapName;
                if (Array.IndexOf(matcapNames, active) < 0) active = matcapNames[0];
                var matcapField = new PopupField<string>("Matcap", matcapNames.ToList(), active)
                {
                    tooltip = "Which matcap texture the stylized metal reflects (from MatcapPresets)."
                };
                matcapField.RegisterValueChangedCallback(evt =>
                    StudioAvatarShaderSwitcher.ActiveMatcapName = evt.newValue);
                container.Add(matcapField);
            }

            foreach (var knob in knobs)
            {
                if (knob.Kind == StudioKnobKind.Float)
                {
                    var slider = new Slider(knob.Label, knob.Min, knob.Max)
                    {
                        value = StudioAvatarShaderSwitcher.GetFloat(mode, knob),
                        showInputField = true,
                        tooltip = knob.Tooltip
                    };
                    slider.RegisterValueChangedCallback(evt =>
                        StudioAvatarShaderSwitcher.SetFloat(mode, knob, evt.newValue));
                    container.Add(slider);
                }
                else
                {
                    var color = new ColorField(knob.Label)
                    {
                        value = StudioAvatarShaderSwitcher.GetColor(mode, knob),
                        showAlpha = false,
                        tooltip = knob.Tooltip
                    };
                    color.RegisterValueChangedCallback(evt =>
                        StudioAvatarShaderSwitcher.SetColor(mode, knob, evt.newValue));
                    container.Add(color);
                }
            }

            container.Add(new Button(() =>
            {
                StudioAvatarShaderSwitcher.ResetKnobs(mode);
                BuildShaderTuning(container); // reflect reset values back into the fields
            }) { text = "Reset shader defaults", style = { marginTop = 4 } });
        }

        // Fortnite-style "item card" frame around the avatar (background gradient → rounded card →
        // avatar → bottom fade), composed by StudioCardFrame as camera-parented quads so it renders
        // through the capture camera. Studio scene only; a collapsible section since it's beauty-shot
        // dressing, not part of the outfit. See IMPLEMENTATION.md §18.
        private void BuildCardFrame(VisualElement pane)
        {
            var fold = new Foldout { text = "Card frame (beauty shot)", value = false, style = { marginTop = 4 } };

            var enable = new Toggle("Enable") { value = StudioCardFrame.Enabled };
            enable.RegisterValueChangedCallback(evt => StudioCardFrame.Enabled = evt.newValue);
            fold.Add(enable);

            var enableBackground = new Toggle("Enable background")
            {
                value = StudioCardFrame.BackgroundEnabled,
                tooltip = "Off leaves the card panel and avatar untouched but skips the fullscreen " +
                          "gradient behind them, so captures come out with a transparent background " +
                          "instead of the gradient."
            };
            enableBackground.RegisterValueChangedCallback(evt => StudioCardFrame.BackgroundEnabled = evt.newValue);
            fold.Add(enableBackground);

            var useDclBackground = new Toggle("Use Decentraland Background")
            {
                value = StudioCardFrame.UseDclBackground,
                tooltip = "Replaces the background gradient with the animated purple pattern from " +
                          "the Decentraland Explorer loading screens. Off by default."
            };
            useDclBackground.RegisterValueChangedCallback(evt => StudioCardFrame.UseDclBackground = evt.newValue);
            fold.Add(useDclBackground);

            var sideMask = new Toggle("Mask avatar to card sides")
            {
                value = StudioCardFrame.SideMask,
                tooltip = "Clip arms/hands that spill past the card's sides/bottom (the head still " +
                          "overflows the top), like the Fortnite cards."
            };
            sideMask.RegisterValueChangedCallback(evt => StudioCardFrame.SideMask = evt.newValue);
            fold.Add(sideMask);

            var hideOutline = new Toggle("Hide avatar outline")
            {
                value = StudioCardFrame.HideOutline,
                tooltip = "Suppress the avatar's outline (a thin silhouette line, most visible over " +
                          "the head against a light card) for clean beauty shots. Play mode only."
            };
            hideOutline.RegisterValueChangedCallback(evt => StudioCardFrame.HideOutline = evt.newValue);
            fold.Add(hideOutline);

            var body = new VisualElement();
            BuildCardBody(body);
            fold.Add(body);

            pane.Add(fold);
        }

        private static void BuildCardBody(VisualElement c)
        {
            c.Clear();

            Label Section(string t) => new(t) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } };

            // Colour presets — one button per CardColorPreset asset. Applies ONLY the 7 frame colours,
            // leaving the current margins/sizes/toggles intact. The whole body is rebuilt on apply so
            // the ColorFields below reflect the new values.
            c.Add(Section("Presets"));
            var presetRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            foreach (var preset in LoadCardPresets())
            {
                var p = preset; // capture per-iteration
                presetRow.Add(new Button(() =>
                {
                    ApplyCardPreset(p);
                    BuildCardBody(c);
                }) { text = p.name, style = { marginRight = 2, marginBottom = 2 } });
            }

            if (presetRow.childCount == 0)
                presetRow.Add(new Label("No presets — create via Assets ▸ Create ▸ Outfit Studio ▸ Card Color Preset")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Italic, opacity = 0.7f, marginRight = 4 }
                });

            presetRow.Add(new Button(() =>
            {
                EnsureFolder(CARD_PRESETS_DIR); // so the dialog opens in the intended default folder
                var path = EditorUtility.SaveFilePanelInProject("Save Card Color Preset", "CardColorPreset",
                    "asset", "Save the current 7 frame colours as a reusable preset.", CARD_PRESETS_DIR);
                if (string.IsNullOrEmpty(path)) return;

                var preset = ScriptableObject.CreateInstance<CardColorPreset>();
                preset.backgroundTop = StudioCardFrame.BgTop;
                preset.backgroundBottom = StudioCardFrame.BgBottom;
                preset.glow = StudioCardFrame.Glow;
                preset.cardTop = StudioCardFrame.CardTop;
                preset.cardBottom = StudioCardFrame.CardBottom;
                preset.border = StudioCardFrame.Border;
                preset.bottomFade = StudioCardFrame.Fade;
                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();
                BuildCardBody(c); // show the new preset button
            }) { text = "Save current…", tooltip = "Save the current 7 colours as a new preset asset", style = { marginRight = 2, marginBottom = 2 } });

            presetRow.Add(new Button(() => BuildCardBody(c))
            {
                text = "⟳", tooltip = "Rescan for preset assets", style = { marginBottom = 2 }
            });
            c.Add(presetRow);

            c.Add(Section("Background"));
            CardColor(c, "Top", () => StudioCardFrame.BgTop, v => StudioCardFrame.BgTop = v);
            CardColor(c, "Bottom", () => StudioCardFrame.BgBottom, v => StudioCardFrame.BgBottom = v);
            CardColor(c, "Glow", () => StudioCardFrame.Glow, v => StudioCardFrame.Glow = v, true);
            CardSlider(c, "Glow Height", 0f, 1f, () => StudioCardFrame.GlowHeight, v => StudioCardFrame.GlowHeight = v);
            CardSlider(c, "Glow Size", 0.1f, 1.5f, () => StudioCardFrame.GlowSize, v => StudioCardFrame.GlowSize = v);

            c.Add(Section("Card"));
            CardColor(c, "Top", () => StudioCardFrame.CardTop, v => StudioCardFrame.CardTop = v);
            CardColor(c, "Bottom", () => StudioCardFrame.CardBottom, v => StudioCardFrame.CardBottom = v);
            CardSlider(c, "Margin Sides", 0f, 0.3f, () => StudioCardFrame.MarginX, v => StudioCardFrame.MarginX = v);
            CardSlider(c, "Margin Top", 0f, 0.4f, () => StudioCardFrame.MarginTop, v => StudioCardFrame.MarginTop = v);
            CardSlider(c, "Margin Bottom", 0f, 0.3f, () => StudioCardFrame.MarginBottom, v => StudioCardFrame.MarginBottom = v);
            CardSlider(c, "Corner Radius", 0f, 0.5f, () => StudioCardFrame.CornerRadius, v => StudioCardFrame.CornerRadius = v);
            CardColor(c, "Border", () => StudioCardFrame.Border, v => StudioCardFrame.Border = v);
            CardSlider(c, "Border Width", 0f, 0.05f, () => StudioCardFrame.BorderWidth, v => StudioCardFrame.BorderWidth = v);

            c.Add(Section("Bottom fade"));
            CardColor(c, "Color", () => StudioCardFrame.Fade, v => StudioCardFrame.Fade = v);
            CardSlider(c, "Fade Height", 0f, 1f, () => StudioCardFrame.FadeHeight, v => StudioCardFrame.FadeHeight = v);
            CardSlider(c, "Fade Softness", 0f, 1f, () => StudioCardFrame.FadeSoftness, v => StudioCardFrame.FadeSoftness = v);

            c.Add(new Button(() =>
            {
                StudioCardFrame.ResetDefaults();
                BuildCardBody(c); // reflect reset values back into the fields
            }) { text = "Reset card defaults", style = { marginTop = 6 } });
        }

        private static void CardSlider(VisualElement c, string label, float min, float max,
            Func<float> get, Action<float> set)
        {
            var s = new Slider(label, min, max) { value = get(), showInputField = true };
            s.RegisterValueChangedCallback(e => set(e.newValue));
            c.Add(s);
        }

        private static void CardColor(VisualElement c, string label, Func<Color> get, Action<Color> set,
            bool showAlpha = false)
        {
            var f = new ColorField(label) { value = get(), showAlpha = showAlpha };
            f.RegisterValueChangedCallback(e => set(e.newValue));
            c.Add(f);
        }

        // All CardColorPreset assets in the project, sorted by name (one button each).
        private static List<CardColorPreset> LoadCardPresets() =>
            AssetDatabase.FindAssets($"t:{nameof(CardColorPreset)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CardColorPreset>)
                .Where(p => p != null)
                .OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Push a preset's 7 colours onto the live card frame. Each setter refreshes the frame; the
        // margins/sizes/toggles are deliberately not touched.
        private static void ApplyCardPreset(CardColorPreset p)
        {
            StudioCardFrame.BgTop = p.backgroundTop;
            StudioCardFrame.BgBottom = p.backgroundBottom;
            StudioCardFrame.Glow = p.glow;
            StudioCardFrame.CardTop = p.cardTop;
            StudioCardFrame.CardBottom = p.cardBottom;
            StudioCardFrame.Border = p.border;
            StudioCardFrame.Fade = p.bottomFade;
        }

        // Create an "Assets/…"-relative folder (and any missing parents) if it doesn't exist yet.
        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        // One button per single-frame GLB in StreamingAssets/poses/. Clicking sets the pose as the
        // active emote (it holds its one frame in play mode — where stills are captured). The active
        // pose's button is disabled (= selected), same convention as the shader buttons. Rebuilt in
        // place so the selection highlight and a fresh folder scan (⟳) both refresh live.
        private void BuildPoseButtons(VisualElement grid)
        {
            grid.Clear();

            var names = GetPoseNames();
            foreach (var name in names)
            {
                var emoteName = $"{POSES_EMBEDDED_PREFIX}/{name}";
                var btn = new Button(() =>
                {
                    outfit.emote = emoteName;
                    RemoveDraftEmote(); // an equipped draft emote would override the pose
                    _poseLabel.text = $"Pose: {name}";
                    SyncEmotePopup();
                    RefreshShareCode();
                    // Play mode: pose ONLY the currently-loaded avatar (which may be a Random Profile
                    // from the Debug tab) without reloading the custom outfit. Edit mode: assemble the
                    // outfit + pose onto the preview skeleton as before.
                    if (Application.isPlaying)
                        ApplyPoseOnly(emoteName);
                    else
                        ScheduleApply();
                    BuildPoseButtons(grid); // refresh the selected-highlight
                }) { text = name, style = { marginRight = 2, marginBottom = 2 } };
                btn.SetEnabled(outfit.emote != emoteName); // disabled = selected
                grid.Add(btn);
            }

            if (names.Count == 0)
                grid.Add(new Label($"No poses — drop single-frame GLBs in Assets/{POSES_DIR_UNDER_ASSETS}/")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Italic, opacity = 0.7f, marginRight = 4 }
                });

            grid.Add(new Button(() => BuildPoseButtons(grid))
            {
                text = "⟳",
                tooltip = "Rescan the poses folder",
                style = { marginBottom = 2 }
            });
        }

        // Base names (no extension) of the pose GLBs in Assets/OutfitStudio/Poses/, sorted.
        // Editor-time file scan (Application.dataPath = <project>/Assets), valid outside play mode.
        private static List<string> GetPoseNames()
        {
            var dir = System.IO.Path.Combine(Application.dataPath, POSES_DIR_UNDER_ASSETS);
            if (!System.IO.Directory.Exists(dir)) return new List<string>();

            return System.IO.Directory.GetFiles(dir, "*.glb")
                .Select(System.IO.Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Label Header(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 10,
                    marginBottom = 2
                }
            };
        }

        private ColorField ColorRow(VisualElement parent, string label, Color initial, Action<Color> setter)
        {
            var field = new ColorField(label) { value = initial, showAlpha = false };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                RefreshShareCode();
                ScheduleApply();
            });
            parent.Add(field);
            return field;
        }

        private void RefreshSlots()
        {
            if (_slotsContainer == null) return;

            _slotsContainer.Clear();

            // Draft (builder collection) items — shown above the catalog ones
            foreach (var base64 in outfit.base64Items.ToList())
            {
                var (name, category, isEmote) = DescribeDraft(base64);
                if (isEmote) continue; // the pose row covers draft emotes

                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 }
                };

                row.Add(new Label($"[{category}] {name} (draft)")
                {
                    style = { flexGrow = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, marginLeft = 4 }
                });

                row.Add(new Button(() =>
                {
                    outfit.base64Items.Remove(base64);
                    RefreshSlots();
                    RefreshShareCode();
                    ScheduleApply();
                }) { text = "✕" });

                _slotsContainer.Add(row);
            }

            if (outfit.urns.Count == 0 && outfit.base64Items.Count == 0)
            {
                _slotsContainer.Add(new Label("No wearables equipped — pick items from the browser")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Italic, marginTop = 4, marginBottom = 4 }
                });
                return;
            }

            foreach (var urn in outfit.urns.ToList())
            {
                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 }
                };

                var known = _knownItems.GetValueOrDefault(urn);

                var thumb = new Image { scaleMode = ScaleMode.ScaleToFit, style = { width = 24, height = 24 } };
                row.Add(thumb);
                if (known != null)
                {
                    LoadThumbnail(known.thumbnail, tex =>
                    {
                        if (tex != null) thumb.image = tex;
                    });
                }

                var slot = known?.Slot ?? "?";
                var name = known?.name ?? urn[(urn.LastIndexOf(':') + 1)..];
                row.Add(new Label($"[{slot}] {name}")
                {
                    tooltip = urn,
                    style = { flexGrow = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, marginLeft = 4 }
                });

                row.Add(new Button(() =>
                {
                    outfit.urns.Remove(urn);
                    RefreshSlots();
                    RefreshShareCode();
                    ScheduleApply();
                }) { text = "✕" });

                _slotsContainer.Add(row);
            }
        }

        private void RefreshShareCode()
        {
            _shareCodeField?.SetValueWithoutNotify(outfit.ToShareCode());
        }

        // ---------------------------------------------------------------- Draft (builder) items

        private readonly Dictionary<string, (string name, string category, bool isEmote)> _draftDescriptions = new();

        /// <summary>Reads name/category/type from a base64 RawActiveEntity without full parsing.</summary>
        private (string name, string category, bool isEmote) DescribeDraft(string base64)
        {
            if (_draftDescriptions.TryGetValue(base64, out var cached)) return cached;

            (string, string, bool) description;
            try
            {
                var json = JObject.Parse(Encoding.UTF8.GetString(OutfitDefinition.DecodeBase64(base64)));
                var isEmote = json["emoteDataADR74"] is JObject;
                description = (
                    json["name"]?.Value<string>() ?? "draft item",
                    isEmote ? "emote" : json["data"]?["category"]?.Value<string>() ?? "?",
                    isEmote);
            }
            catch
            {
                description = ("invalid draft item", "?", false);
            }

            _draftDescriptions[base64] = description;
            return description;
        }

        private void RemoveDraftEmote() =>
            outfit.base64Items.RemoveAll(base64 => DescribeDraft(base64).isEmote);

        private void LoadOutfit(OutfitDefinition loaded)
        {
            outfit = loaded;

            // Face-feature previews belong to the session that picked them, not to whatever outfit
            // happens to be loaded next — start clean rather than carrying stale overrides across.
            _previewFaceUrns.Clear();
            RefreshFaceGrid();

            _bodyShapePopup.SetValueWithoutNotify(
                outfit.bodyShape == WearablesConstants.BODY_SHAPE_FEMALE ? "Female" : "Male");
            _skinField.SetValueWithoutNotify(outfit.skinColor);
            _hairField.SetValueWithoutNotify(outfit.hairColor);
            _eyeField.SetValueWithoutNotify(outfit.eyeColor);
            _poseLabel.text = $"Pose: {outfit.emote}";
            SyncEmotePopup();

            HydrateKnownItems();
            RefreshSlots();
            RefreshShareCode();
            ScheduleApply();
        }

        /// <summary>
        /// Keeps the "Embedded" popup's displayed value truthful whenever outfit.emote changes from
        /// somewhere other than the popup itself (pose buttons, draft/catalog emote picks, loading a
        /// share code or preset). Falls back to <see cref="EMBEDDED_EMOTE_NONE"/> for anything that
        /// isn't a literal EMBEDDED_EMOTES entry (poses, URNs) so the popup never silently shows a
        /// stale emote while something else is actually loaded/playing.
        /// </summary>
        private void SyncEmotePopup() =>
            _emotePopup?.SetValueWithoutNotify(
                EMBEDDED_EMOTES.Contains(outfit.emote) ? outfit.emote : EMBEDDED_EMOTE_NONE);

        /// <summary>
        /// Resolves names/thumbnails for URNs we don't have catalog info for
        /// (pasted share codes, presets, domain reloads).
        /// </summary>
        private void HydrateKnownItems()
        {
            var unknown = outfit.urns.Append(outfit.emote)
                .Where(urn => !string.IsNullOrEmpty(urn)
                              && urn.StartsWith("urn:", StringComparison.OrdinalIgnoreCase)
                              && !urn.Contains(":off-chain:")
                              && !_knownItems.ContainsKey(urn))
                .Distinct()
                .ToArray();

            if (unknown.Length == 0) return;

            CatalogService.Search(new CatalogQuery { Urns = unknown, First = unknown.Length },
                page =>
                {
                    foreach (var item in page.data)
                        _knownItems[item.urn] = item;
                    RefreshSlots();
                },
                error => Debug.LogWarning($"[OutfitStudio] Failed to resolve URNs: {error}"));
        }

        // ---------------------------------------------------------------- Apply

        private void ScheduleApply()
        {
            if (!autoApply) return;

            _pendingApply?.Pause();
            _pendingApply = rootVisualElement.schedule.Execute(Apply);
            _pendingApply.StartingIn(400);
        }

        private void Apply()
        {
            var previewOutfit = BuildPreviewOutfit(); // outfit + local face-feature overrides, never shared

            if (!Application.isPlaying)
            {
                // Edit-mode 3D preview: assembles onto the scene skeleton without play mode.
                // Pose/emote playback and capture still require play mode.
                EditModeAvatarPreview.Apply(previewOutfit, SetStatus);
                return;
            }

            var previewController = FindPreviewController();
            if (previewController == null)
            {
                SetStatus("No PreviewController in the scene — open Assets/Scenes/Main.unity", true);
                return;
            }

            var config = AangConfiguration.Instance;
            config.SetMode("builder");
            config.BodyShape = outfit.bodyShape;
            config.Urns = FilterForBodyShape(previewOutfit.urns).Select(URNUtils.SanitizeURN).ToList();
            config.SetSkinColor(ColorUtility.ToHtmlStringRGB(outfit.skinColor));
            config.SetHairColor(ColorUtility.ToHtmlStringRGB(outfit.hairColor));
            config.SetEyeColor(ColorUtility.ToHtmlStringRGB(outfit.eyeColor));
            config.Emote = string.IsNullOrEmpty(outfit.emote) ? "idle" : outfit.emote;

            // Draft (builder) items — LoadForBuilder gives base64 per-category priority
            // and a base64 emote overrides the pose
            config.Base64.Clear();
            foreach (var base64 in outfit.base64Items)
            {
                try
                {
                    config.AddBase64(base64);
                }
                catch (Exception e)
                {
                    SetStatus($"Invalid draft item skipped: {e.Message}", true);
                }
            }

            previewController.gameObject.SetActive(true);
            previewController.InvokeReload();

            SetStatus("Outfit applied");
        }

        /// <summary>
        /// Play-mode pose/animation change that does NOT reload the custom outfit: sets only
        /// <c>config.Emote</c> and reloads, so whatever avatar is loaded keeps its identity and just
        /// changes emote (the AvatarLoader diffs the unchanged wearables, so only the emote reloads).
        /// Shared by the pose buttons and the "Embedded" emote popup — either way the currently-loaded
        /// avatar (which may be a Random Profile from the Debug tab) gets re-posed/re-animated in
        /// place instead of switching to the studio's custom Builder outfit.
        ///
        /// Mode handling: <b>Builder</b> (the custom outfit) is kept as-is. <b>Any other</b> mode is
        /// switched to <b>Profile</b> — because Jesus mode hard-codes its emote (<c>Particles_Anim</c>,
        /// the arms-out "jesus" pose) and Marketplace shows a wearable, both ignoring
        /// <c>config.Emote</c>; Profile mode applies it. <c>config.Profile</c> is preserved, so a
        /// Random Profile stays the same avatar, now posed/animated. Edit mode routes through Apply.
        /// </summary>
        private void ApplyPoseOnly(string emoteName)
        {
            var pc = FindPreviewController();
            if (pc == null)
            {
                SetStatus("No PreviewController in the scene", true);
                return;
            }

            var config = AangConfiguration.Instance;
            config.Emote = string.IsNullOrEmpty(emoteName) ? "idle" : emoteName;

            // Keep the custom outfit (Builder); otherwise pose the current profile avatar in Profile
            // mode, where config.Emote is actually applied (Jesus/Marketplace ignore it).
            if (config.Mode != PreviewMode.Builder)
                config.SetMode("profile");

            // Loop in Profile mode, matching how Builder mode already always loops embedded emotes
            // (ResolveBuilderEmote hardcodes loop:true) — without this a single-frame pose would end
            // instantly and revert to the base idle, and a multi-frame animation wouldn't hold either.
            config.EmoteLoop = true;

            pc.gameObject.SetActive(true);
            pc.InvokeReload();
            SetStatus("Applied to the loaded avatar");
        }

        /// <summary>
        /// Drops wearables that have no representation for the selected body shape (the loader
        /// would throw). Only known catalog items can be checked; unknown URNs pass through.
        /// </summary>
        private List<string> FilterForBodyShape(IEnumerable<string> urns)
        {
            var shapeName = outfit.bodyShape == WearablesConstants.BODY_SHAPE_FEMALE ? "BaseFemale" : "BaseMale";
            var result = new List<string>();

            foreach (var urn in urns)
            {
                var known = _knownItems.GetValueOrDefault(urn);
                var bodyShapes = known?.data?.wearable?.bodyShapes;

                if (known != null && bodyShapes is { Length: > 0 } && !bodyShapes.Contains(shapeName))
                {
                    SetStatus($"Skipped {known.name}: no {shapeName} representation", true);
                    continue;
                }

                result.Add(urn);
            }

            return result;
        }

        private static PreviewController FindPreviewController() =>
            FindAnyObjectByType<PreviewController>(FindObjectsInactive.Include);

        private static void WithPreview(Action<PreviewController> action)
        {
            if (!Application.isPlaying) return;
            var pc = FindPreviewController();
            if (pc != null) action(pc);
        }

        // ---------------------------------------------------------------- Capture

        private bool EnsurePlaying()
        {
            if (Application.isPlaying) return true;
            SetStatus("Enter play mode first", true);
            return false;
        }

        private void CaptureStill()
        {
            if (!EnsurePlaying()) return;

            SetStatus("Capturing...");
            OutfitCapture.CaptureStill(captureWidth, captureHeight, transparentBackground, outputFolder, path =>
            {
                if (path != null)
                {
                    SetStatus($"Saved {path}");
                    OutfitCapture.RevealInFinder(path);
                }
                else
                {
                    SetStatus("Capture failed", true);
                }
            });
        }

        private void ToggleVideo()
        {
            if (OutfitCapture.IsRecording)
            {
                OutfitCapture.StopVideo();
                _videoButton.text = "⏺  Start Video";
                SetStatus("Video saved");
                return;
            }

            if (!EnsurePlaying()) return;

            OutfitCapture.StartVideo(captureWidth, captureHeight, captureFrameRate, outputFolder);
            _videoButton.text = "⏹  Stop Video";
            SetStatus("Recording...");
        }

        private void RecordEmote()
        {
            if (!EnsurePlaying() || OutfitCapture.IsRecording) return;

            var pc = FindPreviewController();
            var length = pc != null ? pc.GetEmoteLength() : 0f;

            if (length <= 0f)
            {
                SetStatus("No emote loaded — pick a pose first", true);
                return;
            }

            OutfitCapture.StartVideo(captureWidth, captureHeight, captureFrameRate, outputFolder);
            pc.PlayEmote();
            SetStatus($"Recording emote ({length:0.0}s)...");

            rootVisualElement.schedule.Execute(() =>
            {
                OutfitCapture.StopVideo();
                _videoButton.text = "⏺  Start Video";
                SetStatus("Emote video saved");
            }).StartingIn((long)((length + 0.5f) * 1000));
        }

        private void RecordTurntable()
        {
            if (!EnsurePlaying() || OutfitCapture.IsRecording) return;

            var avatarLoader = FindAnyObjectByType<AvatarLoader>();
            if (avatarLoader == null)
            {
                SetStatus("No avatar loaded", true);
                return;
            }

            var rotator = avatarLoader.GetComponentInParent<DragRotator>();
            var target = rotator != null ? rotator.gameObject : avatarLoader.gameObject;

            var driver = target.GetComponent<TurntableDriver>();
            if (driver == null) driver = target.AddComponent<TurntableDriver>();

            driver.enabled = false;
            driver.Duration = turntableDuration;
            driver.Completed += () =>
            {
                OutfitCapture.StopVideo();
                _videoButton.text = "⏺  Start Video";
                SetStatus("Turntable video saved");
            };

            OutfitCapture.StartVideo(captureWidth, captureHeight, captureFrameRate, outputFolder);
            driver.enabled = true;
            SetStatus($"Recording turntable ({turntableDuration:0.0}s)...");
        }

        private void SnapRotate(float deltaDegrees)
        {
            if (!EnsurePlaying()) return;

            var avatarLoader = FindAnyObjectByType<AvatarLoader>();
            var rotator = avatarLoader != null ? avatarLoader.GetComponentInParent<DragRotator>() : null;
            if (rotator == null)
            {
                SetStatus("No avatar loaded", true);
                return;
            }

            rotationSnapAngle += deltaDegrees;
            _rotationLabel.text = $"{rotationSnapAngle:0}°";
            rotator.SnapRotation(rotationSnapAngle);
        }

        /// <summary>Turns the head bone toward the camera (or lets the current pose/emote drive
        /// it again), independent of body rotation. Returns false (and leaves the toggle
        /// unapplied) when the head bone/avatar/camera can't be resolved.</summary>
        /// <summary>Rotates the head bone to face the camera, giving the neck bone a share of
        /// the turn (see <see cref="NECK_LOOK_SHARE"/>) so it doesn't read as the head twisting
        /// on its own. One-shot: freezes the current pose first (<see cref="AvatarLoader.FreezePose"/>)
        /// since the legacy Animation component would otherwise re-drive these bones back to the
        /// clip pose on the very next frame, undoing the adjustment.</summary>
        private void LookAtCamera()
        {
            if (!EnsurePlaying()) return;

            var avatarLoader = FindAnyObjectByType<AvatarLoader>();
            if (avatarLoader == null)
            {
                SetStatus("No avatar loaded", true);
                return;
            }

            var headBone = avatarLoader.HeadBone;
            if (headBone == null)
            {
                SetStatus("Head bone not found", true);
                return;
            }

            var camera = avatarLoader.MainCamera;
            var direction = camera.transform.position - headBone.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            avatarLoader.FreezePose();

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);

            var neckBone = avatarLoader.NeckBone;
            if (neckBone != null)
            {
                var remainingTurn = targetRotation * Quaternion.Inverse(headBone.rotation);
                neckBone.rotation = Quaternion.Slerp(Quaternion.identity, remainingTurn, NECK_LOOK_SHARE) *
                                     neckBone.rotation;
            }

            headBone.rotation = targetRotation;
            SetStatus("Head looking at camera");
        }

        // ---------------------------------------------------------------- Misc

        private void SetStatus(string message, bool error = false)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = message;
            _statusLabel.style.color = error ? new Color(1f, 0.45f, 0.4f) : new Color(0.65f, 0.65f, 0.65f);
            if (error) Debug.LogWarning($"[OutfitStudio] {message}");
        }
    }
}
