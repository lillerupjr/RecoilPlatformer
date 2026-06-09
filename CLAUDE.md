# Recoil Platformer - Agent Instructions

## Project Overview
2D cyberpunk recoil-platformer prototype built in Unity 6. Core mechanic: high-recoil shotgun used for both combat and traversal. Keyboard/mouse controls with 8-direction quantized mouse aim. First milestone is a greybox recoil test room.

## Docs
Design/reference notes live in **`.claude/docs/`** (versioned with the repo):
- `.claude/docs/roadmap.md` — milestone roadmap (source of truth; MEMORY.md points here)
- `.claude/docs/unity6-notes.md` — Unity 6 specifics, camera/parallax conventions, MCP gotchas

Keep this CLAUDE.md terse; put longer notes in `.claude/docs/`.

## Tech Stack
- Unity 6 (uses `rb.linearVelocity` not `rb.velocity`)
- URP 2D (sprites use Sprite-Unlit-Default material)
- C# scripts in `Assets/Scripts/`
- Old Input Manager (`Input.GetAxisRaw`, `Input.GetButtonDown`) - not new Input System yet
- Craftpix asset packs (cyberpunk player, guns, industrial tileset, seaport enemies, cyberpunk GUI)

## Unity MCP (agents can drive the Editor)
The project has **CoplayDev MCP for Unity** (`com.coplaydev.unity-mcp`). When the Unity
Editor is open and its bridge is running, agents can create/modify GameObjects and
components, enter/exit Play Mode, read the console, take Game-view screenshots, and
recompile — via the `UnityMCP` MCP server (HTTP at `http://127.0.0.1:8080/mcp`, hosted by
the Editor). If tools are missing, the bridge wasn't up at session start; restart the
session after confirming the Editor is running.

Gotchas learned the hard way:
- **The Editor only ticks frames when its window is focused.** Unfocused, the play loop
  freezes — the camera/parallax won't advance and screenshots show a frozen frame. To test
  motion, the human must click into the Game view. Numeric verification still works (read
  transforms before/after) but absolute values lag by a frame or two.
- **Play-mode transitions flap when unfocused.** `stop` may need re-issuing; scene `save`
  and `gameobject modify` fail "during play mode" until the transition settles.
- **`manage_gameobject create` ignores `component_properties`.** Create the object with the
  component, then set props via `manage_components set_property`.
- **New script types don't resolve until a full domain reload.** After creating a script,
  `refresh_unity scope=all` (or restart) before `manage_components add` can find the type.
- **Texture import settings:** edit the `.png.meta` directly (the MCP `manage_texture
  set_import_settings` arg didn't serialize in our client).

## Project Structure
```
Assets/
  Scripts/      # Gameplay scripts, by domain: Player/, Camera/, Environment/
  Art/          # Backgrounds/ (Industrial parallax PNGs), Tiles/ (Raw, Palettes, TileAssets)
  Player/       # Raw/Cyborg (source sprites), Sprites/, Animations/Cyborg
  Prefabs/      # Player/
  Scenes/       # SampleScene (active greybox room)
  Settings/     # URP render-pipeline assets
```

## Player Hierarchy (critical - do not change)
```
Player              # Rigidbody2D, BoxCollider2D, PlayerController2D
  Body              # SpriteRenderer, Animator
  GroundCheck       # Transform used for ground detection
  ArmPivot          # Aim anchor; the right Arm renderer rotates around it to follow the mouse
    Arm             # SpriteRenderer (weapon arm); ShotgunRecoil2D swaps E/SE sprite + rotates
```

## Key Rules

### Never flip physics roots
- Only flip visual SpriteRenderers (for example `Body` via `bodySpriteRenderer.flipX`)
- Player/enemy parents are stable physics roots; flipping them breaks colliders and child positions
- Mirror facing-dependent child anchors by local X instead: `ArmPivot`, enemy `AttackPoint`,
  `WallCheck`, and `LedgeCheck` should move to the facing side while the root stays unflipped

### Unity 6 API differences
- Use `rb.linearVelocity` instead of `rb.velocity`
- Tilemap Collider 2D uses "Composite Operation: Merge" instead of "Used By Composite"
- Do NOT use "Used By Effector" for tilemap compositing

### Animator setup
- Controller: CyborgAnimator
- Parameters: `Speed` (Float), `IsGrounded` (Bool), `VerticalVelocity` (Float)
- States: Cyborg_Idle, Cyborg_Run, Cyborg_Jump
- All movement transitions have "Has Exit Time" disabled

### Sprite slicing
- Cyborg sprites are ~32x48 with 16px horizontal padding (not naive 48x48)
- Naive 48x48 slicing causes teleporting when flipped due to character sitting in left part of frame

### Camera
- **Main Camera is Orthographic** (size 5 → 10-unit-tall view). Switched from the default
  Perspective for predictable 2D framing; the parallax sizing depends on it.
- `CameraFollow2D` (LateUpdate + SmoothDamp) follows the Player; the Player's Rigidbody2D
  Interpolation must be `Interpolate` or the follow looks jittery.

### Parallax background
- `ParallaxLayer2D` drives each layer; `scrollSpeed` = on-screen speed (0 far/slow,
  1 near/fast), `verticalParallaxScale` = vertical strength as a fraction of horizontal.
- Layers are Tiled SpriteRenderers on the `Default` sorting layer at negative orders
  (behind the Player at order 0), under a `Background` parent.
- The `Industrial` set: PNGs `1`–`5` are the parallax layers (far→near); `Background.png`
  is the flattened composite preview and is NOT used as a layer.

## Coding Conventions
- Use `[SerializeField] private` for inspector fields, not public
- Use `[Header("Section")]` to organize inspector groups
- Ground check uses `Physics2D.OverlapBox` against a `groundLayer` LayerMask
- Enemy ground/wall/ledge checks should also use dimensioned checks (box/capsule style) rather
  than radius-only circles once tuned, matching the player ground-check pattern
- Keep physics in `FixedUpdate`, input reading in `Update`
- Prefer direct `rb.linearVelocity` assignment for snappy platformer controls

## What Agents Should and Should NOT Do

### Good for agents
- Writing and modifying C# scripts
- Editor automation scripts (custom inspectors, tools)
- Validation tools and debug utilities
- Code refactors and new gameplay systems
- Writing shader code

### Leave to the human in Unity Editor
- Scene composition and object placement
- Visual tuning (sprite slicing experiments, animation curves)
- Playfeel parameter tweaking (speeds, forces, timings)
- Tilemap painting
- Wiring serialized references in the Inspector

## Current State
- Basic movement and animation working
- Camera follow (`CameraFollow2D`) + Orthographic camera working
- Ctrl/Cmd+R resets the Player to spawn (`PlayerController2D.ResetToSpawn`)
- Multilayer parallax background working (`ParallaxLayer2D`, Industrial set)
- Composite Collider 2D being set up for tile seam fix
- Next: coyote time, jump buffering, variable jump height, fall multiplier
- Then: 8-direction mouse aim and shotgun recoil mechanic

## Testing
- No automated test framework set up yet
- Test by entering Play Mode in Unity Editor
- Use `OnDrawGizmosSelected` for visual debugging of colliders/raycasts
