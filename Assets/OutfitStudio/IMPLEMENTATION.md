# Outfit Studio — Implementation Documentation

> Developer/handoff doc for the Outfit Studio artist tool. The user-facing workflow doc is
> [README.md](README.md) in this folder. Written 2026-07-13; iteration 2 (edit-mode preview)
> added same day — see §11.

## 1. What this is

An **editor-only artist tool** for Decentraland: browse the live marketplace catalog, compose an
outfit (one wearable per slot) on an avatar, pose it with an emote, and capture high-res PNG
stills and MP4 video. It drives the existing aang-renderer pipeline in **play mode** — it does
not reimplement any loading/rendering.

Menu entry: **Decentraland ▸ Outfit Studio** (`OutfitStudioWindow`, UI Toolkit EditorWindow).

## 2. Repo / branch context

- Built on local branch **`feat/outfit-studio`**, branched off **`main`** (deliberately *not* off
  `feat/avatar-toon-shader-metallic-normals` — kept independent of the in-progress
  metallic/normals shader work).
- All code is folder-isolated under `Assets/OutfitStudio/`. **No asmdefs** — the repo has none,
  so everything compiles into `Assembly-CSharp` / `Assembly-CSharp-Editor` (an asmdef cannot
  reference `Assembly-CSharp`, which is where the renderer classes live). `Editor/` uses Unity's
  special-folder rule to land in the editor assembly.
- Touch points outside this folder (kept minimal for easy extraction later; all behave
  identically in play mode / production builds):
  1. `Packages/manifest.json` — added `"com.unity.recorder": "5.1.2"` (editor-only package).
  2. `Assets/Scripts/Preview/PreviewController.cs` — `ResolveBuilderEmote()` (see §6). Additive.
  3. `Assets/Scripts/Loading/GLTFLoader.cs` — `Sanitize` uses `DestroyImmediate` outside play
     mode (was an edit-mode error; play path unchanged — see §11).
  4. `Assets/Scripts/Services/EntityService.cs` — removed a hard `Assert.AreEqual` that fired
     when the catalyst returns fewer entities than requested (e.g. third-party/linked wearables).
     The graceful shortfall handling below it (warn + return resolved subset) was already there
     and now actually runs. Assertions are stripped from production builds, so prod is unchanged.
  5. `Assets/Scripts/Preview/PreviewUIPresenter.cs` — the debug URL presets list was hoisted
     from a local variable in `EnableDebug()` to `public static readonly DEBUG_URL_PRESETS`
     (same content) so the window's Debug tab shares one source of truth. Behavior-neutral.

## 3. File map

```
Assets/OutfitStudio/
├── README.md                      # artist-facing workflow doc
├── IMPLEMENTATION.md              # this file
├── Runtime/
│   ├── CatalogModels.cs           # CatalogQuery, CatalogPage, CatalogItem DTOs
│   ├── CatalogService.cs          # GET /v1/items (marketplace browse + URN lookup)
│   ├── OutfitDefinition.cs        # the outfit model + share-code round-trip
│   ├── OutfitPreset.cs            # ScriptableObject preset asset
│   └── TurntableDriver.cs         # deterministic 360° spin MonoBehaviour
└── Editor/
    ├── OutfitCapture.cs           # still (RenderPipeline request) + video (Unity Recorder)
    ├── EditModeAvatarPreview.cs   # edit-mode outfit assembly on the scene skeleton (§11)
    └── OutfitStudioWindow.cs      # the EditorWindow (all UI + orchestration)
```

## 4. How it drives the renderer (the key idea)

The renderer's **Builder mode** already does outfit assembly. The tool just writes into the
existing config singleton and reloads:

```csharp
var config = AangConfiguration.Instance;       // Assets/Scripts/AangConfiguration.cs
config.SetMode("builder");
config.BodyShape = outfit.bodyShape;           // body shape URN
config.Urns = <wearable URNs>;                 // one per slot; sanitized via URNUtils.SanitizeURN
config.SetSkinColor/SetHairColor/SetEyeColor(hexNoHash);
config.Emote = <embedded name | emote URN>;
FindFirstObjectByType<PreviewController>(FindObjectsInactive.Include).InvokeReload();
```

