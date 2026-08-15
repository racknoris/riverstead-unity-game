# Contraption Game POC Architecture (Unity)

## 1. Purpose

This document defines the architecture for a touch-first 2D contraption-building game POC built in Unity.

The core gameplay loop is:

```text
build -> run simulation -> succeed or fail -> modify -> retry
```

The POC should prove that repeatedly rebuilding and testing a machine is enjoyable before adding accounts, multiplayer, procedural levels, combat modes, or a backend.

## 2. POC Scope

One short obstacle course where the player builds an autonomous machine that carries fragile cargo to a finish area while a simple enemy launcher fires slow projectiles.

The player can:

- attach parts to predefined holes on a chassis;
- rotate parts in fixed angle increments;
- connect supported parts;
- run the simulation;
- pause or reset it;
- modify the blueprint and try again.

A run succeeds when the cargo reaches the finish with health remaining.

Possible score inputs: cargo health remaining, completion time, part count or cost.

## 3. Engine and Target Platforms

- **Unity 6.5** (Supported release). Upgrade to the next LTS only when locking production, not during the POC.
- **2D URP** project template. The Built-In Render Pipeline is deprecated; do not use it.
- Landscape-only on mobile; fixed 16:9 camera.

### Platform roles

| Platform | Role | Gate? |
| --- | --- | --- |
| Windows/desktop player | Development loop | Yes |
| Android native | Primary touch target; where touch feel is judged | Yes |
| Unity Web (desktop browser) | Tester link for anyone who will not install | Yes |
| Mobile browser | Bonus. Nice if it works | No |

Do not depend on browser-only APIs. Do not tune against mobile browser.

## 4. Core Stack

- Unity 6.5, 2D URP template.
- **Physics 2D** (built-in Box2D) for rigid bodies and joints.
- **Input System** package for touch and mouse.
- **UI Toolkit** for HUD and editor panels (uGUI acceptable for world-space touch handles if needed).
- **Unity Test Framework** for edit-mode and play-mode tests.
- **com.unity.nuget.newtonsoft-json** for blueprint serialization (JsonUtility cannot handle dictionaries or polymorphism).

Do not add DOTS/ECS, Addressables, a DI container, or a reactive framework in the POC. Add a package only after recording the decision in `TASKS.md`.

## 5. Central Architectural Rule

```text
Domain assembly (pure C#) owns meaningful immutable application and editor state.

MonoBehaviours own presentation, lifecycle, and local runtime behaviour.

Physics 2D owns mutable physics state.

Persistence stores durable blueprint data only, behind a repository interface.
```

**Enforced at compile time:** the domain assembly (`Contraption.Domain.asmdef`) has *no reference to UnityEngine*. Blueprint models, editor state, game phase, validation, and scoring rules live there. If a file in the domain assembly needs `UnityEngine`, the design is wrong.

Do not copy body position, rotation, velocity, force, or collision state into domain state per frame.

## 6. State Responsibilities

### 6.1 Physics 2D

Source of truth while a simulation runs: body transforms, velocities, forces, contacts, joints, stepping. `Rigidbody2D`, `Joint2D`, and `Collider2D` instances are runtime objects and must never appear inside blueprint models or domain state.

### 6.2 MonoBehaviours (view layer)

Own: sprites and visuals, animation and effects, component lifecycle, collision callbacks, interaction with Rigidbody2D, input forwarding. View components read domain state; they do not own it.

### 6.3 Domain layer

Plain C# classes own: the current `ContraptionBlueprint`, selected part, active editor tool, part configuration, game phase (`Editing / Running / Paused / Completed / Failed`), current level ID, settings, and meaningful gameplay events (plain C# events).

Start with two services only:

- **`ContraptionEditor`** — owns the immutable blueprint; add, move, rotate, remove, connect, disconnect. Every edit returns a new blueprint or a typed rejection with a player-readable reason.
- **`GameFlow`** — a small state machine for the phase, plus an immutable run-result object.

Do not create one service, ScriptableObject event channel, or MonoBehaviour "manager" per part type. A settings service is added only when settings become substantial.

## 7. Immutable Domain Models

```text
ContraptionBlueprint
PlacedPart
Attachment
PartConfiguration
PartDefinition
PartType
LevelDefinition
RunResult
```

Blueprints store only serializable domain data: stable IDs, part types, editor-space positions, snapped rotations, configuration, attachment-hole IDs, connections, and a **schema version**.

Never store in a blueprint: GameObjects, MonoBehaviours, Rigidbody2D/Joint2D/Collider2D, contacts, velocities, animations, or asset references. Parts reference `PartType` by ID; asset lookup happens in the Unity layer.

Use C# records or readonly classes with value equality; use immutable or defensively copied collections.

## 8. Blueprint-to-Simulation Flow

Starting a run creates a fresh simulation from the immutable blueprint:

