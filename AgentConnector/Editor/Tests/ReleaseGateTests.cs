using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace HeraAgent.Tests
{
    /// <summary>
    /// NUnit entry points for the existing deterministic Connector suites.
    /// The suites stay isolated in the TestAssemblies asmdef, while Unity Test
    /// Runner can discover them in disposable package fixtures. This manifest
    /// is the canonical release-gate ownership list; menu runners are only
    /// maintainer conveniences.
    /// </summary>
    [TestFixture]
    public sealed class ReleaseGateTests
    {
        internal static readonly string[] CanonicalSuiteNames =
        {
            nameof(ApprovalPolicy),
            nameof(AssetConfigPersistence),
            nameof(AssetMutationPreflight),
            nameof(AtomicFile),
            nameof(InputQa),
            nameof(OperationLedger),
            nameof(OutputFilePolicy),
            nameof(ProjectIdentity),
            nameof(ScreenshotAnnotations),
            nameof(ScreenshotPhysics),
            nameof(ToolCatalog),
            nameof(ToolContract),
            nameof(ToolDiscovery),
            nameof(ToolProfiles),
            nameof(ToolSafety),
            nameof(UiDocApply),
        };

        [Test]
        public void ApprovalPolicy() => ApprovalPolicyTests.RunTests();

        [Test]
        public void AssetConfigPersistence() => AssetConfigPersistenceTests.RunTests();

        [Test]
        public void AssetMutationPreflight() => AssetMutationPreflightTests.RunTests();

        [Test]
        public void AtomicFile() => AtomicFileTests.RunTests();

        [Test]
        public void InputQa() => Assert.IsTrue(InputQaTests.RunContractTests());

        [Test]
        public void OperationLedger() => OperationLedgerTests.RunTests();

        [Test]
        public void OutputFilePolicy() => OutputFilePolicyTests.RunTests();

        [Test]
        public void ProjectIdentity() => ProjectIdentityTests.RunTests();

        [Test]
        public void ScreenshotAnnotations() => ScreenshotAnnotationTests.RunTests();

        [Test]
        public void ScreenshotPhysics() => ScreenshotPhysicsTests.RunTests();

        [Test]
        public void ToolCatalog() => ToolCatalogTests.RunTests();

        [Test]
        public void ToolContract() => ToolContractTests.RunTests();

        [Test]
        public void ToolDiscovery() => ToolDiscoveryTests.RunTests();

        [Test]
        public void ToolProfiles() => ToolProfileTests.RunTests();

        [Test]
        public void ToolSafety() => ToolSafetyTests.RunTests();

        [Test]
        public void UiDocApply() => UiDocApplyTests.RunTests();

        [Test]
        public void CanonicalManifestMatchesNUnitWrappers()
        {
            var wrappers = GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.Name != nameof(CanonicalManifestMatchesNUnitWrappers))
                .Where(method => method.GetCustomAttributes(typeof(TestAttribute), false).Length != 0)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var expected = CanonicalSuiteNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(expected, wrappers,
                "Update CanonicalSuiteNames whenever the release-gate wrapper set changes.");
        }
    }
}
