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
  6. `Assets/Scripts/AangConfiguration.cs` (+`Assets/Scripts/Preview/PreviewController.cs`) —
     added `bool EmoteLoop` (default **false**) and passed it to `LoadForProfile` in the
     Profile/Authentication case (`LoadForProfile(config.Profile, config.Emote, config.EmoteLoop)`).
     Lets the studio hold a single-frame pose on a profile avatar (§17). Prod default false =
     unchanged; only the Outfit Studio sets it true.

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
│   ├── TurntableDriver.cs         # deterministic 360° spin MonoBehaviour
│   └── MatcapPresets.cs           # LOCAL COPY of the package type — delete at integration (§16)
├── Shaders/
│   ├── Matcaps/                   # 6 matcap PNGs + MatcapPresets.asset (local copies, §16)
│   ├── DCL_Toon_Studio/           # unlocked copy of the metallic-branch DCL_Toon (§16)
│   ├── DCL_Stylized_PBR/          # new Disney-principled stylized PBR shader (§16)
│   └── StudioCardFrame/           # unlit shader for the Fortnite-style card frame (§18)
└── Editor/
    ├── OutfitCapture.cs           # still (RenderPipeline request) + video (Unity Recorder)
    ├── EditModeAvatarPreview.cs   # edit-mode outfit assembly on the scene skeleton (§11)
    ├── StudioAvatarShaderSwitcher.cs # 3-way shader enforcement + matcap bootstrap (§16)
    ├── StudioCardFrame.cs         # camera-parented card-frame quads (bg/card/fade) (§18)
    └── OutfitStudioWindow.cs      # the EditorWindow (all UI + orchestration)
```
(Plus `Editor/StudioSceneOverlayHider.cs`, `Editor/StudioRenderPipelineSwitcher.cs`,
`Editor/BuilderIdentity.cs`, `Editor/BuilderCollectionService.cs`, `Editor/Plugins/` (vendored
DLLs), `Scenes/OutfitStudio.unity`, `Settings/URP_Asset_Studio.asset` — see §13/§14.)

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

## Status as of 2026-07-15

**New (iteration 5, untested in-editor):** the 3-way shader switcher (§16) — Shader section in
the outfit pane, `DCL_Toon_Studio` (metallic-branch copy + rim/lights/GI unlocks),
`DCL_Stylized_PBR` (new Disney-principled stylized PBR), `StudioAvatarShaderSwitcher`
enforcement, and the verbatim `ToonMaterialGenerator`/`CommonAssets` metallic port + local
`MatcapPresets`. First editor focus needs to compile both shaders — run §16's verification
list. Everything from the 2026-07-14 status below still applies.

## Status as of 2026-07-14 (end of day)

**Verified working by Mauricio on 2026-07-14:**
- Edit-mode 3D preview (after the inactive-rig + orphan-sweep fixes), thumbnail pagination fix,
  Debug tab + Clean View (§12).
- **Load from Collection (§13) — confirmed working end-to-end**, including the signed
  builder-api access (identity paste + auth-chain signing) and equipping draft items via base64.

**Not yet tested:** capture paths (Still / Video / Record Emote / Turntable — Recorder 5.1.2
installed, code unexercised), presets save/load, share-code load round-trip, body-shape
switching, `dev (zone)` env, emote-URN poses from the Emotes/Poses tab.

**In progress:** dedicated studio scene (§14) — raw copy + menu shortcut + overlay hider +
studio render pipeline (per-pixel additional lights for set geometry) all in place; Mauricio is
mid set-dressing (own CinemachineCamera added; stock vcams to disable, Configurator to strip).
Target look = Fortnite-style item cards; see §14 for the lighting findings (avatar = 1
directional only, rim compiled off) and the planned shared-dependencies shader session
(rim promotion + optional additional lights), to be bundled with the metallic integration.
Untried: the back-key + post-lift interim recipe; the studio renderer-data duplicate for
tweakable gradient background colors (Route A).

## Metallic/normals branch — integration options (deliberately NOT done yet)

The normals + stylized-metallic matcap work lives on `feat/avatar-toon-shader-metallic-normals`
and is intentionally kept OFF this branch. Facts for whenever we want to see it in the Outfit
Studio (verified 2026-07-14):

- The shader HLSL lives in the **`unity-shared-dependencies` package**; the metallic branch
  repoints the package to `#feat/toon-normalmap-stylized-metallic` (NOT merged to the package's
  main) via `Packages/manifest.json` + lock.
- Aang-side changes committed on that branch (diff vs main): `Assets/Scripts/Loading/
  ToonMaterialGenerator.cs` (+123 — maps GLB normal/metallic data + matcaps into DCL_Toon),
  `Bootstrap.cs` + `CommonAssets.cs` (+9 each — matcap preset wiring), `Assets/Scenes/Main.unity`
  (serialized refs), `Assets/StreamingAssets/character/PuffyJacket.glb` (test asset).
- The `git stash` ("WIP metallic/normals test harness") holds only the optional
  `LocalWearableOverride` local-GLB harness — NOT required to see the effect.
- **Repointing the package alone is not enough** — the `ToonMaterialGenerator` changes are what
  feed the new shader features.

**Update 2026-07-15 (iteration 5, §16):** the `ToonMaterialGenerator` + `CommonAssets` diffs
have now been ported onto THIS branch verbatim (prod-safe — the stock package shader ignores
the extra properties), and the metallic shader itself was copied locally into
`DCL/DCL_Toon_Studio`. Remaining "integration" with the metallic branch is therefore only the
package repoint (option 1/2 below) plus deleting the local duplicates flagged in §16.

Options, in recommendation order:
1. **Integration branch** — `feat/outfit-studio-metallic` = this branch + `git merge
   feat/avatar-toon-shader-metallic-normals`. Both source branches stay pure for upstream PRs.
   Expect one small conflict in `manifest.json`/`packages-lock.json` (keep BOTH the
   `com.unity.recorder` line and the shared-deps branch repoint). Bonus: metallic draft
   wearables can then be QA'd through Load from Collection.
2. **Merge metallic into `feat/outfit-studio`** — simplest daily workflow, but entangles the
   histories (outfit-studio PR would carry the shader work).
