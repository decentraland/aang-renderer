# Outfit Studio

Editor tool for artists: compose an outfit from marketplace wearables, pose the avatar with an
emote and capture high-res stills / MP4 video — without leaving Unity.

Open via **Decentraland ▸ Outfit Studio**.

## Workflow

1. Open `Assets/Scenes/Main.unity`.
2. Open the Outfit Studio window and browse the marketplace catalog (left pane).
   Search, filter by slot / rarity / body and click items to equip them.
   **The avatar assembles live in the Scene/Game view in edit mode** — no play mode needed for
   outfit selection (static idle pose; use **Clear Preview** in the toolbar to remove it).
3. Pick a pose: an embedded emote from the dropdown, or any marketplace emote from the
   **Emotes / Poses** tab. Emote playback needs play mode; use ▶ / ❚❚ / ■ and the scrubber to
   freeze a specific frame.
4. Press **▶ Enter Play** for animation and capture (or Apply while playing). The edit-mode
   preview clears itself automatically and the renderer's builder mode loads the same outfit;
   changes keep auto-applying while you pick items.
5. Capture:
   - **Capture Still** — PNG at the configured resolution (independent of the Game view size),
     optionally with a transparent background.
   - **Start/Stop Video** — manual MP4 recording of the Game view (Unity Recorder).
   - **Record Emote** — records exactly one full emote playback.
   - **Record Turntable** — deterministic 360° spin over the configured duration.

   Files land in the `Captures/` folder next to the project by default.

## Reproducing an outfit

- **Share code** — the query-string in the Share code box fully describes the outfit
  (body shape, wearable URNs, colors, pose). Copy it, send it to someone, paste it back with
  **Load from code**. The same string works as `Bootstrap.debugUrl` and as URL parameters for
  the deployed web renderer (builder mode).
- **Presets** — save named `OutfitPreset` assets in the project for a local outfit library.

## Debug tab & Clean View

The renderer's built-in play-mode debug overlay (JSBridge invoke, URL presets, Print Config,
Random Profile, zoom) lives in the window's **Debug** tab. The **Clean View** toolbar toggle
(on by default) hides that overlay in the Game view so only the avatar is visible — mouse-drag
rotation and the loading spinner keep working. Toggle it off to get the classic in-game overlay
back (plain play mode without the window is unaffected either way).

## Notes

- Browsing, preset editing **and 3D outfit preview** work in edit mode; emote playback and
  capture need play mode. The edit-mode preview is a static idle pose (no spring bones/outline).
- The prod/dev toggle in the toolbar switches between `.org` and `.zone` backends.
- Wearables with no representation for the selected body shape are skipped with a warning.
- Everything lives in this folder except two small touch points: the `com.unity.recorder`
  dependency in `Packages/manifest.json` and emote-URN support in
  `PreviewController.LoadForBuilder`.
