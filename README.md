# Recoil Platformer

A 2D cyberpunk **recoil-platformer** prototype built in Unity 6. The core mechanic is a
high-recoil shotgun used for both combat *and* traversal — fire downward to launch yourself
upward, chain shots in mid-air, and blast enemies apart along the way. Keyboard/mouse
controls with 8-direction quantized mouse aim.

> **Status:** early prototype / greybox. First milestone is a playable recoil test room with
> enemy and combat iteration. See the [roadmap](.claude/docs/roadmap.md) for what's done and
> what's next.

**▶ [Play in browser](https://lillerupjr.github.io/RecoilPlatformer/)** — latest WebGL build,
auto-deployed from `main`. (Live once the one-time CI setup below is done and the first build
has run.)

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

| Action        | Input                    |
| ------------- | ------------------------ |
| Move          | `A` / `D` (or arrows)    |
| Jump          | Jump button (Space)      |
| Aim           | Mouse (snapped to 8 dir) |
| Fire shotgun  | Left mouse button        |
| Reset to spawn| `Ctrl` / `Cmd` + `R`     |

## Tech stack

- **Unity 6** — note the project uses `rb.linearVelocity` (not the deprecated `rb.velocity`).
- **URP 2D** — sprites use the Sprite-Unlit-Default material.
- **Old Input Manager** (`Input.GetAxisRaw`, `Input.GetButtonDown`) — not the new Input System.
- C# gameplay scripts in `Assets/Scripts/`.
- Craftpix asset packs (cyberpunk player, guns, industrial tileset, seaport enemies, GUI).

## Getting started

1. Open the project in **Unity 6** (matching the editor version the project was created with).
2. Open `Assets/Scenes/SampleScene.unity` — the active greybox room.
3. Press **Play** in the Editor to test. (There is no automated test framework yet; testing is
   done in Play Mode.)

`Assets/Scenes/Template.unity` is the canonical base for new level scenes (scene-per-level);
`level_1.unity` is an in-progress level.

## Play in browser (WebGL build)

A WebGL build is published to **GitHub Pages** at
`https://lillerupjr.github.io/RecoilPlatformer/`, built and deployed automatically by the
[`Deploy WebGL to GitHub Pages`](.github/workflows/deploy-webgl.yml) GitHub Actions workflow
on every push to `main` (and on manual dispatch). The README can only *link* to it — GitHub
strips interactive content from the rendered README itself, so the game can't be embedded
inline.

The build uses Brotli compression with **Decompression Fallback enabled**
(`webGLDecompressionFallback: 1`), which is required for WebGL to load on GitHub Pages — Pages
doesn't send the `Content-Encoding` headers a non-fallback compressed build needs.

### One-time setup (required before the first deploy works)

The workflow builds with [GameCI](https://game.ci/), which needs a Unity license and Pages
enabled:

1. **Add a Unity license to repo secrets** (Settings → Secrets and variables → Actions):
   - *Personal license:* `UNITY_EMAIL`, `UNITY_PASSWORD`, and `UNITY_LICENSE` (the contents of
     the `.ulf` activation file — see GameCI's
     [activation guide](https://game.ci/docs/github/activation)).
   - *Pro/Plus license:* `UNITY_EMAIL`, `UNITY_PASSWORD`, and `UNITY_SERIAL` instead (uncomment
     the `UNITY_SERIAL` line in the workflow).
2. **Enable Pages:** Settings → Pages → Build and deployment → **Source: GitHub Actions**.
3. Push to `main` (or run the workflow manually from the Actions tab). The first run activates
   the license, builds WebGL, and deploys; the Pages URL appears on the workflow's `deploy` job.

> **Note:** the editor version is auto-detected from `ProjectSettings/ProjectVersion.txt`
> (`6000.4.10f1`). A matching GameCI editor Docker image must exist for that version; if the
> build can't find the image, pin `unityVersion` in the workflow to the nearest available one.

## Project structure

```
Assets/
  Scripts/      # Gameplay scripts, by domain:
    Player/     #   PlayerController2D, ShotgunRecoil2D, PlayerHealth2D, VelocityHud2D
    Enemies/    #   RoboDog/RoboSpider/Drone controllers, health, damage interfaces
    Combat/     #   Projectile2D
    Hazards/    #   PistonHazard2D, HazardContactDamage2D
    Camera/     #   CameraFollow2D
    Environment/#   ParallaxLayer2D, AnimatedTile2D
    GameFeel/   #   screen-feel helpers
    UI/         #   PlayerHudBars2D
  Art/          # Backgrounds (parallax), Tiles (Raw, Palettes, TileAssets), UI
  Player/       # Source sprites, slices, animations (Cyborg)
  Prefabs/      # Player, Projectile, Hazards
  Scenes/       # SampleScene (active), Template (base), level_1, dev
  Settings/     # URP render-pipeline assets
```

### Player hierarchy (do not restructure)

```
Player            # Rigidbody2D, BoxCollider2D, PlayerController2D
  Body            # SpriteRenderer, Animator
  GroundCheck     # Transform for ground detection
  ArmPivot        # Aim anchor; the Arm rotates around it to follow the mouse
    Arm           # Weapon-arm SpriteRenderer (ShotgunRecoil2D swaps/rotates the sprite)
```

## Conventions worth knowing

- **Never flip physics roots.** Only flip visual `SpriteRenderer`s (e.g. `Body.flipX`); mirror
  facing-dependent child anchors by local X instead. Flipping a parent breaks colliders.
- Keep physics in `FixedUpdate`, input reading in `Update`; prefer direct `rb.linearVelocity`
  assignment for snappy control.
- Inspector fields are `[SerializeField] private`, grouped with `[Header("…")]`.
- Ground/wall/ledge checks use dimensioned `Physics2D.OverlapBox` against a LayerMask.
- Targeting is filtered by `IPlayerDamageReceiver2D` (not layer masks alone) so enemies don't
  attack each other while they share the `Default` layer.

See [`CLAUDE.md`](CLAUDE.md) for the full set of agent/contributor conventions and
[`.claude/docs/`](.claude/docs) for design notes:

- [`roadmap.md`](.claude/docs/roadmap.md) — milestone roadmap (source of truth)
- [`unity6-notes.md`](.claude/docs/unity6-notes.md) — Unity 6 specifics & camera/parallax conventions
- [`enemy-behavior.md`](.claude/docs/enemy-behavior.md) — enemy asset/controller conventions and AI

## Editor automation (Unity MCP)

The project includes **CoplayDev MCP for Unity** (`com.coplaydev.unity-mcp`). When the Editor
is open with its bridge running, agents can create/modify GameObjects and components,
enter/exit Play Mode, read the console, take Game-view screenshots, and recompile via the
`UnityMCP` server (HTTP at `http://127.0.0.1:8080/mcp`). Note the Editor only ticks frames while
its window is focused.
