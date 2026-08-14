using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ApprovalPolicyTests
    {
        static int s_BatchMutations;

        [MenuItem("HeraAgent/Tests/ApprovalPolicy")]
        public static void RunTests()
        {
            var passed = true;
            passed &= Run(nameof(TestDestructiveOperationCannotRunWithoutApproval), TestDestructiveOperationCannotRunWithoutApproval);
            passed &= Run(nameof(TestApprovalBindsArgumentsHash), TestApprovalBindsArgumentsHash);
            passed &= Run(nameof(TestExpiredApprovalRejected), TestExpiredApprovalRejected);
            passed &= Run(nameof(TestApprovalSingleUse), TestApprovalSingleUse);
            passed &= Run(nameof(TestBatchCannotBypassApproval), TestBatchCannotBypassApproval);
            passed &= Run(nameof(TestPreflightUsesAuthoritativeContract), TestPreflightUsesAuthoritativeContract);
            passed &= Run(nameof(TestApprovedRetryReplaysWithoutReusingToken), TestApprovedRetryReplaysWithoutReusingToken);
            passed &= Run(nameof(TestApprovedReceivedRetryDoesNotConsumeTokenAgain), TestApprovedReceivedRetryDoesNotConsumeTokenAgain);
            if (passed)
                Debug.Log("[ApprovalPolicyTests] ALL PASSED");
            else
                Debug.LogError("[ApprovalPolicyTests] SOME TESTS FAILED");
        }

        static bool TestDestructiveOperationCannotRunWithoutApproval()
        {
            using var fixture = new ApprovalFixture();
            var context = fixture.Context("op_no_approval_1", new JObject { ["target"] = "fixture" });

            var decision = fixture.Ledger.Begin(context, "fixture", "delete", fixture.Destructive);

            return !decision.Execute
                && decision.Response is ErrorResponse error
                && error.code == "APPROVAL_REQUIRED"
                && !Directory.Exists(fixture.LedgerRoot);
        }

        static bool TestApprovalBindsArgumentsHash()
        {
            using var fixture = new ApprovalFixture();
            var original = fixture.Context("op_binding_0001", new JObject { ["target"] = "first" });
            var changed = fixture.Context("op_binding_0001", new JObject { ["target"] = "second" });
            var grant = fixture.Authority.Issue(fixture.Binding(original));
            changed = fixture.Context("op_binding_0001", new JObject { ["target"] = "second" }, grant.Token);

            var decision = fixture.Ledger.Begin(changed, "fixture", "delete", fixture.Destructive);

            return !decision.Execute
                && decision.Response is ErrorResponse error
                && error.code == "APPROVAL_MISMATCH";
        }

        static bool TestExpiredApprovalRejected()
        {
            using var fixture = new ApprovalFixture();
            var unsigned = fixture.Context("op_expired_0001", new JObject());
            var grant = fixture.Authority.Issue(fixture.Binding(unsigned));
            fixture.Now += (long)TimeSpan.FromMinutes(6).TotalMilliseconds;
            var context = fixture.Context("op_expired_0001", new JObject(), grant.Token);

            var decision = fixture.Ledger.Begin(context, "fixture", "delete", fixture.Destructive);

            return !decision.Execute
                && decision.Response is ErrorResponse error
                && error.code == "APPROVAL_EXPIRED";
        }

        static bool TestApprovalSingleUse()
        {
            using var fixture = new ApprovalFixture();
            var unsigned = fixture.Context("op_single_use_1", new JObject());
            var binding = fixture.Binding(unsigned);
            var grant = fixture.Authority.Issue(binding);

            var first = fixture.Authority.VerifyAndConsume(grant.Token, binding);
            var second = fixture.Authority.VerifyAndConsume(grant.Token, binding);

            return first == null && second?.code == "APPROVAL_ALREADY_USED";
        }

        static bool TestBatchCannotBypassApproval()
        {
            s_BatchMutations = 0;
            var result = CommandRouter.DispatchBatch(
                new System.Collections.Generic.List<CommandRouter.BatchCommandItem>
                {
                    new CommandRouter.BatchCommandItem
                    {
                        Command = "exec",
                        Params = new JObject
                        {
                            ["code"] = "HeraAgent.Tests.ApprovalPolicyTests.MarkBatchMutation(); return null;",
                        },
                    },
                },
                new CommandRouter.BatchOptions { Atomic = true }).GetAwaiter().GetResult();

            return s_BatchMutations == 0
                && result is ErrorResponse error
                && error.code == "APPROVAL_REQUIRED";
        }

        public static void MarkBatchMutation() => s_BatchMutations++;

        static bool TestPreflightUsesAuthoritativeContract()
        {
            var response = ApprovalPolicy.Preflight(new JObject
            {
                ["operation_id"] = "op_preflight_0001",
                ["tool"] = "exec",
                ["arguments"] = new JObject { ["code"] = "return null;" },
                ["target"] = "misleading target",
                ["side_effect"] = "none",
                ["reversible"] = true,
                ["external_impact"] = true,
            }) as SuccessResponse;
            var preflight = response?.data as ApprovalPreflight;

            return preflight?.Summary != null
                && preflight.Summary.Tool == "exec"
                && preflight.Summary.Target == "parameters: code"
                && preflight.Summary.SideEffect == "unity_editor_and_project"
                && !preflight.Summary.Reversible
                && !preflight.Summary.ExternalImpact;
        }

        static bool TestApprovedRetryReplaysWithoutReusingToken()
        {
            using var fixture = new ApprovalFixture();
            var unsigned = fixture.Context("op_retry_approval", new JObject());
            var grant = fixture.Authority.Issue(fixture.Binding(unsigned));
            var approved = fixture.Context("op_retry_approval", new JObject(), grant.Token);

            var first = fixture.Ledger.Begin(approved, "fixture", "delete", fixture.Destructive);
            if (!first.Execute)
                return false;
            fixture.Ledger.Commit(approved, new SuccessResponse("done"));
            var replay = fixture.Ledger.Begin(approved, "fixture", "delete", fixture.Destructive);

            return !replay.Execute && replay.Response is JObject;
        }

        static bool TestApprovedReceivedRetryDoesNotConsumeTokenAgain()
        {
            using var fixture = new ApprovalFixture();
            var unsigned = fixture.Context("op_received_approved", new JObject());
            var grant = fixture.Authority.Issue(fixture.Binding(unsigned));
            var approved = fixture.Context("op_received_approved", new JObject(), grant.Token);
            if (!fixture.Ledger.Begin(approved, "fixture", "delete", fixture.Destructive).Execute)
                return false;

            var path = Path.Combine(fixture.LedgerRoot, "op_received_approved.json");
            var record = JObject.Parse(File.ReadAllText(path));
            record["state"] = "received";
            File.WriteAllText(path, record.ToString(Newtonsoft.Json.Formatting.None));

            return fixture.Ledger.Begin(approved, "fixture", "delete", fixture.Destructive).Execute;
        }

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

        sealed class ApprovalFixture : IDisposable
        {
            internal string LedgerRoot { get; }
            internal long Now { get; set; } = 2_000_000_000_000;
            internal ApprovalAuthority Authority { get; }
            internal OperationLedger Ledger { get; }
            internal ToolSafetyContract Destructive { get; } = new ToolSafetyContract
            {
                RiskClass = HeraRiskClass.Destructive,
                Destructive = true,
                RequiresConfirmation = true,
                SideEffectScope = "fixture",
            };

            internal ApprovalFixture()
            {
                LedgerRoot = Path.Combine(Path.GetTempPath(), "hera-approval-ledger-" + Guid.NewGuid().ToString("N"));
                Authority = new ApprovalAuthority(new byte[32], () => Now);
                Ledger = new OperationLedger(LedgerRoot, "approval-domain", Authority);
            }

            internal ApprovalBinding Binding(CommandRequestContext context) => new ApprovalBinding
            {
                OperationId = context.OperationId,
                Tool = "fixture",
                Action = "delete",
                ArgumentsHash = context.ArgumentsHash,
                RiskClass = "destructive",
                ProjectId = ToolCatalogRuntime.ProjectId,
            };

            internal CommandRequestContext Context(string operationId, JObject arguments, string token = null)
            {
                var metadata = new JObject { ["operation_id"] = operationId };
                if (token != null)
                    metadata["approval_token"] = token;
                if (!CommandRequestContext.TryCreate(metadata, arguments, out var context, out var error))
                    throw new InvalidOperationException(error.message);
                return context;
            }

            public void Dispose()
            {
                try { Directory.Delete(LedgerRoot, true); } catch { }
            }
        }
    }
}
