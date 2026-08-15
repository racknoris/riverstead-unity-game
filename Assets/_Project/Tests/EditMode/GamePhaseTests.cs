using System.Reflection;
using Contraption.Domain.Flow;
using NUnit.Framework;

namespace Contraption.Tests.EditMode
{
    /// <summary>
    /// Milestone 0 smoke test. Its real job is to prove the edit-mode suite runs from
    /// the command line and that the Domain assembly is reachable from tests.
    /// </summary>
    public class GamePhaseTests
    {
        [Test]
        public void GamePhase_Default_IsEditing()
        {
            Assert.That(default(GamePhase), Is.EqualTo(GamePhase.Editing));
        }

        [Test]
        public void DomainAssembly_Always_DoesNotReferenceUnityEngine()
        {
            AssemblyName[] references = typeof(GamePhase).Assembly.GetReferencedAssemblies();

            foreach (AssemblyName reference in references)
            {
                Assert.That(
                    reference.Name,
                    Does.Not.StartWith("UnityEngine").And.Not.StartWith("UnityEditor"),
                    $"Contraption.Domain must never reference Unity. Found: {reference.Name}");
            }
        }
    }
}