Reload path: `PreviewController.Reload()` → `LoadForBuilder()` → slot-dedup (one wearable per
category, last wins) → `AvatarLoader.LoadAvatar(...)`. `AvatarLoader` **diffs against
`_loadedModels`**, so swapping one wearable only reloads that wearable — this is what makes
live click-to-equip feel fast. `InvokeReload()` during an in-flight load safely queues
(`_shouldReload` loop), so debounced rapid clicking is safe.

**Play-mode entry:** the window never edits `Bootstrap.debugUrl` (would dirty the scene).
Instead: `applyOnPlay = true` → `EditorApplication.EnterPlaymode()` → on
`EnteredPlayMode`, apply is scheduled ~1s later (lets `Bootstrap.Start()` parse debugUrl and kick
its initial load; our reload then supersedes it).

## 5. Marketplace catalog access

`CatalogService` (Runtime, but editor-safe) hits:

```
GET https://marketplace-api.decentraland.{org|zone}/v1/items
    ?first=24&skip=N
    [&category=wearable|emote] [&search=text]
    [&wearableCategory=slot] [&emoteCategory=cat]
    [&rarity=r] [&wearableGender|emoteGender=g] [&sortBy=newest|name|cheapest]
    [&urn=...&urn=...]        # direct URN lookup mode (ignores browse filters)
```

- Environment comes from `Services.APIService.Environment` (`"org"`/`"zone"`) — same switch the
  whole renderer uses; the toolbar prod/dev popup sets it.
- **Callback-based, not `Awaitable`** — deliberately, so it runs in *edit mode* (the browser works
  without entering play). Uses `UnityWebRequest` + `operation.completed`.
- Response parsed with `JsonUtility` into `CatalogPage { CatalogItem[] data; int total; }`.
  `CatalogItem` declares **only consumed fields** (`id, name, thumbnail, urn, category, rarity,
  isOnSale, data.wearable.{bodyShapes,category,isSmart}, data.emote.{...}`) — extra JSON fields
  are ignored, which keeps us resilient to API additions. `bodyShapes` values are
  `"BaseMale"`/`"BaseFemale"`.
- `CatalogItem.Slot` → wearable category, or `"emote"` for emotes.
- The URN-lookup mode (`CatalogQuery.Urns`) is used to **hydrate** names/thumbnails for URNs the
  window doesn't know (pasted share codes, loaded presets, after domain reload). Off-chain URNs
  (`:off-chain:` base-avatars) are skipped — they're not marketplace items.

Thumbnails: static `Dictionary<string, Texture2D>` cache in the window +
`UnityWebRequestTexture`, textures marked `HideAndDontSave`.

## 6. Renderer change: emote URNs as poses

`PreviewController.LoadForBuilder` previously supported only embedded emotes
(StreamingAssets GLBs) or base64. Added:

```csharp
private static async Awaitable<EntityDefinition> ResolveBuilderEmote(string emoteName)
// null for "idle"; EntityService.GetEntities() for "urn:..." (must be EntityType.Emote);
// EntityDefinition.FromEmbeddedEmote(name, loop: true) otherwise
```

So `config.Emote` can now be any marketplace emote URN → artists pick poses from the
**Emotes / Poses** browser tab. Embedded set remains: `idle, clap, dab, dance, fashion,
fashion-2..4, love, money, fist-pump, head-explode`.

Pose freezing: play-mode transport buttons call `PreviewController.PlayEmote/PauseEmote/
StopEmote/GoToEmote(seconds)`; the scrub slider's `highValue` is polled from
`GetEmoteLength()` every 500 ms.

## 7. Share code & presets (outfit reproducibility)

`OutfitDefinition` = `{ bodyShape URN, urns[], skinColor, hairColor, eyeColor, emote }`.

- `ToShareCode()` emits `?mode=builder&bodyShape=...&urn=...&urn=...&skinColor=RRGGBB&...&emote=...`
  — **intentionally identical to `AangConfiguration`'s builder query-string format**, so a share
  code works as `Bootstrap.debugUrl`, as URL params on the deployed web renderer, and back into
  the tool. Do not diverge from that format.
