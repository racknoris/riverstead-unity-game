using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Domain.Validation;
using UnityEngine;

namespace Contraption.Runtime.Catalog
{
    /// <summary>
    /// The single registry mapping <see cref="PartType"/> to its definition
    /// (`ARCHITECTURE.md` §9). There is exactly one of these assets, ever
    /// (`docs/CONVENTIONS.md`).
    ///
    /// Blueprints reference parts by type and never hold asset references, so this is the one
    /// place a type becomes a concrete prefab and set of tuning values.
    /// </summary>
    [CreateAssetMenu(menuName = "Contraption/Part Catalog", fileName = "PartCatalog")]
    public sealed class PartCatalog : ScriptableObject
    {
        [SerializeField] private PartDefinitionAsset[] _definitions = new PartDefinitionAsset[0];

        private Dictionary<PartType, PartDefinitionAsset> _byType;

        public IReadOnlyList<PartDefinitionAsset> Definitions => _definitions;

        /// <summary>
        /// Looks up a part's asset. Returns false rather than throwing, so callers can report a
        /// missing part in their own terms; the catalog validation test is what guarantees this
        /// never fails at runtime for a well-formed build.
        /// </summary>
        public bool TryGetDefinition(PartType partType, out PartDefinitionAsset definition)
        {
            return Index().TryGetValue(partType, out definition);
        }

        /// <summary>Projects the whole catalog into plain domain definitions for validation and scoring.</summary>
        public IReadOnlyList<PartDefinition> ToDomainDefinitions()
        {
            var definitions = new List<PartDefinition>(_definitions.Length);
            foreach (PartDefinitionAsset asset in _definitions)
            {
                definitions.Add(asset == null ? null : asset.ToDomainDefinition());
            }

            return definitions;
        }

        /// <summary>
        /// Human-readable problems with this catalog, empty when it is sound. The rule itself
        /// lives in the domain; this only supplies the data.
        /// </summary>
        public IReadOnlyList<string> Validate() => PartCatalogValidator.Validate(ToDomainDefinitions());

        private Dictionary<PartType, PartDefinitionAsset> Index()
        {
            if (_byType != null)
            {
                return _byType;
            }

            _byType = new Dictionary<PartType, PartDefinitionAsset>(_definitions.Length);
            foreach (PartDefinitionAsset asset in _definitions)
            {
                if (asset != null)
                {
                    // A duplicate would throw here; the validator reports it as a problem
                    // instead, so keep the first and let validation speak.
                    _byType[asset.PartType] = asset;
                }
            }

            return _byType;
        }
    }
}
