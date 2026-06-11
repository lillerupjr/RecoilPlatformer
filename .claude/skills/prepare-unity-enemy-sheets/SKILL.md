---
name: prepare-unity-enemy-sheets
description: Prepare raw Unity 2D enemy sprite-sheet folders into Animator-Controller-ready per-enemy asset folders. Use when an agent must inspect enemy PNG sprite sheets, derive frame sizes/counts, configure Unity texture import settings, slice sprites, create AnimationClips for body and projectile sheets, validate Unity reimports, or migrate enemy sheet collections into the repo's root-clips plus Raw-sheets enemy layout before controller/behavior work.
---

# Prepare Unity Enemy Sheets

## Overview

Convert a raw enemy sheet collection into a single Unity asset folder that is ready for an Animator Controller. Include body animations and projectile sheets in the same folder. Do not create the Animator Controller or enemy behavior logic in this workflow.

For enemy controller/prefab behavior conventions after sheet prep, see
`.claude/docs/enemy-behavior.md`.

## Target Layout

Use one folder per enemy:

```text
Assets/Enemies/<EnemyName>/
  Idle.anim
  Run.anim
  Attack.anim
  Projectile.anim
  Raw/
    Idle.png
    Run.png
    Attack.png
    Projectile.png
```

Keep controller-facing `.anim` clips at the enemy root. Keep source `.png` sprite sheets under that enemy's `Raw/` subfolder, including projectile sheets. After the repo migration, do not preserve or recreate global split folders such as `Assets/Enemies/Raw/<EnemyName>` or `Assets/Enemies/Animations/<EnemyName>`.

## Workflow

1. Inspect the enemy folder.
   - List every `.png` in the enemy's `Raw/` subfolder; animation names are whatever files exist, not a fixed set.
   - Include projectile sheets such as `Projectile.png`, `Projectile1.png`, and `Projectile2.png`.
   - Visually inspect at least one representative sheet and any oddly sized sheet. Use dimensions to verify the inferred grid.

2. Derive sprite-sheet layout from the images.
   - Infer frame width, frame height, row count, column count, and frame count from the PNG content and dimensions.
   - Prefer the enemy's dominant frame size when sheets are consistent, then verify every PNG is divisible by that size.
   - Treat tiny single-frame images as static sprites unless the pixels clearly form multiple frames.
   - If a sheet cannot be inferred safely because dimensions or visual content are ambiguous, ask for the frame size before importing.

3. Configure each PNG import in Unity.
   - Use Unity MCP/Editor APIs whenever available; avoid hand-authoring Unity `.meta` file IDs.
   - Set `Texture Type = Sprite`, `Sprite Mode = Multiple` for animated sheets, centered pivot, no mipmaps, alpha transparency, point filtering, and no compression for pixel art.
   - Set pixels-per-unit from the enemy's existing convention or nearby processed enemies. In this repo, current processed enemies use `16` PPU, but verify against the actual enemy assets instead of assuming globally.
   - Slice each animated sheet into sprites named `<SheetName>_0`, `<SheetName>_1`, etc., in left-to-right, top-to-bottom order.

4. Create clips for every animated sheet.
   - Create one root-level `.anim` per multi-frame `.png`, including projectile sheets.
   - Bind the clip to `SpriteRenderer.m_Sprite` with the sliced sprites in frame order.
   - Use frame timing derived from the requested/default FPS. In this repo, `12 FPS` is the established starting point unless the user or source asset implies another rate.
   - Name clips exactly after the source sheet stem, for example `Assets/Enemies/<EnemyName>/Attack.anim` from `Assets/Enemies/<EnemyName>/Raw/Attack.png`.

5. Set loop policy deliberately.
   - Loop idles and locomotion cycles such as `Idle`, `Walk`, `Run`, `Fly`, `Hover`, or `Patrol`.
   - Do not loop one-shot body actions such as `Attack`, `Hurt`, `Death`, `Spawn`, or `Despawn`.
   - For projectile sheets, loop only continuous travel cycles. Do not loop muzzle flashes, impacts, explosions, or one-shot projectile effects.
   - If the role is unclear from the sheet name and frames, choose the safer non-looping option and mention it in the result.

6. Validate in Unity.
   - Refresh/reimport assets and wait for domain/import completion.
   - Check the Unity console for errors.
   - Verify every animated PNG under `Raw/` has sliced sprites and a matching root-level clip.
   - Verify every clip references the expected sliced sprites and uses the intended loop setting.
   - Leave the folder controller-ready, but do not create an Animator Controller, prefab, AI script, transitions, or behavior parameters.

## Unity MCP Notes

- Treat `manage_asset move` results cautiously in this project. The tool may report a failed `MoveAsset` call even when Unity moved the asset and its `.meta` successfully. After every move, verify the destination exists and the source is gone before retrying or changing approach.
- Prefer Unity asset operations for moves/deletes so GUIDs travel with `.meta` files. Then verify clip references by comparing each moved PNG `.meta` GUID against the matching `.anim` contents.
- Avoid relying on `execute_code` for multi-step editor automation on Windows here; it can fail with Mono `filename or extension is too long` errors, even for short calls.
- For nontrivial editor automation, create a temporary editor helper under `Assets/Editor`, expose a `[MenuItem]`, refresh/compile Unity, run it with `execute_menu_item`, then delete the helper and refresh again.
- Check `editor_state` before mutations and `read_console` after refresh/compile/menu execution. Ignore only known MCP transport warnings if the asset validation passes.

## Migration Notes

When migrating legacy folders, use Unity asset operations through MCP/Editor APIs so `.meta` files and GUID references move with the assets. Move source PNGs into `Assets/Enemies/<EnemyName>/Raw/` and clips into `Assets/Enemies/<EnemyName>/`. The final repo structure should contain only per-enemy folders; do not keep duplicate legacy `Assets/Enemies/Raw/<EnemyName>` or `Assets/Enemies/Animations/<EnemyName>` copies after cleanup.