```text
ContraptionBlueprint
        |
        v
PartViewFactory / BodyBuilder / JointBuilder
        |
        v
GameObjects + Rigidbody2D + Joint2D under a single SimulationRoot
```

- All runtime objects are instantiated under one `SimulationRoot` transform.
- **Reset = destroy the root and rebuild from the unchanged blueprint.** The simulation never mutates the stored blueprint.
- One conceptual entry point: `SimulationBuilder.Build(level, blueprint)`. UI code never constructs physics bodies.
- Joints are created after all bodies exist, in one deterministic pass.

## 9. Part-Definition Registry

- **`PartDefinitionAsset`** (ScriptableObject): tuning values, prefab reference, sprite, per `PartType`.
- **`PartCatalog`** (ScriptableObject): the single registry mapping `PartType -> PartDefinitionAsset`.
- Each asset exposes a plain domain `PartDefinition` so the domain assembly can validate and score without touching Unity types.

Tuning lives in assets, not in code constants scattered across components.

## 10. Construction System

- One chassis with ~8–12 predefined attachment holes.
- Initial parts: normal wheel, powered wheel/motor, beam, rigid connector, hinge, spring, protective plate.
- Rotation snaps to fixed increments (15° or 30°).
- Invalid overlapping placement is rejected **with a player-readable reason**.
- Total parts limited to ~12.
- Every placed part, hole, and attachment has a stable ID.

Joint mapping (verify all of these in the fun checkpoint before trusting them):

| Concept | Unity joint |
| --- | --- |
| Rigid connection / weld | `FixedJoint2D` (or `RelativeJoint2D` if stiffness disappoints) |
| Hinge, with swing limits | `HingeJoint2D` with `useLimits` |
| Powered wheel | `WheelJoint2D` or `HingeJoint2D` with motor |
| Spring | `SpringJoint2D` |

## 11. Determinism and Replay Prep (light)

- Physics runs on the fixed timestep only; gameplay decisions in `FixedUpdate`.
- One seeded `System.Random` in the domain layer; no `UnityEngine.Random` in gameplay logic.
- Do not assume bit-identical re-simulation across platforms. Reliable replay later means periodic state snapshots, not input-only replay.
- Server validation, replay storage, and rankings are out of scope.

## 12. Persistence

- Repository interface in the domain layer: `IContraptionRepository`.
- POC implementation: JSON file in `Application.persistentDataPath` (Unity Web: same API backed by IndexedDB).
- Persist only: current blueprint, current level ID, simple settings — all with a schema version.
- Never persist physics objects, per-frame state, drag state, or transient editor selection. `selectedPartId` resets on startup.
- If multiple save slots, thumbnails, or search arrive later, move to SQLite behind the same interface. Domain code must not know the storage format.

## 13. Folder and Assembly Structure

```text
Assets/_Project/
  Domain/            Contraption.Domain.asmdef   (no UnityEngine)
    Blueprints/  Editing/  Flow/  Validation/
  Runtime/           Contraption.Runtime.asmdef  (refs Domain)
    Simulation/      builders, SimulationRoot
    Views/           part view components
    Catalog/         PartDefinitionAsset, PartCatalog
    Persistence/     JSON repository
  UI/                Contraption.UI.asmdef       (refs Domain, Runtime)
  Spike/             Contraption.Spike.asmdef    (checkpoint only; deleted after verdict)
  Tests/
    EditMode/        Contraption.Tests.EditMode.asmdef (refs Domain)
    PlayMode/        Contraption.Tests.PlayMode.asmdef
  Scenes/  Prefabs/  Settings/
```

Nothing outside `Spike/` may reference `Contraption.Spike`. The asmdef graph enforces every boundary in this document. Do not add empty layers or placeholder abstractions.

## 14. Fun Checkpoint

Before building the editor, run the fun checkpoint (`TASKS.md` Milestone 1): a deliberately throwaway playable slice with hard-coded contraption variants, a crude course, and a one-tap restart, used to record a go/no-go verdict on the core loop **and** to verify Unity's 2D joints under motorised multi-joint load.

Checkpoint code is exempt from the structural rules here, lives entirely in `Spike/`, and is deleted in one commit after the verdict. It must still not leak Unity types into the domain assembly (the asmdef makes this impossible anyway). Tuning constants are re-derived into the catalog deliberately, not copy-pasted.

## 15. Explicit Non-Goals

Accounts; backend; multiplayer/Netcode; server validation; procedural or user-created levels; combat mode; cloud sync; leaderboards; replay storage; multiple save slots; a local database; formal undo/redo command infrastructure; DOTS/ECS; DI containers; event-channel frameworks; one manager per part; live physics state in domain models; polished art.

## 16. POC Success Criterion

The POC succeeds when testers voluntarily retry the same level several times because they want to reach the finish, protect the cargo better, use fewer parts, or finish faster.