- `FromShareCode()` is a tolerant parser (accepts full URLs, ignores unknown params) — kept
  separate from `AangConfiguration.RecreateFrom` because that mutates the global singleton and
  `APIService.Environment`.
- `OutfitPreset` (ScriptableObject, `CreateAssetMenu` "Decentraland/Outfit Preset") wraps an
  `OutfitDefinition` for named local presets; window has Load / Save / Save As via
  `AssetDatabase`.

## 8. Capture

`OutfitCapture` (static, editor-only):

**Still** — renders `Camera.main` into an arbitrary-resolution `RenderTexture` (independent of
Game view size) via `RenderPipeline.StandardRequest` + `SubmitRenderRequest` (URP-correct;
falls back to `camera.targetTexture + Render()`), `ReadPixels` → `EncodeToPNG` →
`File.WriteAllBytes`. Transparent background = temporarily set solid-color clear with alpha 0
(URP preserves alpha — same mechanism the WebGL transparent template relies on). Runtime UI
Toolkit overlays don't render through the camera, so stills are automatically clean.

**Video** — Unity Recorder driven programmatically: `RecorderControllerSettings` +
`MovieRecorderSettings` with `CoreEncoderSettings { Codec = MP4, Quality = High }` and
`GameViewInputSettings { OutputWidth/Height }`, manual record mode, `FrameRate` capped.
Caveat: Game view capture **includes runtime UI overlays** (builder-mode zoom buttons etc.).

