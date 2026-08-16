using System;
using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Domain.Validation;
using NUnit.Framework;

namespace Contraption.Tests.EditMode
{
    /// <summary>
    /// Milestone 3: the catalog rule itself, tested without loading an asset. These are the tests
    /// that prove the validator can actually fail — <see cref="PartCatalogAssetTests"/> then only
    /// has to check that the real catalog satisfies it.
    /// </summary>
    public sealed class PartCatalogValidatorTests
    {
        [Test]
        public void Validate_CompleteCatalog_ReportsNoProblems()
        {
            Assert.That(PartCatalogValidator.Validate(EveryPartType()), Is.Empty);
        }

        [Test]
        public void Validate_MissingPartType_ReportsTheMissingType()
        {
            List<PartDefinition?> definitions = EveryPartType();
            definitions.RemoveAt(definitions.FindIndex(d => d!.Type == PartType.Spring));

            IReadOnlyList<string> problems = PartCatalogValidator.Validate(definitions);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("Spring"));
        }

        [Test]
        public void Validate_DuplicatePartType_ReportsTheDuplicate()
        {
            List<PartDefinition?> definitions = EveryPartType();
            definitions.Add(Definition(PartType.Beam));

            IReadOnlyList<string> problems = PartCatalogValidator.Validate(definitions);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("Beam"));
        }

        [Test]
        public void Validate_EmptyEntry_ReportsIt()
        {
            List<PartDefinition?> definitions = EveryPartType();
            definitions.Add(null);

            Assert.That(PartCatalogValidator.Validate(definitions), Is.Not.Empty);
        }

        [Test]
        public void Validate_NullCatalog_ReportsAProblemRatherThanThrowing()
        {
            Assert.That(PartCatalogValidator.Validate(null), Is.Not.Empty);
        }

        [Test]
        public void Validate_SeveralGaps_ReportsAllOfThem()
        {
            // Reporting only the first problem would mean fixing a broken catalog one
            // edit-compile cycle at a time.
            IReadOnlyList<string> problems = PartCatalogValidator.Validate(new List<PartDefinition?>());

            Assert.That(problems, Has.Count.EqualTo(Enum.GetValues(typeof(PartType)).Length));
        }

        private static List<PartDefinition?> EveryPartType()
        {
            var definitions = new List<PartDefinition?>();
            foreach (PartType type in Enum.GetValues(typeof(PartType)))
            {
                definitions.Add(Definition(type));
            }

            return definitions;
        }

        private static PartDefinition Definition(PartType type)
        {
            return new PartDefinition(type, type.ToString(), mass: 1f, cost: 1);
        }
    }
}
