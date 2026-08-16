using UnityEngine;
using UnityEngine.InputSystem;

namespace Contraption.Spike
{
    /// <summary>
    /// Entry point for the Milestone 1 fun checkpoint. Throwaway by design: this whole assembly
    /// is deleted in one commit once the go/no-go verdict is recorded, so it uses IMGUI for the
    /// HUD and builds the world in code rather than earning UI Toolkit and prefabs it will not
    /// live to use.
    ///
    /// It does honour one rule from ARCHITECTURE.md §8 on purpose, because the checkpoint is
    /// meant to test it: everything runtime lives under a single root, and restart destroys that
    /// root and rebuilds from scratch rather than trying to reset state in place.
    /// </summary>
    public sealed class SpikeBootstrap : MonoBehaviour
    {
        private const float RunTimeLimitSeconds = 75f;
        private const float CameraHeight = 3.2f;
        private const float CameraOrthographicSize = 7f;

        private enum RunState
        {
            Running,
            Finished,
            Failed
        }

        private Transform _simulationRoot;
        private Camera _camera;
        private SpikeCargo _cargo;
        private SpikeFinishSensor _finish;
        private Rigidbody2D _chassisBody;

        private SpikeVariant _variant = SpikeVariant.PoweredWheels;
        private RunState _state;
        private string _failReason = string.Empty;
        private float _runStartTime;
        private float _runEndTime;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            CreateCamera();
            Rebuild();
        }

        private void CreateCamera()
        {
            var cameraObject = new GameObject("SpikeCamera");
            cameraObject.transform.SetParent(transform, worldPositionStays: false);

            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = CameraOrthographicSize;
            _camera.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.transform.position = new Vector3(SpikeCourse.StartX, CameraHeight, -10f);
            cameraObject.AddComponent<AudioListener>();
        }

        /// <summary>
        /// Reset = destroy the root and rebuild. Never reset state in place (ARCHITECTURE.md §8).
        /// </summary>
        private void Rebuild()
        {
            if (_simulationRoot != null)
            {
                Destroy(_simulationRoot.gameObject);
            }

            var root = new GameObject("SimulationRoot");
            root.transform.SetParent(transform, worldPositionStays: false);
            _simulationRoot = root.transform;

            _finish = SpikeCourse.Build(_simulationRoot);
            _cargo = SpikeContraptions.Build(_simulationRoot, _variant);
            _chassisBody = _simulationRoot.Find("Chassis").GetComponent<Rigidbody2D>();

            _state = RunState.Running;
            _failReason = string.Empty;
            _runStartTime = Time.time;
            _runEndTime = 0f;
        }

        private void SelectNextVariant()
        {
            int next = ((int)_variant + 1) % System.Enum.GetValues(typeof(SpikeVariant)).Length;
            _variant = (SpikeVariant)next;
            Rebuild();
        }

        // Input is read in Update, physics in FixedUpdate (docs/CONVENTIONS.md).
        private void Update()
        {
            ReadKeyboardShortcuts();
            EvaluateRun();
        }

        /// <summary>
        /// The camera follows in LateUpdate, not Update, because Rigidbody2D interpolation is
        /// applied to transforms after Update runs. Following in Update would read the raw
        /// stepped pose and reintroduce exactly the judder the interpolation removes.
        /// </summary>
        private void LateUpdate()
        {
            FollowWithCamera();
        }

        private void ReadKeyboardShortcuts()
        {
            // Keyboard.current is null on a touch device, which is the primary target.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            {
                Rebuild();
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                SelectNextVariant();
            }
        }

        private void EvaluateRun()
        {
            if (_state != RunState.Running)
            {
                return;
            }

            if (_finish != null && _finish.CargoArrived)
            {
                _state = RunState.Finished;
                _runEndTime = Time.time;
                return;
            }

            if (_cargo == null || _cargo.IsDestroyed)
            {
                Fail("Cargo destroyed");
                return;
            }

            if (_cargo.transform.position.y < SpikeCourse.KillPlaneY)
            {
                Fail("Cargo lost down the gap");
                return;
            }

            if (Time.time - _runStartTime > RunTimeLimitSeconds)
            {
                Fail("Out of time");
            }
        }

        private void Fail(string reason)
        {
            _state = RunState.Failed;
            _failReason = reason;
            _runEndTime = Time.time;
        }

        private void FollowWithCamera()
        {
            Transform target = _cargo != null ? _cargo.transform : (_chassisBody != null ? _chassisBody.transform : null);
            if (target == null)
            {
                return;
            }

            Vector3 position = _camera.transform.position;
            position.x = Mathf.Lerp(position.x, target.position.x + 3f, Time.deltaTime * 3f);
            position.y = Mathf.Lerp(position.y, target.position.y + 1.5f, Time.deltaTime * 2f);
            _camera.transform.position = position;
        }

        private float ElapsedSeconds => (_state == RunState.Running ? Time.time : _runEndTime) - _runStartTime;

        // -------------------------------------------------------------------------------------
        // HUD. IMGUI is the right tool for a throwaway checkpoint: no assets, no scene wiring,
        // and its buttons respond to touch on Android, which is all the restart flow needs.
        // -------------------------------------------------------------------------------------

        private void OnGUI()
        {
            // Scale the HUD up on high-density phone screens, or it is unreadable and untappable.
            float scale = Mathf.Max(1f, Screen.height / 720f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float width = Screen.width / scale;
            DrawStatusPanel();
            DrawButtons(width);

            GUI.matrix = previousMatrix;
        }

        private void DrawStatusPanel()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 420f, 210f), GUI.skin.box);

            GUILayout.Label($"<b>{SpikeContraptions.DisplayName(_variant)}</b>", RichLabel);
            GUILayout.Label(SpikeContraptions.Description(_variant), RichLabel);
            GUILayout.Space(6f);

            float health = _cargo != null ? _cargo.Health : 0f;
            float maxHealth = _cargo != null ? _cargo.MaxHealth : 1f;
            string healthColor = health > 60f ? "#7fdc8f" : health > 25f ? "#e8c86a" : "#e87a7a";
            GUILayout.Label($"Cargo health: <color={healthColor}><b>{health:F0}</b></color> / {maxHealth:F0}", RichLabel);
            GUILayout.Label($"Time: {ElapsedSeconds:F1}s / {RunTimeLimitSeconds:F0}s", RichLabel);

            GUILayout.Space(4f);
            switch (_state)
            {
                case RunState.Finished:
                    GUILayout.Label(
                        $"<color=#7fdc8f><b>FINISHED</b></color>  health {health:F0}, {ElapsedSeconds:F1}s",
                        RichLabel);
                    break;
                case RunState.Failed:
                    GUILayout.Label($"<color=#e87a7a><b>FAILED</b></color>  {_failReason}", RichLabel);
                    break;
                default:
                    GUILayout.Label("Running...", RichLabel);
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawButtons(float width)
        {
            const float ButtonWidth = 210f;
            const float ButtonHeight = 74f;
            float x = width - ButtonWidth - 12f;

            if (GUI.Button(new Rect(x, 12f, ButtonWidth, ButtonHeight), "Restart  (R)"))
            {
                Rebuild();
            }

            if (GUI.Button(new Rect(x, 12f + ButtonHeight + 10f, ButtonWidth, ButtonHeight), "Next variant  (Tab)"))
            {
                SelectNextVariant();
            }
        }

        private GUIStyle _richLabel;

        private GUIStyle RichLabel
        {
            get
            {
                if (_richLabel == null)
                {
                    _richLabel = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 16 };
                }

                return _richLabel;
            }
        }
    }
}
