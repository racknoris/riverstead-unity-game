using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using UnityEngine;

namespace Contraption.Runtime.Catalog
{
    /// <summary>
    /// Everything the game knows about one kind of part, in one asset.
    ///
    /// This is the boundary object described in `ARCHITECTURE.md` §9: it holds the Unity-side
    /// tuning the simulation builder needs, and exposes a plain <see cref="PartDefinition"/> so
    /// the domain can validate and score without seeing a Unity type. Tuning lives here rather
    /// than in constants scattered across components, so changing how a wheel feels is an asset
    /// edit, not a code change.
    ///
    /// Not every field applies to every part — a beam has no motor. That is deliberate: one asset
    /// type with an unused field is simpler than a hierarchy of part-specific assets, and the POC
    /// has seven parts, not seventy.
    /// </summary>
    [CreateAssetMenu(menuName = "Contraption/Part Definition", fileName = "PartDefinition")]
    public sealed class PartDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private PartType _partType = PartType.Beam;
        [SerializeField] private string _displayName = string.Empty;

        [Tooltip("Ids of the holes this part offers for others to attach to.")]
        [SerializeField] private string[] _attachmentHoleIds = new string[0];

        [Header("Domain (validation and scoring)")]
        [SerializeField] private float _mass = 1f;

        [Tooltip("Budget cost, for the 'use fewer parts' scoring input.")]
        [SerializeField] private int _cost = 1;

        [Header("Presentation")]
        [Tooltip("Assigned in Milestone 4, when the simulation builders exist.")]
        [SerializeField] private GameObject _prefab;

        [Header("Physics")]
        [Tooltip("Wheels need grip; an unmaterialed wheel spins in place. See docs/CONVENTIONS.md.")]
        [SerializeField] private float _friction = 1f;

        [SerializeField] private float _radius;
        [SerializeField] private Vector2 _size = Vector2.one;

        [Header("Motor (powered parts only)")]
        [Tooltip("Sets drive speed. Positive drives +X - see docs/ISSUES.md L2.")]
        [SerializeField] private float _motorSpeedDegreesPerSecond;

        [Tooltip("Buys climbing and load capacity, NOT speed. See docs/ISSUES.md.")]
        [SerializeField] private float _maxMotorTorque;

        [Header("Hinge (hinged parts only)")]
        [SerializeField] private float _hingeLimitMinDegrees = -45f;
        [SerializeField] private float _hingeLimitMaxDegrees = 45f;

        [Header("Spring (sprung parts only)")]
        [SerializeField] private float _springFrequency;
        [SerializeField] private float _springDampingRatio;

        public PartType PartType => _partType;

        public string DisplayName => _displayName;

        public GameObject Prefab => _prefab;

        public float Friction => _friction;

        public float Radius => _radius;

        public Vector2 Size => _size;

        public float MotorSpeedDegreesPerSecond => _motorSpeedDegreesPerSecond;

        public float MaxMotorTorque => _maxMotorTorque;

        public float HingeLimitMinDegrees => _hingeLimitMinDegrees;

        public float HingeLimitMaxDegrees => _hingeLimitMaxDegrees;

        public float SpringFrequency => _springFrequency;

        public float SpringDampingRatio => _springDampingRatio;

        /// <summary>
        /// Projects this asset onto the plain domain model. Returns null if the asset is not
        /// filled in well enough to make a valid definition, so a broken asset surfaces as a
        /// catalog validation problem rather than as an exception during a build.
        /// </summary>
        public PartDefinition ToDomainDefinition()
        {
            if (string.IsNullOrWhiteSpace(_displayName) || _mass <= 0f)
            {
                return null;
            }

            var holes = new List<HoleId>(_attachmentHoleIds.Length);
            foreach (string holeId in _attachmentHoleIds)
            {
                if (string.IsNullOrWhiteSpace(holeId))
                {
                    return null;
                }

                holes.Add(new HoleId(holeId));
            }

            return new PartDefinition(_partType, _displayName, _mass, _cost, holes);
        }
    }
}
