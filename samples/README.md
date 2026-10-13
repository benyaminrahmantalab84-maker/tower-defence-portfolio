# Code sample guide

These are unchanged source snapshots, selected for reading in a portfolio review. They are not a complete Unity project or an installable package.

## 1. Match outcome policy

Read `TowerProgressionIdentity.cs` and `MatchFlowState.cs` together. They use the .NET base libraries and have no Unity API dependency.

Review points:

- Exactly four unique, valid faction IDs are required.
- Commands are collected under one explicit simulation tick.
- Eliminations resolve before delivery arrivals, independent of submission order.
- Multiple eligible same-tick arrivals produce a draw; one survivor alone does not win.
- Completed results are locked before event callbacks.
- Reset and command submission are guarded during notification dispatch.

The game must provide an end-of-tick coordinator. This sample does not schedule simulation ticks or move Packages.

## 2. Shared tower progression

`TowerProgressionService.cs` groups registered towers by `(OwnerId, TowerTypeId)`. The group retains a level even before towers exist. Registering a tower synchronizes it with that group; upgrading a group works on a snapshot of its membership so lifecycle callbacks can change registrations safely.

Dependencies retained in the private Unity project include `TowerDefinition`, `TowerRoot`, `TowerOwnerContext`, and `TowerLevelController`. They are not included here. This file cannot be imported by itself as a working component.

## 3. Enemy NavMesh movement

`EnemyNavMeshMovement.cs` consumes a scene-bound registry and a `NavMeshAgent`. It orders registered candidates by distance and checks a limited number of paths per refresh, moving past unreachable candidates across successive refreshes. Initial target queries are staggered across the configured interval.

Dependencies retained in the private Unity project include `EnemyTargetRegistry` and `EnemyTargetMarker`. A compatible baked NavMesh and properly authored targets are also required. Enemy attack and HP behavior are outside this movement sample.

## Performance limits to evaluate

The code has performance-conscious timing and query limits, but these are not benchmark results. The registry implementation, candidate count, search radius, sorting, NavMesh settings, and target device determine actual navigation cost. Tower group synchronization is proportional to the upgraded group's membership. No 1,000-enemy capacity is claimed.

## Source and validation

See `../docs/source-manifest.json` for original relative paths and SHA-256 values. The files were inspected as source and copied without editing. No compile or runtime test was executed for this publication; this guide is not evidence of a passing test run.
