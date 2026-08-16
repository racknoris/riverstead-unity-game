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

## Building and running

### In the editor

Open `Assets/_Project/Scenes/Spike.unity` and press Play. Play mode runs whatever scene is
currently *open*, which is not necessarily the one a player build uses — a build uses scene 0
from Build Settings. Opening the wrong scene renders a bare background and looks like a broken
build.

The scene contains a single `SpikeBootstrap` object; the camera, course and contraption are all
created at runtime, so an almost-empty Hierarchy before pressing Play is correct.

`R` or `Space` restarts, `Tab` cycles variants. On a touch device, use the on-screen buttons.

### Desktop player

From the editor: **File → Build Profiles**, select macOS, **Build**. Scene 0 is already set.

From the command line — Unity must be **closed**, since the CLI needs the project lock:

```sh
"/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "$(pwd)" \
  -buildOSXUniversalPlayer "$HOME/Desktop/SpikeMac.app" \
  -logFile "$(pwd)/Logs/build-mac.log"
```

Unsigned builds are blocked by Gatekeeper on first launch; right-click → Open gets past it.

Judge anything about feel or frame pacing in a player build, not in the editor. The editor adds
judder of its own (domain reload, Scene view rendering alongside Game view, VSync interaction)
which can only add artefacts, never hide them.

### Android

Scripts compile against the Android target and the SDK and NDK ship with the installed module,
so an APK builds from the editor with no extra setup. To verify compilation without building:

```sh
"/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit -projectPath "$(pwd)" \
  -buildTarget Android -logFile "$(pwd)/Logs/android.log"
```

### Unity Web

Not yet verified. There is no CLI flag for a WebGL build, so it needs a small editor script
calling `BuildPipeline`, which does not exist yet. See `docs/TASKS.md` Milestone 10 — and note
that Web diverges structurally at Milestone 9, where `persistentDataPath` is backed by IndexedDB.

### Platform support on this machine

Only `AndroidPlayer`, `MacStandaloneSupport` and `WebGLSupport` are installed — there is no
Windows Build Support. See `docs/ISSUES.md` #1.

## Project settings of record

Tuned values — fixed timestep, solver iteration counts, joint tuning rules — live in
[docs/CONVENTIONS.md](docs/CONVENTIONS.md) and are **not** restated here. They change during
tuning, and a second copy in this file would go stale silently. (It already did once: this
section used to name the pre-tuning solver iteration counts and was left behind when they were
raised in Milestone 1.)
