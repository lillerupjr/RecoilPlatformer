# Unity 6 Notes — RecoilPlatformer

Reference for Unity-6-specific gotchas and project conventions. Companion to the root
`CLAUDE.md` (agent instructions) and the auto-memory `MEMORY.md`. This file is the home for
design/reference notes that don't belong in the terse CLAUDE.md.

## Unity 6 API differences
- Use `rb.linearVelocity`, **not** `rb.velocity` (renamed in Unity 6).
- Tilemap Collider 2D compositing: use **"Composite Operation: Merge"** on the
  TilemapCollider2D — *not* the legacy "Used By Composite" toggle.
- Do **not** use "Used By Effector" for tilemap compositing.

## Tilemap Composite Collider setup
- TilemapCollider2D: **Composite Operation = Merge**.
- Add a **Composite Collider 2D** component to the tilemap GameObject.
- The tilemap's **Rigidbody2D should be Static**.
- Composite **Geometry Type = Outlines**.
- If seam-catching persists: bump **Extrusion Factor ~0.01**, or put a **Capsule Collider**
  on the player.

## Rendering / camera
- Project is **URP 2D**. Sprites use the `Sprite-Unlit-Default` material.
- **Main Camera is Orthographic** (size 5 → 10-unit-tall viewport). Switched from the default
  Perspective so 2D framing and parallax sizing are predictable and depth-independent.
- `CameraFollow2D` follows the Player in `LateUpdate` with `Vector3.SmoothDamp`
  (offset, smoothTime, per-axis locks). Hand-rolled instead of Cinemachine (which *is* in the
  manifest) to keep the greybox lean; revisit if dead zones/confiners/blends are needed.
- **Any physics-driven object the camera follows needs Rigidbody2D Interpolation = `Interpolate`.**
  Physics steps at 50 Hz; a per-frame smoothing camera makes that stepping read as jitter.
  Interpolation smooths the *rendered* transform between physics steps. The fix lives on the
  followed object (the Player), not in the camera script.

## Sprites
- Cyborg sprites are **~32×48 with 16px horizontal padding** (not naive 48×48). Naive 48×48
  slicing causes teleporting on flip because the character sits in the left part of each frame.
- Only ever flip `Body`'s SpriteRenderer via `flipX` — never flip the Player parent (it's the
  stable physics object; flipping it breaks colliders and child positions).

## Facing anchors and physics probes
- Never flip Player/enemy parent roots; they are stable physics objects and flipping them breaks
  colliders and child positions.
- Flip only visual SpriteRenderers via `flipX`.
- Facing-dependent child anchors **do** mirror by local X. `GunPivot`, enemy `AttackPoint`,
  `WallCheck`, and `LedgeCheck` should move to the facing side while the root collider stays
  stable. This keeps gameplay probes and hit areas aligned with the sprite without scaling the
  physics root.
- Player ground detection uses `Physics2D.OverlapBox` with a tunable size against `groundLayer`.
- Enemy ground/wall/ledge probes should follow the same dimensioned-check pattern once tuned:
  prefer serialized box/capsule sizes over radius-only circles so checks can match sprite and
  collider proportions in the Scene view.

## Parallax background (`ParallaxLayer2D`)
- `scrollSpeed`: apparent on-screen speed (0 = far/slow, 1 = near/fast). Internally the layer
  follows the camera by `(1 - scrollSpeed)` — high value lags the world more and reads as nearer.
- `verticalParallaxScale`: vertical parallax as a fraction of horizontal (0 = vertically
  pinned, 1 = equal). Set ~0.35 so vertical drift stays within each layer's coverage.
- Layers are **Tiled** SpriteRenderers (PPU 31 frames one image ≈ viewport; whole-tile
  recenter gives seamless infinite horizontal scroll), scaled ~1.4× for vertical headroom,
  on the `Default` sorting layer at negative orders (behind the Player at order 0), under a
  `Background` parent. Industrial PNGs 1–5 are far→near; `Background.png` is the flattened
  composite preview and is **not** used as a layer. Assumes art tiles seamlessly left↔right.

## Unity MCP (CoplayDev `com.coplaydev.unity-mcp`)
Agents can drive the Editor via the `UnityMCP` server (HTTP `http://127.0.0.1:8080/mcp`,
hosted by the running Editor). If tools are missing, the bridge wasn't up at session start —
restart the session after confirming the Editor is running with the bridge started.

Hard-won gotchas:
- **The Editor only ticks frames when its window is focused.** Unfocused, the play loop
  freezes — camera/parallax won't advance and screenshots show a frozen frame. Click the Game
  view to test motion. Numeric verification (read transforms before/after) still works but
  absolute values lag a frame or two.
- **Play-mode transitions flap when unfocused.** `stop` may need re-issuing; scene `save` and
  `gameobject modify` fail "during play mode" until the transition settles.
- **`manage_gameobject create` ignores `component_properties`.** Create with the component,
  then set props via `manage_components set_property`.
- **New script types don't resolve until a full domain reload** (`refresh_unity scope=all`)
  before `manage_components add` can find the type.
- **Texture import settings:** edit the `.png.meta` directly (the MCP `manage_texture
  set_import_settings` arg didn't serialize in our client).
- **Asset moves/renames go through `manage_asset`** (not `git mv`) so `.meta` files travel and
  GUID references survive.
- **Manual MCP fallback:** if Codex's native MCP wrapper cannot handshake but
  `http://127.0.0.1:8080/mcp` responds, raw JSON-RPC can still drive Unity. POST
  `initialize`, read the `mcp-session-id` response header, POST `notifications/initialized`
  with that session id, then call `tools/list`, `resources/read`, or `tools/call` using the
  same header. This has been used successfully for `refresh_unity`, `read_console`, Play Mode
  smoke checks, and editor state reads.
