# Recoil Platformer - Agent Instructions

## Project Overview
2D cyberpunk recoil-platformer prototype built in Unity 6. Core mechanic: high-recoil shotgun used for both combat and traversal. Keyboard/mouse controls with 8-direction quantized mouse aim. First milestone is a greybox recoil test room.

## Tech Stack
- Unity 6 (uses `rb.linearVelocity` not `rb.velocity`)
- C# scripts in `Assets/Scripts/`
- Old Input Manager (`Input.GetAxisRaw`, `Input.GetButtonDown`) - not new Input System yet
- Craftpix asset packs (cyberpunk player, guns, industrial tileset, seaport enemies, cyberpunk GUI)

## Project Structure
```
Assets/
  Scripts/           # All gameplay scripts
  Sprites/           # Craftpix sprite sheets
  Animations/        # Animator controllers and clips
  Scenes/            # Unity scenes
  Tilemaps/          # Tilemap palettes
```

## Player Hierarchy (critical - do not change)
```
Player              # Rigidbody2D, BoxCollider2D, PlayerController2D
  Body              # SpriteRenderer, Animator
  GroundCheck       # Transform used for ground detection
  GunPivot          # (future) gun attachment point
```

## Key Rules

### Never flip the Player parent
- Only flip `Body`'s SpriteRenderer via `bodySpriteRenderer.flipX`
- The Player parent is the stable physics object; flipping it breaks colliders and child positions

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

## Coding Conventions
- Use `[SerializeField] private` for inspector fields, not public
- Use `[Header("Section")]` to organize inspector groups
- Ground check uses `Physics2D.OverlapBox` against a `groundLayer` LayerMask
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
- Composite Collider 2D being set up for tile seam fix
- Next: coyote time, jump buffering, variable jump height, fall multiplier
- Then: 8-direction mouse aim and shotgun recoil mechanic

## Testing
- No automated test framework set up yet
- Test by entering Play Mode in Unity Editor
- Use `OnDrawGizmosSelected` for visual debugging of colliders/raycasts
