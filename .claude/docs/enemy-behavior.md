# Enemy Behavior Notes

Current enemy conventions and controller decisions. Keep this file focused on behavior and
prefab/controller architecture; raw sheet preparation belongs in
`.claude/skills/prepare-unity-enemy-sheets/SKILL.md`.

## Asset Layout

Enemy assets live in one folder per enemy:

```text
Assets/Enemies/<EnemyName>/
  Idle.anim
  Walk.anim or Run.anim
  Attack.anim
  Hurt.anim
  Death.anim
  <EnemyName>Animator.controller
  <EnemyName>.prefab
  Raw/
    Idle.png
    Walk.png or Run.png
    Attack.png
    Hurt.png
    Death.png
```

Keep source sheets under `Raw/`. Keep controller-facing `.anim` clips at the enemy root.
Projectile sheets/clips also stay inside the same enemy folder when an enemy has projectiles.

## Shared Rules

- Do not flip enemy physics roots. Root GameObjects hold Rigidbody2D/colliders and stay at
  positive scale.
- Flip only visual SpriteRenderers through `EnemyAnimator2D.FaceDirection`.
- Mirror facing-dependent child anchors by local X, especially `AttackPoint`, `WallCheck`, and
  `LedgeCheck`.
- Enemy damage hooks stay enemy-side: attacks call `IPlayerDamageReceiver2D` when present.
- Since Player and enemies can both be on `Default`, enemy target detection must filter hits by
  `IPlayerDamageReceiver2D`; layer masks alone are not enough.
- Keep the simple behavior model unless a specific enemy needs more. Avoid generic AI
  abstraction until two or more concrete enemies genuinely share the same behavior.

## RoboDog

`RoboDogController2D` is a concrete grounded melee controller.

- Spawns facing `initialFacingDirection`.
- Patrols in that direction at `patrolSpeed`.
- Turns around only when `WallCheck` detects ground/wall ahead or `LedgeCheck` no longer detects
  ground ahead.
- If a valid player target enters `detectionRange`, chases horizontally at `chaseSpeed`.
- If the target leaves `detectionRange`, immediately resumes the normal patrol loop.
- Stops during attack windup/active/recovery, triggers `Attack`, and applies the player-damage
  hook during the active window.
- Ground, wall, and ledge probes use serialized `Physics2D.OverlapBox` sizes.

## RoboSpider

`RoboSpiderController2D` is not a surface-raycast AI anymore. It is a deterministic closed-loop
path follower.

- On spawn, searches `groundLayer` within `spawnSearchRadius`.
- Supports `CompositeCollider2D`, `PolygonCollider2D`, and `BoxCollider2D`.
- Builds a private runtime `SurfaceLoop` from the closest collider path and stores
  `distanceAlongLoop`.
- Moves forever by advancing `distanceAlongLoop` with `crawlSpeed * initialFacingDirection`.
- Samples the loop for surface position, tangent, and outward normal, then places the root at
  `surfacePoint + normal * surfaceOffset`.
- Rotates so the spider's local down points toward the surface and local up points away from it.
- Does not chase or turn toward the player.
- Bites in place if the rotated attack box overlaps an `IPlayerDamageReceiver2D`; pauses movement
  for the attack timing, then resumes the same loop direction.
- If no valid loop can be derived, logs one warning, sets speed to zero, and keeps health/death
  behavior safe.

Do not reintroduce `SurfaceProbe`, `ForwardProbe`, corner raycasts, or "find nearby surface"
fallbacks for RoboSpider. Cornering is solved by loop sampling, not by sensing.

## Drone

`DroneController2D` currently inherits the flying ram behavior from `FlyingEnemyController2D`.

- No gravity, free 2D movement.
- Detects only valid player-damage receivers inside its target layer/range.
- Hovers/loiters until a target enters range.
- Runs a windup/dash/recovery ram attack and applies the same player-damage hook.
- Projectile behavior is intentionally not implemented yet.

## Validation

- After controller or prefab changes, refresh/compile Unity and check console errors.
- A quick Play Mode smoke check should clear the console first, enter Play Mode, wait a few
  seconds, then inspect warnings/errors.
- For RoboSpider specifically, absence of the missing-loop warning means it derived a nearby
  Ground loop successfully at startup.
