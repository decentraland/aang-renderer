using System;
using System.Collections.Generic;
using System.Linq;
using Preview;
using Services;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Networking;
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
        private const int PAGE_SIZE = 24;
        private const int THUMB_SIZE = 90;

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

        private static readonly List<string> RARITIES = new()
        {
            "any", "common", "uncommon", "rare", "epic", "legendary", "exotic", "mythic", "unique"
        };

        private static readonly List<string> GENDERS = new() { "any", "male", "female", "unisex" };
        private static readonly List<string> SORT_OPTIONS = new() { "newest", "name", "cheapest" };

        private static readonly List<string> EMBEDDED_EMOTES = new()
        {
            "idle", "clap", "dab", "dance", "fashion", "fashion-2", "fashion-3", "fashion-4",
            "love", "money", "fist-pump", "head-explode"
        };

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

        // Browser state (session only)
        private readonly CatalogQuery _query = new() { First = PAGE_SIZE };
        private CatalogPage _results;
        private int _searchSequence;

        // urn -> catalog item, used to resolve slot/name/thumbnail for outfit rows
        private readonly Dictionary<string, CatalogItem> _knownItems = new();

        private static readonly Dictionary<string, Texture2D> THUMBNAIL_CACHE = new();
        private static readonly HashSet<string> THUMBNAILS_IN_FLIGHT = new();

        // UI references
        private VisualElement _grid;
        private Label _pageLabel;
        private Button _prevButton, _nextButton;
        private VisualElement _slotsContainer;
        private Label _poseLabel;
        private TextField _shareCodeField;
        private Label _statusLabel;
        private Button _playButton;
        private Button _videoButton;
        private Slider _emoteSlider;
        private PopupField<string> _bodyShapePopup;
        private ColorField _skinField, _hairField, _eyeField;
        private IVisualElementScheduledItem _pendingApply;

        [MenuItem("Decentraland/Outfit Studio")]
        public static void Open()
        {
            var window = GetWindow<OutfitStudioWindow>("Outfit Studio");
            window.minSize = new Vector2(760, 480);
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
            var wearablesTab = new Button { text = "Wearables" };
            var emotesTab = new Button { text = "Emotes / Poses" };

            void SelectTab(string category)
            {
                _query.Category = category;
                _query.WearableCategory = null;
                _query.EmoteCategory = null;
                wearablesTab.SetEnabled(category != "wearable");
                emotesTab.SetEnabled(category != "emote");
                ResetAndSearch();
            }

            wearablesTab.clicked += () => SelectTab("wearable");
            emotesTab.clicked += () => SelectTab("emote");
            wearablesTab.SetEnabled(false);
            tabs.Add(wearablesTab);
            tabs.Add(emotesTab);
            pane.Add(tabs);

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
            pane.Add(search);

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
                _query.SortBy = sortPopup.value;
                ResetAndSearch();
            });
            filters.Add(sortPopup);

            // Swap slot filter choices when the tab changes
            wearablesTab.clicked += () => { slotPopup.choices = WEARABLE_SLOTS; slotPopup.index = 0; };
            emotesTab.clicked += () => { slotPopup.choices = EMOTE_CATEGORIES; slotPopup.index = 0; };

            pane.Add(filters);

            // Results grid
            var scroll = new ScrollView { style = { flexGrow = 1 } };
            _grid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, paddingLeft = 4, paddingTop = 4 }
            };
            scroll.Add(_grid);
            pane.Add(scroll);

            // Pagination
            var pager = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, paddingBottom = 4 } };
            _prevButton = new Button(() => { _query.Skip = Mathf.Max(0, _query.Skip - PAGE_SIZE); RunSearch(); }) { text = "◀" };
            _nextButton = new Button(() => { _query.Skip += PAGE_SIZE; RunSearch(); }) { text = "▶" };
            _pageLabel = new Label("") { style = { unityTextAlign = TextAnchor.MiddleCenter, marginLeft = 8, marginRight = 8 } };
            pager.Add(_prevButton);
            pager.Add(_pageLabel);
            pager.Add(_nextButton);
            pane.Add(pager);

            return pane;
        }

        private void ResetAndSearch()
        {
            _query.Skip = 0;
            RunSearch();
        }

        private void RunSearch()
        {
            if (_grid == null) return;

            SetStatus("Searching catalog...");

            var sequence = ++_searchSequence; // guard against out-of-order responses
            CatalogService.Search(_query,
                page =>
                {
                    if (sequence != _searchSequence) return;
                    _results = page;
                    RebuildGrid();
                    SetStatus($"{page.total} items");
                },
                error =>
                {
                    if (sequence != _searchSequence) return;
                    SetStatus($"Catalog error: {error}", true);
                });
        }

        private void RebuildGrid()
        {
            _grid.Clear();

            if (_results?.data == null) return;

            foreach (var item in _results.data)
            {
                _grid.Add(BuildTile(item));
            }

            var from = _query.Skip + 1;
            var to = _query.Skip + (_results.data?.Length ?? 0);
            _pageLabel.text = _results.total > 0 ? $"{from}–{to} of {_results.total}" : "no results";
            _prevButton.SetEnabled(_query.Skip > 0);
            _nextButton.SetEnabled(to < _results.total);
        }

        private VisualElement BuildTile(CatalogItem item)
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

            tile.RegisterCallback<ClickEvent>(_ => OnItemClicked(item));

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
                _poseLabel.text = $"Pose: {item.name}";
                SetStatus($"Pose set: {item.name}");
            }
            else
            {
                var slot = item.Slot;

                // One wearable per slot: drop anything we know occupies the same category
                outfit.urns.RemoveAll(urn =>
                    _knownItems.TryGetValue(urn, out var known) && known.Slot == slot);
                outfit.urns.Remove(item.urn);
                outfit.urns.Add(item.urn);

                SetStatus($"Equipped {item.name} ({slot})");
                RefreshSlots();
            }

            RefreshShareCode();
            ScheduleApply();
        }

        // ---------------------------------------------------------------- Outfit pane

        private VisualElement BuildOutfitPane()
        {
            var pane = new ScrollView { style = { paddingLeft = 6, paddingRight = 6, paddingTop = 4 } };

            // --- Outfit
            pane.Add(Header("Outfit"));

            _bodyShapePopup = new PopupField<string>("Body shape", new List<string> { "Male", "Female" },
                outfit.bodyShape == WearablesConstants.BODY_SHAPE_FEMALE ? 1 : 0);
            _bodyShapePopup.RegisterValueChangedCallback(_ =>
            {
                outfit.bodyShape = _bodyShapePopup.index == 1
                    ? WearablesConstants.BODY_SHAPE_FEMALE
                    : WearablesConstants.BODY_SHAPE_MALE;
                RefreshShareCode();
                ScheduleApply();
            });
            pane.Add(_bodyShapePopup);

            _slotsContainer = new VisualElement();
            pane.Add(_slotsContainer);

            // --- Colors
            pane.Add(Header("Colors"));
            _skinField = ColorRow(pane, "Skin", outfit.skinColor, c => outfit.skinColor = c);
            _hairField = ColorRow(pane, "Hair", outfit.hairColor, c => outfit.hairColor = c);
            _eyeField = ColorRow(pane, "Eyes", outfit.eyeColor, c => outfit.eyeColor = c);

            // --- Pose
            pane.Add(Header("Pose"));

            _poseLabel = new Label($"Pose: {outfit.emote}");
            pane.Add(_poseLabel);

            var emotePopup = new PopupField<string>("Embedded", EMBEDDED_EMOTES,
                Mathf.Max(0, EMBEDDED_EMOTES.IndexOf(outfit.emote)));
            emotePopup.RegisterValueChangedCallback(_ =>
            {
                outfit.emote = emotePopup.value;
                _poseLabel.text = $"Pose: {outfit.emote}";
                RefreshShareCode();
                ScheduleApply();
            });
            pane.Add(emotePopup);

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

            if (outfit.urns.Count == 0)
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

        private void LoadOutfit(OutfitDefinition loaded)
        {
            outfit = loaded;

            _bodyShapePopup.SetValueWithoutNotify(
                outfit.bodyShape == WearablesConstants.BODY_SHAPE_FEMALE ? "Female" : "Male");
            _skinField.SetValueWithoutNotify(outfit.skinColor);
            _hairField.SetValueWithoutNotify(outfit.hairColor);
            _eyeField.SetValueWithoutNotify(outfit.eyeColor);
            _poseLabel.text = $"Pose: {outfit.emote}";

            HydrateKnownItems();
            RefreshSlots();
            RefreshShareCode();
            ScheduleApply();
        }

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
            if (!Application.isPlaying)
            {
                // Edit-mode 3D preview: assembles onto the scene skeleton without play mode.
                // Pose/emote playback and capture still require play mode.
                EditModeAvatarPreview.Apply(outfit, SetStatus);
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
            config.Urns = FilterForBodyShape(outfit.urns).Select(URNUtils.SanitizeURN).ToList();
            config.SetSkinColor(ColorUtility.ToHtmlStringRGB(outfit.skinColor));
            config.SetHairColor(ColorUtility.ToHtmlStringRGB(outfit.hairColor));
            config.SetEyeColor(ColorUtility.ToHtmlStringRGB(outfit.eyeColor));
            config.Emote = string.IsNullOrEmpty(outfit.emote) ? "idle" : outfit.emote;

            previewController.gameObject.SetActive(true);
            previewController.InvokeReload();

            SetStatus("Outfit applied");
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
            FindFirstObjectByType<PreviewController>(FindObjectsInactive.Include);

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

            var path = OutfitCapture.CaptureStill(captureWidth, captureHeight, transparentBackground, outputFolder);
            if (path != null)
            {
                SetStatus($"Saved {path}");
                OutfitCapture.RevealInFinder(path);
            }
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

            var avatarLoader = FindFirstObjectByType<AvatarLoader>();
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