3. **Working-tree quick look (no commits)** — `git checkout
   feat/avatar-toon-shader-metallic-normals -- <the files listed above>`; revert with
   `git restore`. Minutes to see, fragile to keep (don't commit the manifest/lock repoint).

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

## 13. Load from Collection (iteration 4)

Debug-tab section mirroring the explorer's `--self-preview-builder-collections` flag: paste a
collection ID → **Load** → paginated grid → click to equip.

**Two ID kinds:**
- **`0x` contract address (published)** — unauthenticated `marketplace-api /v1/items?
  contractAddress=...` via `CatalogService` (`CatalogQuery.ContractAddress`), server-paged.
  Tiles reuse the normal URN equip flow.
- **UUID (draft/unpublished)** — `GET builder-api.decentraland.{env}/v1/collections/{id}/items`
  (`BuilderCollectionService`), which **requires a signed auth chain**. Whole collection returned
  at once; client-side paging (24/page).

**Auth (drafts):** `BuilderIdentity` — the user pastes their Decentraland identity JSON from
builder.decentraland.org localStorage (parser is tolerant: finds the
`ephemeralIdentity/expiration/authChain` object anywhere in the pasted JSON, including
stringified nesting). Stored in **EditorPrefs only** (`OutfitStudio.BuilderIdentity`) — the
ephemeral private key must never reach project files or logs. Signing mirrors the explorer
exactly (unity-explorer refs: `WebRequestSignInfo.NewFromRaw`, `WebRequestHeadersInfo.WithSign`,
`RequestEnvelope.SignRequest`, `DecentralandIdentity.Sign`, `NethereumAccount.Sign`):
string-to-sign `"{method}:{path}:{unixMs}:{metadata}"` lowercased (metadata `{}`), personal-sign
with the ephemeral key (`EthereumMessageSigner.EncodeUTF8AndSign`), headers
`x-identity-auth-chain-{i}` (stored chain + appended `ECDSA_SIGNED_ENTITY` link),
`x-identity-timestamp`, `x-identity-metadata`.

**Crypto DLLs:** `Editor/Plugins/` vendors Nethereum.Signer + Hex/RLP/Util/Model +
BouncyCastle + Microsoft.Extensions.Logging.Abstractions, copied from
`unity-explorer/Explorer/Assets/Plugins/Nethereum/net472UnityCommonAOT` (aang's
`apiCompatibilityLevel: 6` = .NET Framework, so the net472 builds match). Their `.meta` files
are hand-written with **editor-only** PluginImporter settings — verify Nethereum never appears
in a WebGL build report.

**Equipping drafts — the renderer's base64 mechanism, zero renderer changes:**
`BuilderCollectionService` converts each draft item into a `RawActiveEntity` JSON
(`Assets/Scripts/Data/RawActiveEntity.cs` shape; representation contents → `{key, url}` against
the public `.../v1/storage/contents/{hash}`; emotes under `emoteDataADR74` with `data` omitted
since `IsEmote` keys off empty `data.category`), base64-encoded into
`OutfitDefinition.base64Items`. Play mode: `Apply()` fills `AangConfiguration.Base64` →
`LoadForBuilder` gives base64 per-category priority; a base64 emote overrides the pose.
Edit mode: `EditModeAvatarPreview` parses them via `EntityDefinition.FromBase64` into the slot
dict (emotes skipped — static pose). Share codes carry drafts as `&base64=` params
(`Uri.EscapeDataString`-escaped because `HttpUtility.UrlDecode` would eat `+`); they round-trip
through `Bootstrap.debugUrl` and the web renderer. Draft-vs-catalog slot conflicts are resolved
on equip (both lists purged for the category); picking an embedded/catalog emote removes any
draft emote (which would otherwise take priority).

## 14. Dedicated studio scene

`Assets/OutfitStudio/Scenes/OutfitStudio.unity` is a **copy of Main.unity** (fresh GUID, never
in Build Settings → zero build impact) meant to become the "studio set": custom backdrop,
lighting and post-processing for beauty shots, authored with the normal scene workflow. Open it
via **Decentraland ▸ Open Outfit Studio Scene**. The tool is scene-agnostic (all lookups are
`FindFirstObjectByType`), so it works identically in either scene.

**Stripping rules (as of the copy, still un-stripped):** only the **Configurator branch** (its
rig, second `AvatarLoader`, cameras, UI) is safe to delete. Everything else in the Preview
branch must STAY even if unused, because `PreviewController.Reload()` dereferences it
unconditionally every reload: `wearableLoader`, `confirmationVFX`, `animationReference`,
`platform`, `previewUIPresenter`. Also keep the `UI` GameObject — mouse-drag rotation runs
through its `DragManipulator` (Clean View hides its visuals anyway). Lighting/post are free:
replace the directional light, add a global URP `Volume`, enable post-processing on the camera
directly in the scene.

**Overlay in the studio scene:** `StudioSceneOverlayHider` ([InitializeOnLoad], cadence like
Clean View) force-hides `DebugPanel/ZoomControls/Switcher/EmoteControls/Loader` whenever the
studio scene is active — edit and play mode, window open or not. The `UI` GameObject must stay
alive (PreviewController dereferences the presenter unconditionally; drag rotation runs through
the panel).

**Cameras in the studio scene:** keep the real `Main Camera` (with `CinemachineBrain`) and the
`PreviewCameraController` component — `PreviewController.Reload()` calls `SetMode()`
unconditionally, and it `Prioritize()`s its serialized vcams every reload. To use a custom
CinemachineCamera: **disable (don't delete) the stock vcam GameObjects** (authProfile/
marketplaceWearable/marketplaceAvatar/builder/jesus) — disabled vcams don't compete in the
brain, so the custom camera wins regardless of priority, and `Prioritize()` on a disabled vcam
is a harmless field write. Deleting them NREs `SetMode`.

**Studio lighting — IMPORTANT limitation:** the avatar's shader (`DCL/DCL_Toon`) is lit by
**exactly one light — the main directional**. Its additional-lights loop is **commented out**
in the fragment code (`DCL_ToonBodyDoubleShadeWithFeather.hlsl:~304`, package
`unity-shared-dependencies`; `CalculateAdditionalLightingColour` in `DCL_ToonLighting.hlsl` is
dead code). Point/spot lights do NOT affect the avatar regardless of URP settings. Ambient/GI
is also compiled out (`#define _GI_Intensity 0.0f` in `DCL_ToonVariables.hlsl`); ambient only
leaks via the SH fallback light color when no main light is present.

`Assets/OutfitStudio/Settings/URP_Asset_Studio.asset` (Additional Lights **Per Pixel / limit 8**,
same renderer data + volume profile by GUID) + `StudioRenderPipelineSwitcher`
([InitializeOnLoad], overrides `QualitySettings.renderPipeline` while the studio scene is
active, restores on leave) therefore benefit the **set geometry/props only** (standard URP
shaders take per-pixel spots/points + shadows). Caveat: saving the project *while in the studio
scene* diffs `ProjectSettings/QualitySettings.asset`; self-reverts after leaving + saving —
don't commit.

Avatar light-rig reality: 1 directional (color/intensity/angle = the cel bands), set/post for
mood. The shader's built-in rim light is compiled to near-invisible
(`DCL_ToonVariables.hlsl`: `_RimLight 1.0` but `_RimLight_Power 0.3`,
`_Tweak_RimLightMaskLevel -0.9` — compile-time constants, not tweakable from materials).

**Goal look (Fortnite-style item cards):** gradient background + glow (the renderer's
`BackgroundRendererFeature` ships exactly this — for studio-only tweakable colors, duplicate
the renderer data asset and point `URP_Asset_Studio.m_RendererDataList` at the copy), bloom on
emissives, and a strong cool **top-back rim light** — which is NOT currently reproducible as
light (no additional lights, rim compiled off). Interim technique that works today: use the
single directional AS the back/rim key (top-behind, cool tint → the toon lit band becomes the
rim) and lift the front with post (Shadows/Lift, warm) — see the 2-Ball reference; its front is
mid-dark too.

**Shader session — DONE LOCALLY (2026-07-15, iteration 5, see §16):** all three unlocks (rim
promotion, additional-lights loop, `_GI_Intensity`) now exist in the local
`DCL/DCL_Toon_Studio` shader copy under `Assets/OutfitStudio/Shaders/`. A future
`unity-shared-dependencies` session is now only about **upstreaming** that promotion diff into
the package (the studio copy is the reference implementation).

**Sync ritual:** this copy does NOT receive upstream `Main.unity` changes. After merging main
into the branch, if avatar loading/rig behavior changed: `git diff <old>..<new> --
Assets/Scenes/Main.unity` and re-apply relevant changes (or re-copy Main and re-strip/re-dress).
Renderer *script* changes flow automatically — only scene-serialized wiring drifts.

## 15. Verification checklist (first run after checkout)

1. Focus Unity (project open on `feat/outfit-studio`) → Recorder package installs, scripts
   compile, `.meta` files generate for `Assets/OutfitStudio/`.
2. **Decentraland ▸ Outfit Studio** → search "jacket", slot filter `upper_body` → grid populates.
3. Equip items → **▶ Enter Play** → avatar assembles in Game view; further clicks hot-swap.
4. Capture Still (transparent on/off), Record Emote, Record Turntable → files in `Captures/`.
5. Copy share code → Load from code → identical avatar. Same string pasted into
   `Bootstrap.debugUrl` reproduces it without the window.

## 16. Shader switcher & studio shaders (iteration 5, 2026-07-15)

A "Shader" section at the top of the outfit pane with 3 selector buttons (always visible for quick
access); the per-shader tuning panel below them is tucked into a collapsible **"Shader Settings"**
foldout (2026-07-17, matching the Card frame section). The selection persists (EditorPrefs
`OutfitStudio.Shader`) and is enforced on every avatar material in edit AND play mode, across
reloads, until another shader is picked. Studio-scene-gated — outside
`OutfitStudio.unity` nothing is touched.

| Button | Shader | What it is |
|---|---|---|
| DCL_Toon | `DCL/DCL_Toon` | Stock package shader — the official look, untouched. |
| DCL_Toon_Studio | `DCL/DCL_Toon_Studio` | Local unlocked copy (see below). |
| DCL_Stylized_PBR | `DCL/DCL_Stylized_PBR` | New Disney-principled stylized PBR (see below). |

### Live tuning panel (art direction)
Below the 3 buttons the outfit pane shows sliders/color fields for the **selected** shader
(stock `DCL_Toon` has none — it's the fixed official look). The knob list is defined ONCE in
`StudioAvatarShaderSwitcher` (`StudioKnobs` / `PbrKnobs`, a `StudioShaderKnob[]`) and is the
single source of truth: the window builds the UI from it, and `Apply()` pushes the values onto
every active-shader avatar material each poll + immediately on change. Values persist in
EditorPrefs keyed `OutfitStudio.Knob.{modeIndex}.{property}` (rim power for toon vs PBR are
independent entries). "Reset shader defaults" clears the current shader's keys.

Knobs are **global look controls** (rim, ambient, stylization) — deliberately not per-wearable
identity (textures/base color/gates are left alone). `_BumpScale` and `_StylizedMetalStrength`
are the exceptions: they override the per-wearable value with a global one (fine for a debug
tool; tooltip says so). A dedicated `_RimLightIntensity` scalar was **added to both studio
shaders** (neither had a rim-strength multiplier — rim was color+power only): in
`DCL_Toon_Studio` it scales `Set_RimLight` in the composition; in `DCL_Stylized_PBR` it scales
the fresnel rim term. Toon Studio knobs: rim intensity/power/mask/color, ambient GI, normal
strength, metal strength, matcap tint, matcap blur. PBR knobs add: rim sharpness, diffuse wrap,
shadow sharpness, specular softness, specular F0, sheen (+tint), clearcoat (+gloss), matcap
metal blend, metal strength, emission strength, matcap tint, matcap blur. Above the sliders both
studio shaders show a **Matcap dropdown** (the reflection texture; see the 2026-07-16 update).
Matcap blur is capped 0–4. (Metal strength/blend, emission strength, and the dialed-in default
look values: see the iteration-6 update at the end of §16.)

### How switching works — `Editor/StudioAvatarShaderSwitcher.cs`
Poll-based (`[InitializeOnLoad]`, 0.5 s on `EditorApplication.update`, ticks in play mode too —
same pattern as the overlay hider / pipeline switcher). Every avatar reload creates fresh
material clones with the stock shader; the next tick scans every renderer in the studio scene via
`Resources.FindObjectsOfTypeAll<Renderer>()` (filtered to the active scene) and acts on any whose
`sharedMaterial.shader.name` is one of the three avatar shaders. Important: it must NOT use
`FindObjectsByType<Renderer>` — that skips `HideFlags.DontSave` objects, and the edit-mode preview
builds its avatar with exactly that flag, so the scan would find zero renderers in edit mode.
`Resources.FindObjectsOfTypeAll` returns DontSave/inactive/hidden objects too, so it's independent
of avatar hierarchy and covers play-mode wearables the same way. If the target shader can't be
resolved (compile error / not imported) it logs one warning and skips; on a button click it logs
the outcome (materials found / swapped) so a no-op is never silent — `0 avatar materials` means no
outfit is loaded into the preview yet. **Swap mechanics:** named properties survive by name;
`renderQueue` resets on shader assignment (the generator sets it for cutout/transparent
wearables) and keywords are restored defensively — both saved/restored around the swap.
Materials are filtered by shader name (one of the 3 above), which naturally excludes
`DCL/DCL_Avatar_Facial_Features` (eyes/brows/mouth stay stock in all modes) — plus an
`EditorUtility.IsPersistent` guard so `Avatar_Toon.mat` can never be dirtied. In PBR mode the
carried `_GI_Intensity 0` is nudged to 1 once per swap (toon compiles ambient off; PBR needs it).

**Outline contract:** `AvatarUtils` collects outline renderers by `shader.name ==
"DCL/DCL_Toon"` at LOAD time (always before our first swap — materials are born stock), and the
outline feature draws each renderer's *current* material via `FindPass("Outline")`. So no
renderer change was needed — but **every switchable shader MUST have a pass named "Outline"**
(both new shaders do; the PBR one is gated by its `_OutlineEnabled` toggle).

### DCL_Toon_Studio — `Shaders/DCL_Toon_Studio/`
Copied from the **metallic branch** of `unity-shared-dependencies` (local clone
`/mnt/d/GIT/unity-shared-dependencies` @ `feat/toon-normalmap-stylized-metallic` = `9eda18fb`,
the exact commit the aang metallic branch pins) — so normals + stylized matcap metallic are
included without waiting for that branch to merge. Edits on top of the copy:
- Renamed `Shader "DCL/DCL_Toon_Studio"`, dropped the package-bound `CustomEditor` line, fresh
  .meta GUIDs everywhere (never reuse package GUIDs).
- **Promoted from compile-time constants to material properties** (the branch's own
  `_MatCapColor`/`_BlurLevelMatcap` promotion was the template; CBUFFER + DOTS-instancing
  mirrors in `DCL_ToonInput.hlsl`): `_RimLight`, `_RimLight_Power`, `_RimLight_InsideMask`,
  `_RimLight_FeatherOff`, `_Is_LightColor_RimLight`, `_Tweak_RimLightMaskLevel`,
  `_RimLightColor`, `_GI_Intensity`, `_BumpScale`, `_StylizedMetalStrength` (was a local
  const). `Avatar_Toon.mat` already serialized the old constant values, so the default look is
  IDENTICAL until you tweak — the promotion just makes the knobs live (the Fortnite-card rim!).
- **Re-enabled the UTS additional-lights loop** in `DCL_ToonBodyDoubleShadeWithFeather.hlsl`
  (was commented out pending a Forward+ rework). Its helpers were all still live; the
  referenced shade-map/high-color textures do NOT exist in this trimmed DCL variant, so the
  loop body was stubbed exactly like the main-light path (base-as-shademap, masks = 1). The
  studio pipeline (classic Forward, per-pixel additional lights) uses the `UTS_LIGHT_LOOP`
  path; spot/point lights now hit the avatar with banded UTS-style cel additions (that's the
  point — it's stylized, not physical).

### DCL_Stylized_PBR — `Shaders/DCL_Stylized_PBR/`
New hand-written URP shader (`.shader` + `_Input.hlsl` + `_ForwardPass.hlsl`). The OW2 GitHub
reference was inspected and discarded (Built-in RP, plain Unity Standard + texture packing);
the model is instead the **Disney Principled BRDF** (Burley SIGGRAPH 2012 — the
parameterization Unreal/Fortnite shading derives from), implemented fresh:
- Burley diffuse with retro-reflection, over a stylization layer: `_DiffuseWrap` +
  `_ShadowSharpness` (wrapped, smoothstep-sharpened falloff).
- GGX + height-correlated Smith specular; Disney `_Specular` F0 scale for dielectrics;
  `_SpecularSoftness` compression for the broad stylized gleam.
- `_Sheen`/`_SheenTint` (cloth edge gleam) and `_Clearcoat`/`_ClearcoatGloss` (GTR1 lobe —
  the glossy "action figure" finish).
- Metallic from `_MetallicGlossMap.b`, roughness from `.g` (glTF ORM, same convention as the
  toon metallic work; `_Metallic`/`_Smoothness` scalars as fallback when no map).
- Additional lights (Forward and Forward+), SH ambient via the shared `_GI_Intensity`, artist
  rim on the shared `_RimLight*` names (same exponent mapping as toon so carried values feel
  familiar, plus `_RimSharpness`), **matcap as environment reflection for metals** (SH
  fallback), emission ×2.5 to match toon's magic number, same `_IS_CLIPPING_*` dynamic-branch
  clipping contract, and ShadowCaster/DepthOnly/DepthNormals passes with alpha clip.
- `_OutlineEnabled` toggle (default on) on the mandatory inverted-hull "Outline" pass.
Property names match DCL_Toon everywhere they overlap — switching is lossless in both
directions.

### Renderer touches (prod-safe, documented)
- `Assets/Scripts/Loading/ToonMaterialGenerator.cs` + `Assets/Scripts/CommonAssets.cs`: the
  metallic branch's diffs applied VERBATIM (`git diff main...feat/avatar-toon-shader-metallic-
  normals` on those two files applies cleanly — keep it that way for a trivial future merge).
  Feeds GLB `normalTexture` → `_NormalMap`, `metallicRoughnessTexture`/`metallicFactor` →
  `_MetallicGlossMap`/`_IsStylizedMetallic`, matcap from `CommonAssets.MatcapPresets`. The
  stock package shader ignores all of these properties → play/WebGL behavior unchanged.
- Bootstrap/Main.unity NOT touched: `StudioAvatarShaderSwitcher` assigns
  `CommonAssets.MatcapPresets` (from `Shaders/Matcaps/MatcapPresets.asset`) +
  `DefaultMatcapName = "matcap_01"` on its poll instead.

### ⚠ Delete-at-integration tripwire
`Runtime/MatcapPresets.cs` is a verbatim copy of the package type (same namespace
`DCL.Rendering.DCL_Toon`, kept identical so the ported generator code needs zero edits). When
the metallic branch merges into the package and the package is repointed/updated, the duplicate
type will fail compilation with **CS0433** — that's the intentional signal to: delete
`Runtime/MatcapPresets.cs` + `Shaders/Matcaps/`, wire Bootstrap per the metallic branch, and
optionally delete `Shaders/DCL_Toon_Studio/` in favor of upstreamed unlocks.

### Verification (not yet run — needs the editor)
1. Focus Unity → both new shaders compile, no CS errors.
2. Studio scene, edit mode: apply an outfit → each button swaps all body/wearable renderers
   within ~0.5 s; facial features unaffected; 2→1 and 3→1 restores look pixel-identical
   (check hair alpha-clip edges / transparent wearables → renderQueue restored).
3. Persistence: re-apply outfit / change body shape / enter play / restart editor → selection
   re-applies.
4. Studio mode: `_RimLight_Power` etc. live-tweakable on a material instance; spot/point
   lights affect the avatar; `_GI_Intensity > 0` lifts ambient; a metallic wearable shows
   matcap metal.
5. PBR mode: normals shade; metallic masks specular; clipped hair correct in shadows/depth;
   outline toggle works.
6. Prod safety: no diffs on `Avatar_Toon.mat` / `Main.unity` / `Bootstrap.cs` / manifest.

### Update 2026-07-16 (iteration 6) — stylized-metal fixes + matcap controls (CONFIRMED working)

First real in-editor test of stylized metallic via **Load from Collection** (draft PuffyJacket,
the same asset QA'd in unity-explorer). Normals rendered but metal didn't; three fixes landed,
all in studio-only code (the verbatim `ToonMaterialGenerator` was NOT touched):

1. **The metal gate never opened (root cause).** The switcher's diagnostic (see below) showed the
   jacket material with `_MetallicGlossMap`/`_MatCap_Sampler` bound, both `*Arr_ID = 0`, but
   `_IsStylizedMetallic = 0` — so the shader gate `_IsStylizedMetallic > 0 && _MatCap_SamplerArr_ID
   >= 0` stayed shut (normals were never gated on it, hence "normals yes, metal no"). Cause:
   avatar materials are born on the **stock `DCL/DCL_Toon`** package shader, which on this branch
   does NOT declare the metallic-branch `_IsStylizedMetallic`. Setting a real `Integer` property
   the *active* shader doesn't declare does not survive the later `mat.shader` swap to the studio
   shader — it falls back to the shader default (0). (The neighbouring `_MetallicGlossMapArr_ID`
   DID survive, because stock DCL_Toon declares it.) **Fix:** in `StudioAvatarShaderSwitcher.Apply`,
   after the swap, re-assert `_IsStylizedMetallic = (_MetallicGlossMapArr_ID >= 0) ? 1 : 0` — using
   the surviving mask id as the "metal was detected" signal, now that the active shader declares the
   flag. Data-driven, so it's correct regardless of the exact persistence mechanism. This also fixed
   DCL_Stylized_PBR (same gate).
2. **Matcap selector + live tint/blur.** New `ActiveMatcapName` (EditorPrefs `OutfitStudio.Matcap`)
   + a **Matcap dropdown** at the top of the tuning panel (both studio shaders; names from
   `GetMatcapNames()` over the loaded library). The switcher pushes the selected matcap **texture**
   onto metal materials each poll (`_MetallicGlossMapArr_ID >= 0` signal) so switching is live.
   `_MatCapColor` (tint) and `_BlurLevelMatcap` (blur, capped 0–4 in knobs AND both shaders' Range)
   are now **tuning knobs on both shaders** — so the knob loop owns them and the matcap push sets
   texture-only (preset tint/blur no longer applied live; all presets are white/0 anyway).
   `EnsureMatcapPresets` now seeds `CommonAssets.DefaultMatcapName` from `ActiveMatcapName`.
3. **PBR metal now matches DCL_Toon_Studio.** PBR was adding the matcap as a Fresnel/F0-weighted
   reflection (`color += envRefl * envF * metallic * ...`) → bright only at grazing edges, tinted by
   the dark metal albedo, layered over a diffuse-free (dark) base = dark jacket with lit edges. Toon
   does a flat *replace*. Rewrote the PBR metal block (`DCL_StylizedPBR_ForwardPass.hlsl`) to
   `reflWeight = lerp(envF, 1, _MatcapMetalBlend); color = lerp(color, envRefl*reflWeight,
   saturate(metallic) * _StylizedMetalStrength)`. So `_MatcapMetalBlend` is now a **physical(0) ↔
   flat/toon-match(1)** dial and `_StylizedMetalStrength` (added to the PBR shader: cbuffer +
   Property `Range(0,4)`) is the replace amount (1 = full, matches toon; >1 over-drives, like toon).
   Defaults (blend 1, strength 1) match toon out of the box. Only remaining gap vs toon: toon also
   multiplies the matcap by the main light colour; PBR doesn't (invisible under a ~white key).

4. **Rim on metal (toon).** In `DCL_Toon_Studio` the rim is baked INTO `finalColor` (via
   `_RimLight_var = Set_HighColor + Set_RimLight * _RimLightIntensity`), so the metal replace-lerp
   (`finalColor = lerp(finalColor, matcapRefl, metalFactor)`) wiped the rim out on metal areas —
   metal jackets got no rim while cloth did. PBR was fine (it adds rim *after* the metal). Fix in
   `DCL_ToonBodyDoubleShadeWithFeather.hlsl`: after the metal lerp, add the rim term back on top,
   `finalColor += rimTerm * saturate(metalFactor)` (rimTerm = the same `lerp(0, Set_RimLight *
   _RimLightIntensity, _RimLight)`), so metal catches the rim too; non-metal is unchanged.

5. **Emission Strength (PBR).** PBR emissives read much hotter than toon under the studio's HDR
   bloom — NOT because emission differs (both shaders use the identical `_Emissive_Tex *
   _Emissive_Color * 2.5`), but because PBR's emissive pixels sit on a brighter additive base
   (ambient on + additive rim on the same silhouette edges), so more of them cross the bloom
   threshold. Bloom is off-limits, so a `_EmissionStrength` scalar was added to the PBR shader
   (cbuffer + Property + multiplied into the emissive term) and exposed as the **Emission Strength**
   knob. **Default 0.19** — the value that visually matches DCL_Toon under the studio bloom.

Updated knob lists: **Toon Studio** adds Matcap Tint + Matcap Blur; **PBR** adds Metal Strength,
Emission Strength, Matcap Tint + Matcap Blur (and the Matcap Metal Blend tooltip now describes the
physical↔flat dial).

**Dialed-in default look (2026-07-16, confirmed by Mauricio).** The knob defaults were tuned to a
finished look so a fresh studio scene reads right without fiddling. Shared: rim tint `#CCB777` warm
gold (a single `RimGold` field in `StudioAvatarShaderSwitcher`, referenced by both shaders).
- **DCL_Toon_Studio:** Rim Intensity 10, Rim Power 0.8, Rim Inside Mask 0.5, Rim Color gold.
- **DCL_Stylized_PBR:** Rim Color gold, Diffuse Wrap 0.5, Shadow Sharpness 0.55, Specular Softness
  2.2, Specular (F0) 0.4, Sheen Tint 0, Ambient (GI) 2.5, Emission Strength 0.19 (others unchanged:
  Rim Intensity 1 / Power 0.3 / Inside Mask 0.15 / Sharpness 0, Metal Strength 1, Matcap Metal
  Blend 1, Matcap Blur 0, Normal Strength 1).
Changing a C# default does NOT move a knob whose value is already stored in EditorPrefs — press
**Reset shader defaults** for that shader once to adopt new defaults; fresh installs get them.

**Diagnostic** (kept, verbose-only): `Apply(verbose:true)` — fired on a shader **button click** —
dumps per-material metal-gate state (`_IsStylizedMetallic`, `_MatCap_SamplerArr_ID`,
`_MatCap_Sampler` bound?, `_MetallicGlossMapArr_ID`, `_MetallicGlossMap` bound?, strengths) plus the
`MatcapPresets` load state. All reads are `HasProperty`-guarded (toon vs PBR expose different
props). The per-material lines are in the log entry's expanded detail pane, not the collapsed list.
Note: the "No MatcapPresets assigned" warning only fires if metal was *detected* (it's inside
`ApplyDefaultMatcap`), so its absence is not proof the library is loaded — read the dump's
`MatcapPresets=N presets` header instead.

**Blur caveat:** `_BlurLevelMatcap` samples the matcap by mip LOD, so it only softens visibly if the
6 matcap PNGs are imported **with mipmaps enabled** — check their import settings if blur looks inert.

## 17. Screenshot poses (iteration 6, 2026-07-16)

Single-frame "poses" for stills: drop GLBs (1-frame skeletal animations) into
**`Assets/OutfitStudio/Poses/`** and a **button per pose** appears under the **Pose** header
(`OutfitStudioWindow.BuildPoseButtons` / `GetPoseNames`), auto-discovered by a file scan of
`Application.dataPath + "/OutfitStudio/Poses"`. Click → sets `outfit.emote`, clears any draft emote,
applies; the active pose's button is disabled (= selected, same convention as the shader buttons);
a `⟳` button rescans the folder without reopening the window.

**Kept inside the tool folder with ZERO renderer changes** (the whole point — no files spilled into
StreamingAssets). Poses ride the stock embedded-emote path: the emote name is
`"../OutfitStudio/Poses/<file>"`, and `Representation.ForEmbeddedEmote` resolves it as
`Path.Combine(streamingAssetsPath, name + ".glb")` = `.../Assets/StreamingAssets/../OutfitStudio/
Poses/<file>.glb`. The `..` walks back out of StreamingAssets into the tool folder; the OS/URI
normalises it when the loader opens the file (same bare-path handling the built-in emotes use). The
name is project-relative, so it's machine-independent (share codes / persistence work for any
teammate with the same `Poses/` folder), and because it points outside StreamingAssets it never
resolves in production builds — which is fine, poses are an editor-only screenshot tool.

**Fix (2026-07-22): transport (▶/❚❚/■) snapping back to the last pose instead of a picked embedded
emote.** The "Embedded" popup shared `outfit.emote` with the pose buttons but was only built once
(`BuildOutfitPane`, on window open) and computed its displayed index as
`EMBEDDED_EMOTES.IndexOf(outfit.emote)` — a pose path isn't in that list, so the popup silently fell
back to showing `"idle"` (index 0) after any pose click, while the pose was what was actually
loaded/playing. If the user then "reselected" whatever the popup already showed (commonly `"idle"`),
UI Toolkit's `PopupField` doesn't fire `RegisterValueChangedCallback` for a same-value pick, so
nothing reloaded — the transport buttons kept controlling the stale pose clip: Play looked like a
no-op (a 1-frame pose is already "playing"), Stop crossfaded the pose to idle, and Play again
replayed the pose, not the emote the popup claimed to show. Fixed by giving the popup a sentinel
choice, `EMBEDDED_EMOTE_NONE`, and a `SyncEmotePopup()` helper (`_emotePopup.SetValueWithoutNotify`)
called from every place that sets `outfit.emote` outside the popup itself — pose buttons, draft/
catalog emote picks (`EquipDraft`, `OnItemClicked`), and `LoadOutfit` (share code / preset loads).
Whenever a pose (or anything else not in `EMBEDDED_EMOTES`) becomes active, the popup now visibly
shows the sentinel instead of a stale `"idle"`/emote name, so picking an actual embedded emote
afterwards is always a genuine value change and reliably reloads + auto-plays it.

**Apply in play mode** (like all emotes — a 1-frame emote holds its frame): equip → Enter Play →
click a pose → Capture Still. Edit mode still shows the static idle pose (poses aren't sampled onto
the edit-mode skeleton).

**Play-mode pose buttons change ONLY the pose, not the loaded avatar (2026-07-17).** In play mode a
pose button calls `ApplyPoseOnly` instead of `Apply`: it sets just `AangConfiguration.Emote` and
reloads, so whatever avatar is loaded keeps its identity/wearables and only the pose changes (the
`AvatarLoader` diffs the unchanged wearables, so just the emote reloads) — mirroring how the shader
switcher edits the loaded avatar rather than reloading it.

**Mode handling (important):** `Builder` (the custom outfit) is kept. **Every other mode is switched
to `Profile`** (preserving `config.Profile`, so a Debug-tab **Random Profile** stays the same avatar,
now posed). This is required because `LoadForProfile`/`LoadForBuilder` pass `config.Emote` through
`FromEmbeddedEmote` (so `"../OutfitStudio/Poses/<file>"` resolves), **but `Jesus` mode hard-codes its
emote** (`character/Particles_Anim` — the arms-out "jesus" pose) and Marketplace shows a wearable —
both ignore `config.Emote`. Random Profile via `SetProfile` doesn't change the mode, so if the
session was in Jesus mode the pose silently wouldn't apply; forcing Profile fixes it.

**Holding the pose (`EmoteLoop`):** a single-frame pose only *holds* if the emote loops. Builder's
`ResolveBuilderEmote` uses `loop: true`, but `LoadForProfile`/`LoadForMarketplace` use `loop: false`
— so in Profile mode the 1-frame pose ended instantly and reverted to the base breathing idle.
`ApplyPoseOnly` sets `config.EmoteLoop = true` (renderer touch point #6, prod default false) so the
profile pose holds. **Edit mode is unchanged** (pose buttons route through `Apply`). Constants live
in `OutfitStudioWindow`: `POSES_DIR_UNDER_ASSETS` (`OutfitStudio/Poses`, for the scan) and
`POSES_EMBEDDED_PREFIX` (`../OutfitStudio/Poses`, the emote name). A future v2 could sample the pose
clip onto the edit-mode skeleton (like `SampleIdlePose`) so shots can be framed without entering play.

**Update 2026-07-22: the "Embedded" emote popup now goes through `ApplyPoseOnly` too, in play mode.**
Previously picking an animation ("dance", "clap", ...) from the popup always called the full `Apply`,
which hardcodes `config.SetMode("builder")` — so choosing an animation forced a reload of the
studio's custom outfit, discarding whatever avatar was actually loaded (e.g. a Debug-tab Random
Profile), exactly the outfit-switch the pose buttons were built to avoid. The popup's change handler
now mirrors the pose buttons: `Application.isPlaying` → `ApplyPoseOnly(outfit.emote)` (re-animate the
loaded avatar in place); otherwise → `ScheduleApply()` as before (edit mode has no avatar to preserve
identity for). `ApplyPoseOnly` itself is generic over "single-frame pose" vs. "multi-frame animation"
— both are just an embedded-emote name reaching `FromEmbeddedEmote` through Profile/Builder mode, so
no changes were needed there beyond documentation.

## 18. Card frame — Fortnite-style item cards (2026-07-17)

A "Card frame (beauty shot)" section (collapsible Foldout at the top of the outfit pane) composites
a Fortnite item-card look around the avatar: **outer background gradient → rounded card panel →
avatar → bottom fade**. The reference targets are the marketplace/Fortnite item cards (purple card
on a dark→violet backdrop, head overflowing the top edge, legs fading into the card). Studio-scene
only; **fully folder-isolated — zero renderer-data / shipping-asset edits, and nothing ships to a
build** (the shader is only referenced by editor-created runtime materials, so it's excluded from
the WebGL build — verify in a build report, same discipline as the Nethereum DLLs in §13).

### Why quads, not a UI overlay or a renderer feature
The hard constraint is §8: **runtime UI overlays don't render through the capture camera**, so the
frame must be camera geometry to appear in the exported PNG. Two ways to get camera geometry:
- A URP fullscreen renderer feature (like `BackgroundRendererFeature`) — but the studio
  **PreviewCamera renders through `URP_PreviewRenderer` (renderer index 1)**, which has only the
  outline feature; the gradient `BackgroundRendererFeature` lives on `URP_ConfiguratorRenderer`
  (index 0 = the ConfiguratorCamera we strip). Adding a feature would mean editing a **shipping**
  renderer data (or duplicating it, §14 "Route A") **and** shipping the shader into prod builds.
- **Camera-parented quads** (chosen) — keeps everything under `Assets/OutfitStudio/`, needs no
  renderer/asset changes, and the quad shader never enters a build.

### Layers (ordered by render queue, so no per-avatar depth math)
`StudioCardFrame` ([InitializeOnLoad], 0.5 s poll, studio-scene-gated like the other helpers)
parents three quads to the render camera (`Camera.main`, matching what `OutfitCapture` uses; falls
back to a `PreviewCamera`/highest-depth search). One shader (`Custom/StudioCardFrame`, `_Mode`
0/1/2) with **material-driven render state** (`_ZTest`/`_ZWrite`/`_SrcBlend`/`_DstBlend`) covers all
three:
- **Background** — queue 1000, opaque, **ZWrite On**. Fullscreen vertical gradient + optional radial
  glow. Writing depth here occludes the skybox **without touching the camera's clear flags** (no
  scene churn). Safe because the studio renderer has `m_DepthPrimingMode: 0` (priming would force
  ZTest Equal and skip a quad with no DepthOnly pass) and Forward+ (`m_RenderingMode: 2`) is fine for
  unlit quads.
- **Card panel** — queue 1500, ZTest Always, alpha blend. Rounded-rect (SDF, aspect-corrected) with
  a vertical gradient fill (fill only — the border is its own top layer, below). Drawn **before** the
  avatar (opaque, queue 2000), so the avatar draws over it and the **head overflowing the top edge is
  free** (no masking — that was the original "avatar mask" worry; it dissolves because the card is
  just a shape *behind* the avatar). No hard side-clip by design — framing + margins keep the avatar
  inside, matching the refs (add the SideMask toggle below for a hard clip).
- **Bottom fade** — queue 3500, ZTest Always, alpha blend. Drawn **after** the avatar; same rounded
  rect as the card (so its bottom corners match) × a vertical fade to transparent, painting the card
  colour over the legs.
- **Border** (`_Mode 4`) — queue 4000, ZTest Always, alpha blend. Drawn **last, on top of
  everything** (avatar, fade, side-mask) so the card outline is never occluded — this is why the
  border is a separate quad and not baked into the card panel. Ring in the SDF band
  `dist ∈ (-_BorderWidth, 0)`, built as `saturate(sInner - sOuter)` (difference of the inner/outer
  edge smoothsteps) so it collapses to **exactly** 0 at `_BorderWidth == 0` — do **not** revert to
  `mask * innerCut`, which peaks at ~0.25 on the edge and leaves a ~1px hairline around the whole
  card even at width 0. **Open at the top**: the border
  fades out above `_BorderTopFade` (uv.y 0.88) so it frames only the sides + bottom and the head
  overflows the top freely (without this the top edge draws across the neck/shoulders — same intent
  as the side-mask leaving the top open).

The card, fade, and border quads share the same rect; only the background (and the side mask) are
fullscreen. Placement Z is a fixed `PLANE_Z = 50` (behind a ~2 m avatar, inside the far plane);
ordering is queue-only so the exact Z doesn't matter except for the background's depth write.

### Optional side-mask (clip arms/hands to the card) — `SideMask` toggle
By default the avatar overflows on *all* sides (drawing behind gives top-overflow for free but no
side clip). The **"Mask avatar to card sides"** toggle adds a 4th quad (`_Mode 3`, queue 3200 — in
front of the avatar + transparent wearables, before the fade) that **repaints the background gradient
over everything outside the card rect, leaving the top open** so the head still overflows (matches
the Fortnite cards, where arms/hands are clipped at the card edge but the head pokes out the top).
- **No seam:** the mask quad is given the **exact same transform as the background quad**, so its
  mesh UV — and therefore the repainted gradient (incl. glow) — is pixel-identical to the background.
  The card rect is handed to the shader as `_MaskRect (l,r,b,t)` in that shared UV space
  (`U(f) = 0.5 + (f-0.5)/BG_OVERSIZE` maps a viewport fraction into it).
- **Shape:** the same rounded-rect SDF as the card gives clipped **sides + rounded bottom corners**;
  the region **above the card top, within the card width** is forced open (`saturate(cardMask +
  withinX*aboveTop)`) so the head overflows. **Use `+`, not `max`:** at the card-top transition both
  terms are mid-fade (~0.5) with different AA widths, and `max(0.5,0.5)=0.5` dipped the keep-region
  below 1, painting a faint bg **seam across the head**; the sum is ~1 there (the terms are
  complementary in y, and `aboveTop` is 0 below the top so they never over-add elsewhere). Bottom
  corners align with the card panel; the bottom fade draws over the inside afterwards.
- Chosen over a stencil mask (would need every avatar shader to opt in) or a fullscreen composite
  (would need the avatar isolated to its own RT). The repaint-outside approach needs neither and
  stays in the quad model. Only enabled when the toggle is on (`_mask.enabled = SideMask`).

### Controls & persistence
All knobs live on `StudioCardFrame` as EditorPrefs-backed static properties (keys
`OutfitStudio.Card.*`); the window builds fields from them (`BuildCardFrame`/`BuildCardBody`),
setters push live. Groups: **Background** (top/bottom colour, glow colour+height+size), **Card**
(top/bottom colour, side/top/bottom margins, corner radius, border colour+width), **Bottom fade**
(colour, height, softness). "Reset card defaults" clears the keys. Defaults are tuned to the
reference look (bg `#16143A`→`#3A1E5C`, card `#6B3FA0`→`#4A2870`, top margin 0.12 for head overflow).
Master **Enable** toggle (default off, opt-in).

### Capture
`OutfitCapture.CaptureStill` forces `camera.aspect = width/height` and calls
`StudioCardFrame.RelayoutFor(camera)` before the render request (then `camera.ResetAspect()` after),
so a still at a resolution different from the Game view still frames the card correctly. Set the
Game view to a **portrait aspect (~2:3)** to author WYSIWYG. Note: with the card enabled the opaque
background quad fills the frame, so the "Transparent background" capture toggle is effectively
overridden (you want the card bg, not transparency).

### Lifecycle
Quads are `HideFlags.DontSave` (never serialized → no scene churn) and parented to the camera so they
track the view (incl. drag-rotate, which rotates the avatar only). Recreated after a domain reload
(`TryReattach` finds a surviving root by name, else rebuilds) and after the play-mode scene reload
(the poll re-parents to the new camera). Edit-mode preview and play mode both show the frame.

### Verification (needs the editor — not yet run)
1. Focus Unity → `Custom/StudioCardFrame` compiles, no CS errors in the new files.
2. Studio scene, edit mode: load an outfit, open **Card frame**, tick **Enable** → gradient bg +
   rounded purple card appear behind the avatar within ~0.5 s; head overflows the top; legs fade
   into the card at the bottom.
3. Tweak margins/radius/colours/fade → live update. Drag-rotate → bg/card stay put, avatar rotates.
4. Enter play, pick a pose, **Capture Still** at e.g. 900×1350 → PNG shows the framed card matching
   the Game view (set the Game view to a 2:3 portrait aspect first).
5. Toggle Enable off → quads vanish, plain preview returns. Prod safety: confirm the shader is absent
   from a WebGL build report and no diffs on any `URP_*`/renderer-data asset or `Main.unity`.

### Possible v2s
Name/price/"+" text chrome (deferred — not a DCL concept; would be a 4th quad or a captured text
layer); per-side avatar hard-clip via a stencil if a wide pose ever spills past the card; save/load
card presets alongside outfit presets; a horizontal/radial background gradient option.
