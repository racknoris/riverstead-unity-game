using System;
using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Runtime.Catalog;
using NUnit.Framework;
using UnityEditor;

namespace Contraption.Tests.EditMode
{
    /// <summary>
    /// Milestone 3: the *shipped* catalog asset is complete and every entry converts to a valid
    /// domain definition.
    ///
    /// The rule these lean on is tested separately in <see cref="PartCatalogValidatorTests"/>.
    /// This file only asks whether the real asset satisfies it — which is the thing that silently
    /// rots as parts are added.
    /// </summary>
    public sealed class PartCatalogAssetTests
    {
        private const string CatalogPath = "Assets/_Project/Runtime/Catalog/PartCatalog.asset";

        private PartCatalog _catalog = null!;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<PartCatalog>(CatalogPath);
            Assert.That(
                _catalog,
                Is.Not.Null,
                $"There must be exactly one PartCatalog and it must live at {CatalogPath}.");
        }

        [Test]
        public void Catalog_Always_ReportsNoValidationProblems()
        {
            IReadOnlyList<string> problems = _catalog.Validate();

            Assert.That(problems, Is.Empty, problems.Count == 0 ? string.Empty : string.Join("\n", problems));
        }

        [Test]
        public void Catalog_EveryPartType_ResolvesToAnAsset()
        {
            foreach (PartType type in Enum.GetValues(typeof(PartType)))
            {
                Assert.That(
                    _catalog.TryGetDefinition(type, out PartDefinitionAsset definition),
                    Is.True,
                    $"'{type}' has no catalog entry, so a blueprint using it could not be built.");
                Assert.That(definition.PartType, Is.EqualTo(type));
            }
        }

        [Test]
        public void Catalog_EveryEntry_ConvertsToAValidDomainDefinition()
        {
            foreach (PartDefinitionAsset asset in _catalog.Definitions)
            {
                PartDefinition definition = asset.ToDomainDefinition();

                Assert.That(definition, Is.Not.Null, $"{asset.name} is not filled in well enough to use.");
                Assert.That(definition.DisplayName, Is.Not.Empty);
                Assert.That(definition.Mass, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void Catalog_EveryPart_OffersAtLeastOneAttachmentHole()
        {
            // A part with no holes can never be connected to anything, so it could be placed and
            // then silently do nothing.
            foreach (PartDefinitionAsset asset in _catalog.Definitions)
            {
                PartDefinition definition = asset.ToDomainDefinition();

                Assert.That(
                    definition.AttachmentHoles.Count,
                    Is.GreaterThan(0),
                    $"{asset.name} offers no attachment holes.");
            }
        }

        [Test]
        public void Catalog_EveryHole_HasAUniqueIdWithinItsPart()
        {
            foreach (PartDefinitionAsset asset in _catalog.Definitions)
            {
                PartDefinition definition = asset.ToDomainDefinition();
                var seen = new HashSet<string>();

                foreach (AttachmentHole hole in definition.AttachmentHoles)
                {
                    Assert.That(
                        seen.Add(hole.Id.Value),
                        Is.True,
                        $"{asset.name} defines hole '{hole.Id}' twice, so attaching to it is ambiguous.");
                }
            }
        }

        [Test]
        public void Catalog_MultiHoleParts_PlaceTheirHolesApart()
        {
            // Holes stacked on top of each other would make a hinge pivot about the same point as
            // its own mount, which silently behaves like a weld.
            foreach (PartDefinitionAsset asset in _catalog.Definitions)
            {
                PartDefinition definition = asset.ToDomainDefinition();
                IReadOnlyList<AttachmentHole> holes = definition.AttachmentHoles;
                if (holes.Count < 2)
                {
                    continue;
                }

                for (int i = 0; i < holes.Count; i++)
                {
                    for (int j = i + 1; j < holes.Count; j++)
                    {
                        Assert.That(
                            holes[i].LocalPosition,
                            Is.Not.EqualTo(holes[j].LocalPosition),
                            $"{asset.name} puts '{holes[i].Id}' and '{holes[j].Id}' in the same place.");
                    }
                }
            }
        }

        [Test]
        public void Catalog_PoweredWheel_IsTheOnlyPartWithAMotor()
        {
            foreach (PartDefinitionAsset asset in _catalog.Definitions)
            {
                bool hasMotor = asset.MotorSpeedDegreesPerSecond != 0f || asset.MaxMotorTorque != 0f;

                Assert.That(
                    hasMotor,
                    Is.EqualTo(asset.PartType == PartType.PoweredWheel),
                    $"{asset.name} has unexpected motor tuning for its part type.");
            }
        }

        [Test]
        public void Catalog_Wheels_HaveGripAndARadius()
        {
            // Both learned the hard way in Milestone 1: a wheel with no friction material spins
            // in place, and obstacle heights are judged against the wheel radius.
            foreach (PartDefinitionAsset asset in _catalog.Definitions)
            {
                if (asset.PartType != PartType.Wheel && asset.PartType != PartType.PoweredWheel)
                {
                    continue;
                }

                Assert.That(asset.Friction, Is.GreaterThan(0f), $"{asset.name} would spin in place.");
                Assert.That(asset.Radius, Is.GreaterThan(0f), $"{asset.name} has no radius.");
            }
        }
    }
}
