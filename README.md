# Recoil Platformer

A 2D cyberpunk **recoil-platformer** prototype built in Unity 6. The core mechanic is a
high-recoil shotgun used for both combat *and* traversal — fire downward to launch yourself
upward, chain shots in mid-air, and blast enemies apart along the way. Keyboard/mouse
controls with 8-direction quantized mouse aim.

> **Status:** early prototype / greybox. First milestone is a playable recoil test room with
> enemy and combat iteration. See the [roadmap](.claude/docs/roadmap.md) for what's done and
> what's next.

**▶ [Play in browser](https://lillerupjr.github.io/RecoilPlatformer/)** — latest WebGL build,
auto-deployed from `main`.

## Core mechanic

The shotgun recoil is the identity of the game. Recoil is applied as a physics impulse in the
opposite direction of your aim, so:

- Aim down and fire → launch up (a "pogo"/recoil jump).
- Shots add to momentum and chain in the air for extended traversal.
- The same weapon fires a 3-pellet spread for combat.

A short control-lockout window after each shot lets the impulse ride before normal movement
input resumes, so recoil actually moves you instead of being erased by ground-movement code.

## Features

- **Movement feel:** coyote time, jump buffering, variable jump height, and a fall multiplier.
- **8-direction aim:** raw mouse angle snapped to 45° octants; the arm sprite swaps + rotates
  to follow the cursor and mirrors with the body's facing.
- **Combat v1:** owner-aware projectiles, enemy/player health, i-frames, and death → respawn.
- **Enemies:** RoboDog (patrol/chase/attack), RoboSpider (surface-loop crawler), and a flying
  Drone ram attacker.
- **Hazards:** piston slams and contact-damage hazards on a dedicated layer.
- **Environment:** multilayer parallax background (Industrial set), orthographic camera follow
  with SmoothDamp, animated tiles, and composite tilemap colliders for seamless geometry.

## Controls

| Action         | Input                    |
| -------------- | ------------------------ |
| Move           | `A` / `D` (or arrows)    |
| Jump           | Jump button (Space)      |
| Aim            | Mouse (snapped to 8 dir) |
| Fire shotgun   | Left mouse button        |
| Reset to spawn | `Ctrl` / `Cmd` + `R`     |

## Tech stack

- **Unity 6** (`6000.4.10f1`) — uses `rb.linearVelocity` (not the deprecated `rb.velocity`).
- **URP 2D** — sprites use the Sprite-Unlit-Default material.
- **Old Input Manager** (`Input.GetAxisRaw`, `Input.GetButtonDown`) — not the new Input System.
- C# gameplay scripts in `Assets/Scripts/`.
- Craftpix asset packs (cyberpunk player, guns, industrial tileset, seaport enemies, GUI).

## Getting started

1. Open the project in **Unity 6** (`6000.4.10f1`).
2. Open `Assets/Scenes/SampleScene.unity` — the active greybox room.
3. Press **Play** in the Editor. (No automated test framework yet; testing is done in Play Mode.)

`Assets/Scenes/Template.unity` is the canonical base for new level scenes (scene-per-level);
`level_1.unity` is an in-progress level.

## WebGL build

A WebGL build is published to GitHub Pages at
`https://lillerupjr.github.io/RecoilPlatformer/`, built and deployed automatically by the
[`Deploy WebGL to GitHub Pages`](.github/workflows/deploy-webgl.yml) workflow on every push to
`main` (and on manual dispatch).

## Project structure

```
Assets/
  Scripts/        # Gameplay scripts, by domain:
    Player/       #   PlayerController2D, ShotgunRecoil2D, PlayerHealth2D, VelocityHud2D
    Enemies/      #   RoboDog/RoboSpider/Drone controllers, health, damage interfaces
    Combat/       #   Projectile2D
    Hazards/      #   PistonHazard2D, HazardContactDamage2D
    Camera/       #   CameraFollow2D
    Environment/  #   ParallaxLayer2D, AnimatedTile2D
    GameFeel/     #   screen-feel helpers
    UI/           #   PlayerHudBars2D
  Art/            # Backgrounds (parallax), Tiles (Raw, Palettes, TileAssets), UI
  Player/         # Source sprites, slices, animations (Cyborg)
  Prefabs/        # Player, Projectile, Hazards
  Scenes/         # SampleScene (active), Template (base), level_1, dev
  Settings/       # URP render-pipeline assets
```