Three video flows in the window:
- Manual **Start/Stop**.
- **Record Emote**: `pc.PlayEmote()` + auto-stop scheduled at `GetEmoteLength() + 0.5s`.
- **Record Turntable**: `TurntableDriver` added to the avatar's `DragRotator` GameObject —
  disables the `DragRotator` while active (they'd fight over the transform), rotates exactly 360°
  over `Duration`, restores rotation, fires `Completed` → stop recording. Re-enable pattern:
  `driver.enabled = false; configure; subscribe; StartVideo; driver.enabled = true`
  (OnEnable resets its state).

Output: `Captures/` **next to the project root** by default (outside `Assets/` so Unity doesn't
import the files); absolute paths allowed. Filenames `outfit_yyyyMMdd_HHmmss`.

## 9. Window internals worth knowing

(`OutfitStudioWindow.cs`)

- **State survival**: outfit + capture settings are `[SerializeField]` fields → survive domain
  reload / play transitions. Browser results and `_knownItems` (urn → CatalogItem) are session
  state, re-hydrated via §5 URN lookup in `CreateGUI`/`LoadOutfit`.
- **Slot semantics**: `outfit.urns` is a flat URN list (matches the renderer). The window enforces
  one-per-slot at click time by removing any URN whose *known* item shares the clicked item's
  `Slot`. Unknown URNs can't be checked — the renderer's own slot-dedup (last-in-list wins) is the
  backstop, and picks are appended so they win.
- **Body-shape guard**: `FilterForBodyShape()` drops known wearables lacking a representation for
  the selected shape at apply time, with a status warning. Without this,
  `GLTFLoader.LoadModel` → `EntityDefinition[bodyShape]` **throws** and breaks the whole load.
  Unknown URNs pass through unchecked.
- **Auto-apply**: debounced 400 ms via `IVisualElementScheduledItem` (`_pendingApply.Pause()` +
  reschedule). Only fires in play mode with the toolbar toggle on.
- **Search**: debounced 500 ms; out-of-order responses discarded via `_searchSequence` counter.
- **Safety hooks**: `ExitingPlayMode` → `OutfitCapture.StopVideo()`.

## 10. Known caveats / future work

- **Recorder version**: `com.unity.recorder 5.1.2` — if Unity can't resolve it, bump/adjust in
  `Packages/manifest.json`; the API used (`EncoderSettings`, `CoreEncoderSettings`,
  `GameViewInputSettings`) is Recorder 4.0+.
- Game-view video includes runtime UI overlays; a "hide UI during recording" toggle (disable
  `UIDocument` components temporarily) is an easy v2.
- `/v1/items` response fields were implemented from the documented schema; if the grid comes up
  empty with a successful request, diff the live JSON against `CatalogModels.cs` first.
- Docked 3D preview inside the window (RenderTexture) — deferred; the Game view is the viewport.
- Possible v2s: load-outfit-from-profile (via `APIService.GetAvatar`), multi-rarity filters,
  transparent-background video (WebM), smart-wearable filtering, preset thumbnails.
- The base-avatar (off-chain) wearables can't be browsed — the marketplace API only serves
  collection items. Artists get default body parts unless they equip marketplace items; browsing
  base-avatars would need the catalyst entities endpoint instead.

## 11. Edit-mode 3D preview (iteration 2)

Outfit selection previews **without play mode**: `EditModeAvatarPreview.Apply(outfit, status)`
assembles the outfit onto the Preview rig's scene skeleton in edit mode. The window routes
`Apply()` here whenever `!Application.isPlaying`; the play-mode path is unchanged. Play mode is
still required for emote playback and capture.

**Why it works** (verified in code): `AvatarUtils` is pure component logic; the load path
(`GLTFLoader`/`BinaryDownloadProvider`) only awaits `UnityWebRequest` + `Task.Yield()` (pumps on
the editor sync context, no frame waits); glTFast's `IsEditorImport` gate is irrelevant for
GLB-embedded textures (`CreateTexturesFromBuffers` ignores it).

**Edit-mode blockers and their fixes:**
1. `CommonAssets.AvatarMaterial/FacialFeaturesMaterial` normally set in `Bootstrap.Start` →
   `EnsureEditModeSetup()` reads Bootstrap's serialized `baseMat`/`facialFeaturesMat` via
   `SerializedObject`.
2. glTFast's lazy default defer agent doesn't tick in edit mode →
   `GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent())` before first load.
3. `GLTFLoader.Sanitize` used `Object.Destroy` (illegal in edit mode) → patched to
   `DestroyImmediate` when `!Application.isPlaying` (play mode/builds byte-identical).
4. `AvatarLoader` is play-mode-only (`Destroy` in its reload diff) → NOT reused. The preview
   keeps its own `urn → LoadedModel` dicts and destroys with `DestroyImmediate`. Assembly mirrors
   `AvatarLoader.LoadAvatar` post-load steps (AvatarLoader.cs:149-169): slot dedup +
   `HasRepresentation` skip → `HideWearables` → load body+wearables (`GLTFLoader.LoadModel`) and
   facial features → `HideBodyShape` → `SetupFacialFeatures` (the defaults dict self-populates on
   first call) → per-model `SetupWearable(go, colors, _, avatarRootBone, avatarBones)`.

**Scene skeleton access:** private serialized fields are read via `SerializedObject` —
`PreviewController.avatarLoader` → `AvatarLoader.avatarRootBone/avatarBones/avatarAnimation`.
No renderer API changes needed.

**Lifecycle & safety:**
- The Preview rig is saved **inactive** in `Main.unity` (Bootstrap activates it at play time) —
  `EnsureActiveInHierarchy` activates the rig/skeleton ancestor chains for the preview and
  `Clear()` restores the original inactive state (so Bootstrap keeps owning play-time state).
- Preview roots get `HideFlags.DontSave` recursively — never saved into the scene.
- All preview objects live under a `__OutfitStudio_EditPreview` container. The tracking dicts are
  static and die on **domain reload** while the objects survive in the scene; the container makes
  those orphans discoverable — `Apply` sweeps it when the dicts are empty, and `Clear()` does a
  name-based sweep across all `AvatarLoader`s. Without this, applies after a recompile stack
  duplicate meshes. `[InitializeOnLoad]` re-registers the cleanup hooks on every reload.
- `Clear()` runs automatically on `ExitingEditMode` (runtime loader always starts clean) and on
  scene closing; also exposed as the toolbar **Clear Preview** button. A body-shape change
  mid-apply calls `Clear()` then re-arms its own sequence + re-activates the rig.
- Overlapping async applies are serialized with a sequence counter (stale loads are disposed).
- Pose: samples the skeleton's "Idle" clip at t=0 (`SampleIdlePose`) so the avatar isn't in bind
  pose. This moves scene bones and may mark the scene dirty — harmless.

**Limitations:** static idle pose; no spring bones, outline, or emotes in edit mode; glTFast caps
skin weights to 4 outside play mode (minor deformation differences possible). Play mode is ground
truth for capture.

## Status as of 2026-07-14

**Added 2026-07-14, not yet verified:** thumbnail fix (blank thumbnails when revisiting a
browser page — cached textures were delivered synchronously and dropped by a panel-attachment
guard); Debug tab + Clean View (§12).

## Status as of 2026-07-13 (commit cd2afbb)

**Verified working (by Mauricio):**
- Catalog browsing (search/filters/pagination, ~429 items), equipping into slots, share-code UI.
- Play-mode flow: outfit loads via builder mode, live re-apply while picking items.

**Fixed late in the session — needs re-verification:**
- Edit-mode preview visibility: the Preview rig is saved inactive in the scene; fixed via
  `EnsureActiveInHierarchy` + restore-on-Clear. Not yet confirmed after the fix.
- Mesh stacking on same-slot swaps: root cause was domain-reload orphans (see §11 lifecycle);
  fixed via the `__OutfitStudio_EditPreview` container + sweeps. **First action after checkout:
  press Clear Preview once (or just Apply) to sweep any orphans left in the open scene.**
- `EntityService` assertion removed — unresolved entities (likely a third-party/linked wearable)
  now warn + skip instead of failing the load. The offending URN was never identified; if it
  reappears, the console lists it (`[OutfitStudio] Could not resolve entities for: ...`).

**Not yet tested at all:**
- Capture: Still / Start-Stop Video / Record Emote / Record Turntable (Recorder package installed,
  code untested — expect possible API/version friction on first run).
- Presets (save/load/save-as), Load from share code, body-shape switching, `dev (zone)` env,
  emote-URN poses via the Emotes/Poses tab, emote scrubbing.

## 12. Debug tab & Clean View (iteration 3)

The renderer auto-shows its built-in debug overlay in editor play mode (`PreviewUIPresenter.
OnEnable` → `EnableDebug()` when `Application.isEditor`; unlocked in builds by typing
`debugmesilly` — that gating is untouched). The window replicates that functionality in a
third **Debug** tab and hides the overlay for a clean, avatar-only Game view:

- **Debug tab** (`BuildDebugPane`): JSBridge method dropdown (same reflection as the overlay:
  `typeof(JSBridge).GetMethods(DeclaredOnly|Public|Instance)`) + Parameter + Invoke with the
  identical auto-Reload rule (skip for `Reload`/`TakeScreenshot`/`Cleanup`); URL presets from
  `PreviewUIPresenter.DEBUG_URL_PRESETS`; Print Config (logs + fills a read-only field);
  Random Profile (`SetProfile("default"+Random(1,160))`); Zoom In/Out via
  `PreviewCameraController.ZoomIn/ZoomOut`. All actions require play mode (status warning
  otherwise) and go through `SendToJSBridge` (`GameObject.Find("JSBridge").SendMessage`).
- **Clean View** (toolbar toggle, default ON): a 500 ms scheduled loop
  (`EnforceCleanGameView`) hides `DebugPanel`, `ZoomControls`, `Switcher`, `EmoteControls`
  on the runtime UIDocument while playing. Re-enforcement is needed because
  `PreviewController.Reload()` re-enables controls after every load (so the overlay may flash
  briefly post-reload). **Important:** the `Controls` element itself must NEVER be hidden —
  it carries the `DragManipulator` for mouse rotation; only its child widgets are hidden.
  The loader spinner stays visible. Toggling Clean View off restores the debug panel
  (editor-only, mirroring the presenter) and triggers a `Reload` so `PreviewController`
  re-applies the mode-dependent control visibility.

## 13. Verification checklist (first run after checkout)

1. Focus Unity (project open on `feat/outfit-studio`) → Recorder package installs, scripts
   compile, `.meta` files generate for `Assets/OutfitStudio/`.
2. **Decentraland ▸ Outfit Studio** → search "jacket", slot filter `upper_body` → grid populates.
3. Equip items → **▶ Enter Play** → avatar assembles in Game view; further clicks hot-swap.
4. Capture Still (transparent on/off), Record Emote, Record Turntable → files in `Captures/`.
5. Copy share code → Load from code → identical avatar. Same string pasted into
   `Bootstrap.debugUrl` reproduces it without the window.
