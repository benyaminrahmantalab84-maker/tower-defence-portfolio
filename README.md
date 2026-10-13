# Tower Defence — Unity & Blender Portfolio

**Benyamin (Billy) Rahmantalab · Unity Game Developer & 3D Game Artist**

A tower defence project combining modular C# gameplay systems with original Blender game assets. This showcase presents selected engineering work and publicly available artwork from an **in-development prototype**.

**Unity 6000.2.2f1 · C# · Blender · URP · AI Navigation**

[Contact me on LinkedIn](https://www.linkedin.com/in/benyamin-rahmantalab-650675394/)

## Visual work

| Project work | What it demonstrates | View |
| --- | --- | --- |
| Tower designs and upgrade variants | Modeling, animation, and readable visual progression for machine gun, laser, and rocket towers | [Tower showcase](https://www.linkedin.com/feed/update/urn:li:activity:7503443533014519808/) |
| Helicopter Base | Blender modeling, rigging, landing animations, and procedural materials | [Helicopter showcase](https://www.linkedin.com/feed/update/urn:li:activity:7513184259763785728/) |

## Engineering highlights

- **Modular towers:** targeting, weapon cadence, attack execution, aiming, visuals, and progression have separate responsibilities.
- **Owner-specific progression:** upgrades are keyed by owner and tower type, so an upgrade can synchronize that owner's existing towers and initialize later towers at the same level.
- **Explicit match outcomes:** a C# state model collects commands for a simulation tick, applies eliminations before arrivals, and locks the final winner or draw before notifying listeners.
- **NavMesh navigation:** enemies select reachable registered targets, spread their first queries over time, and limit path checks per target refresh.
- **Grid authoring:** logical occupancy and cached terrain heights support tower and road placement; Editor tooling lets the developer author Base and Destination footprints.
- **Enemy spawning foundation:** cached NavMesh triangles supply candidate positions; bounded clearance and target-reachability checks precede pooled spawning.

These describe code present in the development workspace. They are not claims of a completed game, validated mobile capacity, or a shipped release.

## Review the code

| Sample | Engineering focus |
| --- | --- |
| [MatchFlowState.cs](samples/MatchFlowState.cs) | State transitions, batched commands, immutable result snapshots, callback guards |
| [TowerProgressionIdentity.cs](samples/TowerProgressionIdentity.cs) | Value-based owner/type identity and composite dictionary keys |
| [TowerProgressionService.cs](samples/TowerProgressionService.cs) | Group registration, shared upgrade state, lifecycle and callback handling |
| [EnemyNavMeshMovement.cs](samples/EnemyNavMeshMovement.cs) | Bounded path selection, target refresh timing, agent lifecycle |

Start with [the code sample guide](samples/README.md), then [the architecture notes](docs/architecture.md).

The samples are unchanged copies of selected project files. The Unity-dependent samples reference systems from the private project and are provided for code review, not as a complete Unity package.

## Gameplay direction

The planned match has **one player and three AI factions**, each with a Base. Towers defend while roads support a damageable Package's delivery to a shared Destination. The first eligible delivery wins; destroying a Base eliminates its faction. Enemies use NavMesh rather than roads.

The full delivery loop, XP and random level-up offers, AI decision behavior, complete map integration, and final presentation remain development work. No playable build is included in this showcase.

## Project work

This project brings together gameplay design, Unity development and scene integration, and Blender modeling, rigging, and animation. The selected samples show the structure of the current code; the linked artwork shows the visual side of the project.

**Available for freelance Unity development, playable prototypes, and Blender game art.**

## Snapshot

Prepared on **13 October 2026** from the current local development workspace. The original game repository remains private. This separate showcase includes no game scenes, prefabs, models, textures, development logs, or repository history.

No Unity compilation, automated test, Play Mode run, build, or device benchmark was executed while preparing this showcase. The target of approximately 1,000 enemies is a future profiling objective, not a measured result.
