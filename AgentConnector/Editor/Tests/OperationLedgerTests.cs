using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class OperationLedgerTests
    {
        [MenuItem("HeraAgent/Tests/OperationLedger")]
        public static void RunTests()
        {
            var passed = true;
            passed &= Run(nameof(TestOperationReplayReturnsStoredResponse), TestOperationReplayReturnsStoredResponse);
            passed &= Run(nameof(TestOperationConflictRejectsDifferentArguments), TestOperationConflictRejectsDifferentArguments);
            passed &= Run(nameof(TestCommittedResponseSurvivesResponseLoss), TestCommittedResponseSurvivesResponseLoss);
            passed &= Run(nameof(TestPriorDomainRunningBecomesUnknown), TestPriorDomainRunningBecomesUnknown);
            passed &= Run(nameof(TestNonIdempotentUnknownDoesNotInvokeHandler), TestNonIdempotentUnknownDoesNotInvokeHandler);
            passed &= Run(nameof(TestLedgerAtomicWriteFallback), TestLedgerAtomicWriteFallback);
            passed &= Run(nameof(TestLedgerRetentionCleanup), TestLedgerRetentionCleanup);
            if (passed)
                Debug.Log("[OperationLedgerTests] ALL PASSED");
            else
                Debug.LogError("[OperationLedgerTests] SOME TESTS FAILED");
        }

        static bool TestOperationReplayReturnsStoredResponse()
        {
            using var fixture = new LedgerFixture();
            var context = fixture.Context("op_replay_0001", new JObject { ["value"] = 1 });
            var first = fixture.Ledger.Begin(context, "fixture", "mutate", fixture.Mutation);
            if (!first.Execute)
                return false;
            fixture.Ledger.Commit(context, new SuccessResponse("stored", new { count = 1 }));

            var replay = fixture.Ledger.Begin(context, "fixture", "mutate", fixture.Mutation);
            return !replay.Execute
                && replay.Response is JObject response
                && response["success"]?.Value<bool>() == true
                && response["message"]?.ToString() == "stored";
        }

        static bool TestOperationConflictRejectsDifferentArguments()
        {
            using var fixture = new LedgerFixture();
            var original = fixture.Context("op_conflict_01", new JObject { ["value"] = 1 });
            fixture.Ledger.Begin(original, "fixture", "mutate", fixture.Mutation);
            fixture.Ledger.Commit(original, new SuccessResponse("stored"));
            var changed = fixture.Context("op_conflict_01", new JObject { ["value"] = 2 });

            var conflict = fixture.Ledger.Begin(changed, "fixture", "mutate", fixture.Mutation);
            return !conflict.Execute
                && conflict.Response is ErrorResponse error
                && error.code == "OPERATION_CONFLICT";
        }

        static bool TestCommittedResponseSurvivesResponseLoss()
        {
            using var fixture = new LedgerFixture();
            var context = fixture.Context("op_response_loss", new JObject());
            fixture.Ledger.Begin(context, "fixture", "mutate", fixture.Mutation);
            fixture.Ledger.Commit(context, new SuccessResponse("committed-before-write"));

            var afterDisconnect = new OperationLedger(fixture.Root, "domain-b");
            var replay = afterDisconnect.Begin(context, "fixture", "mutate", fixture.Mutation);
            return !replay.Execute
                && replay.Response is JObject response
                && response["message"]?.ToString() == "committed-before-write";
        }

        static bool TestPriorDomainRunningBecomesUnknown()
        {
            using var fixture = new LedgerFixture();
            var context = fixture.Context("op_prior_domain", new JObject());
            fixture.Ledger.Begin(context, "fixture", "mutate", fixture.Mutation);

            var reloaded = new OperationLedger(fixture.Root, "domain-b");
            var decision = reloaded.Begin(context, "fixture", "mutate", fixture.Mutation);
            return IsUnknown(decision);
        }

        static bool TestNonIdempotentUnknownDoesNotInvokeHandler()
        {
            using var fixture = new LedgerFixture();
            var context = fixture.Context("op_no_reinvoke_1", new JObject());
            fixture.Ledger.Begin(context, "fixture", "mutate", fixture.Mutation);
            var reloaded = new OperationLedger(fixture.Root, "domain-b");
            var handlerInvocations = 0;

            var decision = reloaded.Begin(context, "fixture", "mutate", fixture.Mutation);
            if (decision.Execute)
                handlerInvocations++;
            return handlerInvocations == 0 && IsUnknown(decision);
        }

        static bool TestLedgerAtomicWriteFallback()
        {
            var root = Path.Combine(Path.GetTempPath(), "hera-ledger-atomic-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(root, "record.json");
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(path, "old");
                AtomicFile.WriteAllTextCore(
                    path,
                    "new",
                    (_, _) => throw new IOException("replace unavailable"));
                return File.ReadAllText(path) == "new"
                    && Directory.GetFiles(root, "*.tmp").Length == 0;
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        static bool TestLedgerRetentionCleanup()
        {
            using var fixture = new LedgerFixture();
            const string operationId = "op_retention_01";
            var context = fixture.Context(operationId, new JObject());
            fixture.Ledger.Begin(context, "fixture", "mutate", fixture.Mutation);
            fixture.Ledger.Commit(context, new SuccessResponse("expired"));
            var path = Path.Combine(fixture.Root, operationId + ".json");
            var record = JObject.Parse(File.ReadAllText(path));
            var expired = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(2))
                .ToUnixTimeMilliseconds();
            record["started_at_ms"] = expired;
            record["committed_at_ms"] = expired;
            File.WriteAllText(path, record.ToString(Newtonsoft.Json.Formatting.None));

            fixture.Ledger.Cleanup(DateTimeOffset.UtcNow);
            return !File.Exists(path);
        }

        static bool IsUnknown(OperationLedgerDecision decision) =>
            !decision.Execute
            && decision.Response is ErrorResponse error
            && error.code == "OPERATION_OUTCOME_UNKNOWN";

        static bool Run(string name, Func<bool> test)
        {
            try
            {
                var passed = test();
                Debug.Log((passed ? "[PASS] " : "[FAIL] ") + name);
                return passed;
            }
            catch (Exception exception)
            {
                Debug.LogError("[FAIL] " + name + ": " + exception);
                return false;
            }
        }

        sealed class LedgerFixture : IDisposable
        {
            internal string Root { get; }
            internal OperationLedger Ledger { get; }
            internal ToolSafetyContract Mutation { get; } = new ToolSafetyContract
            {
                RiskClass = HeraRiskClass.Write,
                Idempotent = false,
                SideEffectScope = "test",
            };

            internal LedgerFixture()
            {
                Root = Path.Combine(Path.GetTempPath(), "hera-ledger-test-" + Guid.NewGuid().ToString("N"));
                Ledger = new OperationLedger(Root, "domain-a");
            }

            internal CommandRequestContext Context(string operationId, JObject arguments)
            {
                var metadata = new JObject { ["operation_id"] = operationId };
                if (!CommandRequestContext.TryCreate(
                    metadata,
                    arguments,
                    out var context,
                    out var error))
                {
                    throw new InvalidOperationException(error.message);
                }
                return context;
            }

            public void Dispose()
            {
                try { Directory.Delete(Root, true); } catch { }
            }
        }
    }
}
