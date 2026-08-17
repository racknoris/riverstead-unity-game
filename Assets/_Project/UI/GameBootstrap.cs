using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Domain.Editing;
using Contraption.Domain.Flow;
using Contraption.Runtime.Catalog;
using Contraption.Runtime.Simulation;
using Contraption.Runtime.Views;
using UnityEngine;

namespace Contraption.UI
{
    /// <summary>
    /// Composition root for Milestone 5: wires the catalog, the simulation builder, the phase
    /// machine and the HUD together, and drives the run.
    ///
    /// **Everything world-related here is throwaway** — the ground, the finish line and the
    /// hard-coded blueprint all exist so the phase cycle has something real to run against.
    /// Milestone 8 replaces them with an actual level; see `docs/TASKS.md` D8. None of it lives
    /// in `Contraption.Runtime`, so replacing it is a deletion rather than an untangling.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private PartCatalog _catalog = null!;
        [SerializeField] private HudView _hud = null!;
        [SerializeField] private EditorPanelView _editorPanel = null!;
        [SerializeField] private BlueprintPreview _preview = null!;
        [SerializeField] private EditorTouchInput _touchInput = null!;

        [Header("Placeholder world (Milestone 8 replaces this)")]
        [SerializeField] private float _finishLineX = 40f;
        [SerializeField] private float _timeLimitSeconds = 40f;

        private GameFlow _flow = null!;
        private SimulationBuilder _builder = null!;
        private SimulationRoot? _simulation;
        private LevelDefinition _level = null!;
        private ContraptionBlueprint _blueprint = null!;
        private Transform _worldRoot = null!;
        private Camera _camera = null!;
        private Vector3 _cameraHome;
        private float _elapsedSeconds;
        private ContraptionEditor _editor = null!;
        private PartId? _selectedPartId;
        private PartId? _pendingHolePartId;
        private HoleId _pendingHoleId;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            _level = new LevelDefinition("level-01", "Placeholder Straight", _timeLimitSeconds, maxParts: 12);
            _builder = new SimulationBuilder(_catalog);

            // The player starts from a bare chassis and builds outward; the machine is theirs to
            // make, not a rover handed to them.
            _editor = new ContraptionEditor(PlaceholderBlueprints.BareChassis(_level.LevelId), DomainDefinitions());
            _editor.BlueprintChanged += _ => RenderPreview();
            _blueprint = _editor.Blueprint;

            _flow = new GameFlow();
            _flow.PhaseChanged += OnPhaseChanged;

            _camera = Camera.main;
            _cameraHome = _camera.transform.position;
            BuildPlaceholderWorld();

            _preview.Initialise(_catalog);
            _touchInput.Initialise(_camera, _preview, () => _flow.Phase == GamePhase.Editing ? _editor.Blueprint : null);
            _touchInput.HoleTapped += OnHoleTapped;
            _touchInput.PartTapped += OnPartTapped;
            _touchInput.EmptySpaceTapped += OnEmptySpaceTapped;

            _editorPanel.PartTypeChosen += OnPartTypeChosen;
            _editorPanel.RotateRequested += OnRotateRequested;
            _editorPanel.RemoveRequested += OnRemoveRequested;
            _editorPanel.PaletteDismissed += OnPaletteDismissed;

            _hud.RunRequested += OnRunRequested;
            _hud.PauseRequested += OnPauseRequested;
            _hud.ResetRequested += OnResetRequested;

