# Conventions

Style and project conventions, aligned with Unity's published C# style guide and project-organization/version-control best-practice guides. When this file and personal taste disagree, this file wins; when this file is silent, follow Microsoft C# conventions.

## C# style

- **Naming:** PascalCase for types, methods, properties, events, and public fields; camelCase for locals and parameters; `_camelCase` for private fields; `I` prefix for interfaces; PascalCase enum types with PascalCase members. No Hungarian notation, no abbreviations that save three letters.
- One public type per file; file name matches the type.
- `var` only when the type is obvious from the right-hand side.
- Braces on all control blocks, including single-line `if`.
- Prefer `readonly`, records, and immutable collections in the Domain assembly. No public mutable fields in domain types.
- Events: `System.Action`/`event` with past-tense names for facts (`PartPlaced`), imperative names for requests (`PlacePart`).
- No magic numbers in gameplay code — tuning values live in `PartDefinitionAsset` or a named constant with a comment saying why.
- Nullable reference types enabled in the Domain assembly.

## Unity-specific code rules

- No `GameObject.Find`, `FindObjectOfType`, or string-based lookups in gameplay code; wire references in the Inspector or through builders.
- No `GetComponent`, allocation, LINQ, or string concatenation inside `Update`/`FixedUpdate` hot paths; cache in `Awake`.
- Physics reads/writes only in `FixedUpdate`; input reads in `Update`.
- Gameplay randomness uses the seeded `System.Random` in Domain; `UnityEngine.Random` is for cosmetic effects only.
- Coroutines for simple timing; no async/await in MonoBehaviours in the POC.
- `[SerializeField] private` instead of public fields for Inspector exposure.

## Assemblies

- `Contraption.Domain` must not reference UnityEngine — this is the load-bearing rule of the whole project.
- Dependency direction: UI → Runtime → Domain. Never the reverse. `Spike` is referenced by nothing.
- New asmdefs require a recorded deviation in `docs/TASKS.md`.

## Assets and folders

- All project content under `Assets/_Project/`; third-party assets stay in their own top-level folders and are never edited in place.
- Prefabs: PascalCase noun (`ChassisBasic`, `WheelPowered`). Variants: `Base_Variant` (`Wheel_Powered`).
- ScriptableObject assets: type-suffixed (`WheelDefinition`, `PartCatalog`). One `PartCatalog` asset, ever.
- Scenes: `Main`, `Spike` (temporary). No numbered scene copies (`Main2`, `Main_final`) — that is what git is for.
- Sprites/textures under `Art/`, imported at POC quality; no polish passes.

## Version control

- Force Text serialization, Visible Meta Files (set in Milestone 0). Meta files are always committed.
- Git LFS for textures, audio, and any binary asset.
- Standard Unity `.gitignore` (`Library/`, `Temp/`, `Logs/`, `UserSettings/`, build output).
- Small, coherent commits; message = imperative summary, body says why if non-obvious.
- Never commit a broken domain test suite. Scenes and prefabs are edited by one change per commit to keep YAML diffs reviewable.

## Tests

- Edit-mode tests for everything in Domain (models, validation, serialization, scoring). These are the cheap, fast majority.
- Play-mode tests only where physics or lifecycle is the subject (joint verification, simulation rebuild leak test).
- Test names: `Method_Condition_Expectation`.
- A regression test is only trusted after it has been observed to fail against the defect it guards.

## Physics settings (record, don't scatter)

- Fixed timestep and solver iteration counts are set once in Project Settings,
  recorded here when tuned in Milestone 1, and revisited during Milestone 10
  device profiling:
  - Fixed Timestep: *(default 0.02 until tuned)*
  - Velocity / Position iterations: *(record after checkpoint)*