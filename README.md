# Riverstead — Contraption Game POC

Touch-first 2D contraption-building game POC. Unity 6.5 (`6000.5.8f1`), 2D URP, Physics 2D.

Design documents are the source of truth and are read in this order:

1. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — structural rules, binding.
2. [docs/TASKS.md](docs/TASKS.md) — milestone sequence and decision gates.
3. [docs/CONVENTIONS.md](docs/CONVENTIONS.md) — style, naming, assets, git, tests.
4. [docs/ISSUES.md](docs/ISSUES.md) — known defects, risks, lessons.

Agent working rules: [CLAUDE.md](CLAUDE.md).

## Layout

All project content lives under [Assets/_Project/](Assets/_Project/). Assembly
boundaries are enforced by asmdefs; the dependency direction is UI → Runtime → Domain,
never the reverse.

| Assembly | Folder | References |
| --- | --- | --- |
| `Contraption.Domain` | `Assets/_Project/Domain/` | **nothing** — `noEngineReferences: true` |
| `Contraption.Runtime` | `Assets/_Project/Runtime/` | Domain |
| `Contraption.UI` | `Assets/_Project/UI/` | Domain, Runtime |
| `Contraption.Spike` | `Assets/_Project/Spike/` | nothing references it; deleted after the Milestone 1 verdict |
| `Contraption.Tests.EditMode` | `Assets/_Project/Tests/EditMode/` | Domain |
| `Contraption.Tests.PlayMode` | `Assets/_Project/Tests/PlayMode/` | Domain, Runtime |

`Contraption.Domain` must never reference UnityEngine. This is enforced twice: by
`noEngineReferences` in the asmdef (compile time) and by
`GamePhaseTests.DomainAssembly_Always_DoesNotReferenceUnityEngine` (test time).

Nullable reference types are enabled in the Domain assembly via
[Assets/_Project/Domain/csc.rsp](Assets/_Project/Domain/csc.rsp).

## Running tests

Unity must not be open on the project when running these — the CLI needs the project lock.

Edit-mode suite (the fast majority; everything in Domain):

```sh
"/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -runTests \
  -projectPath "$(pwd)" \
  -testPlatform EditMode \
  -testResults "$(pwd)/Logs/editmode-results.xml" \
  -logFile "$(pwd)/Logs/editmode.log"
```

Play-mode suite (physics and lifecycle only):

```sh
"/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -runTests \
  -projectPath "$(pwd)" \
  -testPlatform PlayMode \
  -testResults "$(pwd)/Logs/playmode-results.xml" \
  -logFile "$(pwd)/Logs/playmode.log"
```

Exit code `0` means the suite passed; `2` means tests failed. Read the XML for detail:

```sh
grep -o 'result="[^"]*"' Logs/editmode-results.xml | sort | uniq -c
```

On Windows the editor binary is `C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe`;
every other flag is identical.

## Project settings of record

Tuned values are recorded in [docs/CONVENTIONS.md](docs/CONVENTIONS.md), not scattered in code.

- Asset serialization: Force Text. Meta files: Visible. (Both required for reviewable diffs.)
- Fixed timestep: `0.02`. Physics 2D velocity iterations `8`, position iterations `3` — defaults, revisited in Milestone 1.
- Mobile orientation: landscape only (both landscape rotations allowed, portrait disabled).