            RenderPreview();
        }

        private void OnDestroy()
        {
            if (_hud != null)
            {
                _hud.RunRequested -= OnRunRequested;
                _hud.PauseRequested -= OnPauseRequested;
                _hud.ResetRequested -= OnResetRequested;
            }

            _touchInput.HoleTapped -= OnHoleTapped;
            _touchInput.PartTapped -= OnPartTapped;
            _touchInput.EmptySpaceTapped -= OnEmptySpaceTapped;
            _editorPanel.PartTypeChosen -= OnPartTypeChosen;
            _editorPanel.RotateRequested -= OnRotateRequested;
            _editorPanel.RemoveRequested -= OnRemoveRequested;
            _editorPanel.PaletteDismissed -= OnPaletteDismissed;

            _flow.PhaseChanged -= OnPhaseChanged;
            // Leaving physics in Script mode would freeze whatever loads next, since nothing
            // else calls Physics2D.Simulate.
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }

        // Input and UI in Update; physics reads stay in FixedUpdate (`docs/CONVENTIONS.md`).
        private void Update()
        {
            // Run time is accumulated explicitly rather than read from Time.time, because pausing
            // no longer stops the clock - see the note on OnPhaseChanged.
            if (_flow.Phase == GamePhase.Running)
            {
                _elapsedSeconds += Time.deltaTime;
            }

            EvaluateRun();
            _hud.Show(_flow, ElapsedSeconds, _timeLimitSeconds);
        }

        /// <summary>
        /// The camera follows in LateUpdate, after Rigidbody2D interpolation has been applied.
        /// Following in Update reads the raw stepped pose and reintroduces judder
        /// (`docs/ISSUES.md` L3).
        ///
        /// With no simulation to follow — between runs, after a reset — it eases back to its
        /// starting position rather than staying stranded wherever the last run ended. Reset means
        /// the whole view returns to the start, not just the machine.
        /// </summary>
        private void LateUpdate()
        {
            Vector3 target = _cameraHome;
            if (_simulation != null
                && _simulation.TryGetBody(PlaceholderBlueprints.ChassisId, out Rigidbody2D chassis))
            {
                target = new Vector3(chassis.position.x + 3f, chassis.position.y + 1.5f, _cameraHome.z);
            }

            Vector3 position = _camera.transform.position;
            position.x = Mathf.Lerp(position.x, target.x, Time.deltaTime * 3f);
            position.y = Mathf.Lerp(position.y, target.y, Time.deltaTime * 2f);
            _camera.transform.position = position;
        }

        private float ElapsedSeconds => _elapsedSeconds;

        private void OnRunRequested()
        {
            _elapsedSeconds = 0f;
            // The run is built from whatever the player has made, not from a fixture.
            _blueprint = _editor.Blueprint;
            _flow.StartRun();
        }

        // -------------------------------------------------------------------------------------
        // Editing. Every change goes through ContraptionEditor and comes back as a new blueprint
        // or a refusal the player can read (`ARCHITECTURE.md` §6.3).
        // -------------------------------------------------------------------------------------

        private void OnHoleTapped(PartId partId, HoleId holeId)
        {
            if (_flow.Phase != GamePhase.Editing)
            {
                return;
            }

            _pendingHolePartId = partId;
            _pendingHoleId = holeId;
            _editorPanel.ShowPalette();
        }

        private void OnPartTapped(PartId partId)
        {
            if (_flow.Phase != GamePhase.Editing)
            {
                return;
            }

            _selectedPartId = partId;
            _editorPanel.HidePalette();
            _pendingHolePartId = null;
            RenderPreview();
        }

        private void OnEmptySpaceTapped()
        {
            if (_flow.Phase != GamePhase.Editing)
            {
                return;
            }

            _selectedPartId = null;
            _pendingHolePartId = null;
            _editorPanel.HidePalette();
            RenderPreview();
        }

        private void OnPartTypeChosen(PartType partType)
        {
            if (_pendingHolePartId is null)
            {
                return;
            }

            ApplyEdit(_editor.PlacePart(_pendingHolePartId.Value, _pendingHoleId, partType));
            _pendingHolePartId = null;
            _editorPanel.HidePalette();
        }

        private void OnRotateRequested()
        {
            if (_selectedPartId.HasValue)
            {
                ApplyEdit(_editor.RotatePart(_selectedPartId.Value));
            }
        }

        private void OnRemoveRequested()
        {
            if (!_selectedPartId.HasValue)
            {
                return;
            }

            EditResult result = _editor.RemovePart(_selectedPartId.Value);
            if (result.Accepted)
            {
                _selectedPartId = null;
            }

            ApplyEdit(result);
        }

        private void OnPaletteDismissed()
        {
            _pendingHolePartId = null;
            _editorPanel.HidePalette();
        }

        /// <summary>A refusal is shown, never swallowed (`docs/ISSUES.md`).</summary>
        private void ApplyEdit(EditResult result)
        {
            if (!result.Accepted)
            {
                _editorPanel.ShowRejection(result.RejectionReason!);
                return;
            }

            RenderPreview();
        }

        private void RenderPreview()
        {
            bool editing = _flow == null || _flow.Phase == GamePhase.Editing;
            _editorPanel.SetVisible(editing);

            if (!editing)
            {
                _preview.Clear();
                return;
            }

            _preview.Render(_editor.Blueprint, _selectedPartId);
            _editorPanel.ShowSelection(FindSelected(), SelectedDisplayName(), IsSelectedChassis());
        }

        private PlacedPart? FindSelected()
        {
            if (!_selectedPartId.HasValue)
            {
                return null;
            }

            foreach (PlacedPart part in _editor.Blueprint.Parts)
            {
                if (part.Id == _selectedPartId.Value)
                {
                    return part;
                }
            }

            return null;
        }

        private string? SelectedDisplayName()
        {
            PlacedPart? part = FindSelected();
            return part != null && _catalog.TryGetDefinition(part.Type, out PartDefinitionAsset asset)
                ? asset.DisplayName
                : null;
        }

        private bool IsSelectedChassis() => FindSelected()?.Type == PartType.Chassis;

        private IReadOnlyDictionary<PartType, PartDefinition> DomainDefinitions()
        {
            var definitions = new Dictionary<PartType, PartDefinition>();
            foreach (PartDefinitionAsset asset in _catalog.Definitions)
            {
                if (asset == null)
                {
                    continue;
                }

                PartDefinition definition = asset.ToDomainDefinition();
                if (definition != null)
                {
                    definitions[asset.PartType] = definition;
                }
            }

            return definitions;
        }

        private void OnPauseRequested()
        {
            if (_flow.CanPause)
            {
                _flow.Pause();
            }
            else if (_flow.CanResume)
            {
                _flow.Resume();
            }
        }

        private void OnResetRequested()
        {
            if (_flow.Phase == GamePhase.Completed || _flow.Phase == GamePhase.Failed)
            {
                _flow.ReturnToEditing();
            }
            else if (_flow.CanReset)
            {
                _flow.Reset();
            }
        }

        /// <summary>
        /// The view layer's half of a phase change. The domain says *what* phase we are in; this
        /// decides what that means for GameObjects and time.
        /// </summary>
        private void OnPhaseChanged(GamePhase from, GamePhase to)
        {
            RenderPreview();

            switch (to)
            {
                case GamePhase.Running:
                    // Resuming from Paused must not rebuild the machine mid-run.
                    if (from != GamePhase.Paused)
                    {
                        RebuildSimulation();
                    }

                    SetPhysicsRunning(true);
                    break;

                case GamePhase.Paused:
                    SetPhysicsRunning(false);
                    break;

                case GamePhase.Editing:
                    SetPhysicsRunning(true);
                    DestroySimulation();
                    break;

                case GamePhase.Completed:
                case GamePhase.Failed:
                    // The machine is left standing, and frozen, so the player can read how it
                    // ended rather than watching it roll on past the finish.
                    SetPhysicsRunning(false);
                    break;
            }
        }

        /// <summary>
        /// Freezes or resumes the simulation by switching whether Unity steps Physics 2D at all.
        ///
        /// Deliberately *not* <c>Time.timeScale = 0</c>. That is a global hammer: it stops
        /// animation, effects and every <c>Time.deltaTime</c> in the project, so anything that
        /// should stay alive during a pause has to be rewritten against unscaled time. Freezing
        /// physics alone leaves the rest of the game running normally, which is what a pause
        /// actually means here.
        ///
        /// In <c>Script</c> mode Unity steps physics only when asked, and nothing asks, so the
        /// simulation holds still. The cost is that the run clock no longer stops by itself —
        /// hence the explicit accumulation in Update.
        /// </summary>
        private static void SetPhysicsRunning(bool running)
        {
            Physics2D.simulationMode = running
                ? SimulationMode2D.FixedUpdate
                : SimulationMode2D.Script;
        }

        /// <summary>Reset is destroy-and-rebuild, never a rewind (`ARCHITECTURE.md` §8).</summary>
        private void RebuildSimulation()
        {
            DestroySimulation();
            _simulation = _builder.Build(_level, _blueprint);
        }

        private void DestroySimulation()
        {
            if (_simulation != null)
            {
                _simulation.DestroySimulation();
                _simulation = null;
            }
        }

        private void EvaluateRun()
        {
            if (_flow.Phase != GamePhase.Running || _simulation == null)
            {
                return;
            }

            if (!_simulation.TryGetBody(PlaceholderBlueprints.ChassisId, out Rigidbody2D chassis))
            {
                return;
            }

            int partsUsed = _blueprint.Parts.Count;

            if (chassis.position.x >= _finishLineX)
            {
                // No cargo yet, so health is reported as full. Milestone 8 makes it real.
                _flow.Complete(RunResult.Completed(100f, ElapsedSeconds, partsUsed));
                return;
            }

            if (chassis.position.y < FallLimitY)
            {
                _flow.Fail(RunResult.Failed("Machine fell out of the world", 0f, ElapsedSeconds, partsUsed));
                return;
            }

            if (ElapsedSeconds >= _timeLimitSeconds)
            {
                _flow.Fail(RunResult.Failed("Out of time", 100f, ElapsedSeconds, partsUsed));
            }
        }

        private const float FallLimitY = -20f;

        private void BuildPlaceholderWorld()
        {
            var world = new GameObject("PlaceholderWorld");
            _worldRoot = world.transform;

            CreateSlab("Ground", new Vector2(20f, -1f), new Vector2(80f, 2f), solid: true);
            CreateSlab("BackWall", new Vector2(-6f, 1f), new Vector2(1f, 6f), solid: true);
            // The finish marker must NOT be solid. Built with a collider, the machine crashes
            // into its own finish line and can never reach it - which presents as "the rover
            // won't drive" and cost a debugging cycle.
            CreateSlab("FinishMarker", new Vector2(_finishLineX, 1.5f), new Vector2(0.3f, 5f), solid: false);
        }

        private void CreateSlab(string name, Vector2 centre, Vector2 size, bool solid)
        {
            var slab = new GameObject(name);
            slab.transform.SetParent(_worldRoot, worldPositionStays: false);
            slab.transform.localPosition = centre;

            if (solid)
            {
                BoxCollider2D collider = slab.AddComponent<BoxCollider2D>();
                collider.size = size;
                // Wheels need grip or they spin in place (`docs/CONVENTIONS.md`).
                collider.sharedMaterial = new PhysicsMaterial2D($"{name}Grip") { friction = 1f, bounciness = 0f };
            }

            var visual = new GameObject("View");
            visual.transform.SetParent(slab.transform, worldPositionStays: false);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderBlueprints.SquareSprite();
            renderer.color = name == "FinishMarker"
                ? new Color(0.35f, 0.80f, 0.45f)
                : new Color(0.30f, 0.34f, 0.38f);
        }
    }
}
