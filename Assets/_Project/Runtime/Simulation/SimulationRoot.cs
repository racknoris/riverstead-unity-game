using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using UnityEngine;

namespace Contraption.Runtime.Simulation
{
    /// <summary>
    /// The single parent every runtime object of a run lives under (`ARCHITECTURE.md` §8).
    ///
    /// Its existence is what makes reset trivial and total: destroying this one object destroys
    /// the entire simulation, with no per-system teardown to forget. Nothing resets state in
    /// place — a run is rebuilt from the unchanged blueprint, never rewound.
    /// </summary>
    public sealed class SimulationRoot : MonoBehaviour
    {
        private readonly Dictionary<PartId, Rigidbody2D> _bodiesByPartId = new Dictionary<PartId, Rigidbody2D>();

        /// <summary>The blueprint this was built from. Held for reference only; never written to.</summary>
        public ContraptionBlueprint Blueprint { get; private set; }

        public LevelDefinition Level { get; private set; }

        public int JointCount { get; private set; }

        public IReadOnlyDictionary<PartId, Rigidbody2D> BodiesByPartId => _bodiesByPartId;

        internal void Initialise(LevelDefinition level, ContraptionBlueprint blueprint)
        {
            Level = level;
            Blueprint = blueprint;
        }

        internal void Register(PartId partId, Rigidbody2D body) => _bodiesByPartId[partId] = body;

        internal void CountJoint() => JointCount++;

        public bool TryGetBody(PartId partId, out Rigidbody2D body) => _bodiesByPartId.TryGetValue(partId, out body);

        /// <summary>
        /// Tears the whole simulation down. Prefer this over destroying the GameObject directly,
        /// so the intent reads clearly at call sites.
        /// </summary>
        public void DestroySimulation()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
