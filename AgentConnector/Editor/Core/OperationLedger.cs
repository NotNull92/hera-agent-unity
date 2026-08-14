using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal sealed class OperationLedger
    {
        private const long DefaultMaxBytes = 64L * 1024 * 1024;
        private static readonly TimeSpan RunningExecutionCeiling = TimeSpan.FromHours(1);

        internal static readonly OperationLedger Default = new OperationLedger(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".hera-agent-unity",
                "status",
                "operations",
                SafeProjectDirectory(ToolCatalogRuntime.ProjectId)),
            ToolCatalogRuntime.DomainEpoch,
            ApprovalPolicy.Authority);

        readonly string _root;
        readonly string _domainEpoch;
        readonly long _maxBytes;
        readonly ApprovalAuthority _approvals;

        internal OperationLedger(string root, string domainEpoch)
            : this(root, domainEpoch, ApprovalPolicy.Authority, DefaultMaxBytes)
        {
        }

        internal OperationLedger(string root, string domainEpoch, long maxBytes)
            : this(root, domainEpoch, ApprovalPolicy.Authority, maxBytes)
        {
        }

        internal OperationLedger(
            string root,
            string domainEpoch,
            ApprovalAuthority approvals,
            long maxBytes = DefaultMaxBytes)
        {
            _root = root;
            _domainEpoch = domainEpoch;
            _maxBytes = maxBytes > 0
                ? maxBytes
                : throw new ArgumentOutOfRangeException(nameof(maxBytes));
            _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        }

        static string SafeProjectDirectory(string projectId) =>
            projectId.Replace(':', '_');

        internal OperationLedgerDecision Begin(
            CommandRequestContext context,
            string tool,
            string action,
            ToolSafetyContract safety)
        {
            var riskClass = ToolCatalogBuilder.RiskName(safety.RiskClass);
            var existing = Read(context.OperationId);
            if (existing != null)
            {
                if (!string.Equals(existing.Tool, tool, StringComparison.Ordinal)
                    || !string.Equals(existing.Action, action, StringComparison.Ordinal)
                    || !string.Equals(
                    existing.ArgumentsHash,
                    context.ArgumentsHash,
                    StringComparison.Ordinal)
                    || !string.Equals(existing.RiskClass, riskClass, StringComparison.Ordinal)
                    || existing.Idempotent != safety.Idempotent)
                {
                    return OperationLedgerDecision.Replay(new ErrorResponse(
                        "OPERATION_CONFLICT",
                        $"Operation '{context.OperationId}' was already used for a different request."));
                }
                if (existing.State == "committed"
                    || existing.State == "responded"
                    || existing.State == "failed")
                {
                    return OperationLedgerDecision.Replay((object)existing.Response?.DeepClone()
                        ?? new ErrorResponse(
                            "OPERATION_RESPONSE_MISSING",
                            $"Operation '{context.OperationId}' has no stored response."));
                }
                if (existing.State == "running"
                    && !string.Equals(existing.DomainEpoch, _domainEpoch, StringComparison.Ordinal))
                {
                    existing.State = "outcome_unknown";
                    Write(existing);
                    return Unknown(context.OperationId);
                }
                if (existing.State == "running")
                {
                    return OperationLedgerDecision.Replay(new ErrorResponse(
                        "OPERATION_IN_PROGRESS",
                        $"Operation '{context.OperationId}' is still running."));
                }
                if (existing.State == "outcome_unknown" && !existing.Idempotent)
                    return Unknown(context.OperationId);
            }

            var approvalVerified = existing != null
                && existing.State == "received"
                && existing.ApprovalVerified;
            if (safety.RequiresConfirmation && !approvalVerified)
            {
                if (string.IsNullOrWhiteSpace(context.ApprovalToken))
                {
                    return OperationLedgerDecision.Replay(new ErrorResponse(
                        "APPROVAL_REQUIRED",
                        "This operation requires an approval token."));
                }
                var approvalError = _approvals.VerifyAndConsume(
                    context.ApprovalToken,
                    ApprovalPolicy.Binding(context, tool, action, safety));
                if (approvalError != null)
                    return OperationLedgerDecision.Replay(approvalError);
                approvalVerified = true;
            }

            var record = existing ?? new OperationLedgerRecord
            {
                OperationId = context.OperationId,
                Tool = tool,
                Action = action,
                ArgumentsHash = context.ArgumentsHash,
                RiskClass = riskClass,
                Idempotent = safety.Idempotent,
                StartedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            record.ApprovalVerified = approvalVerified;
            record.State = "received";
            record.DomainEpoch = _domainEpoch;
            Write(record);
            record.State = "running";
            Write(record);
            return OperationLedgerDecision.Proceed();
        }

        internal object Commit(CommandRequestContext context, object response)
        {
            var record = ReadRequired(context.OperationId);
            var token = response as JToken ?? JToken.FromObject(response);
            record.Response = token;
            record.ResponseHash = ToolContractCanonicalJson.ComputeTokenHash(token);
            record.CommittedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            record.State = response is ErrorResponse ? "failed" : "committed";
            Write(record);
            return response;
        }

        internal void MarkResponded(string operationId)
        {
            var record = Read(operationId);
            if (record == null || record.State != "committed")
                return;
            record.State = "responded";
            Write(record);
        }

        internal void Cleanup(DateTimeOffset now)
        {
            if (!Directory.Exists(_root))
                return;

            foreach (var path in Directory.GetFiles(_root, "*.json"))
            {
                OperationLedgerRecord record;
                try { record = JsonConvert.DeserializeObject<OperationLedgerRecord>(File.ReadAllText(path)); }
                catch { continue; }
                if (record == null)
                    continue;

                if (record.State == "running")
                {
                    var started = SafeTimestamp(record.StartedAtMs, now);
                    var priorDomain = !string.Equals(
                        record.DomainEpoch,
                        _domainEpoch,
                        StringComparison.Ordinal);
                    if (!priorDomain && now - started <= RunningExecutionCeiling)
                        continue;

                    record.State = "outcome_unknown";
                    Write(record);
                }

                var retainedFor = record.State == "outcome_unknown"
                    ? TimeSpan.FromDays(7)
                    : TimeSpan.FromHours(24);
                var reference = record.CommittedAtMs ?? record.StartedAtMs;
                if (now - SafeTimestamp(reference, now) > retainedFor)
                {
                    try { File.Delete(path); } catch { }
                }
            }
            CompactToLimit(now);
        }

        void CompactToLimit(DateTimeOffset now)
        {
            var files = Directory.GetFiles(_root, "*.json")
                .Select(path => new FileInfo(path))
                .ToArray();
            var total = files.Sum(file => file.Length);
            if (total <= _maxBytes)
                return;

            var candidates = new List<OperationLedgerRecord>();
            foreach (var file in files)
            {
                try
                {
                    var record = JsonConvert.DeserializeObject<OperationLedgerRecord>(
                        File.ReadAllText(file.FullName));
                    if (record?.Response != null
                        && (record.State == "committed"
                            || record.State == "responded"
                            || record.State == "failed"))
                    {
                        candidates.Add(record);
                    }
                }
                catch { }
            }
            foreach (var record in candidates.OrderBy(item => item.StartedAtMs))
            {
                if (total <= _maxBytes)
                    break;
                var path = PathFor(record.OperationId);
                var before = new FileInfo(path).Length;
                record.Response = null;
                Write(record);
                total -= before - new FileInfo(path).Length;
            }

            if (total <= _maxBytes)
                return;

            var removable = new List<(FileInfo File, long StartedAtMs)>();
            foreach (var file in Directory.GetFiles(_root, "*.json")
                .Select(path => new FileInfo(path)))
            {
                try
                {
                    var record = JsonConvert.DeserializeObject<OperationLedgerRecord>(
                        File.ReadAllText(file.FullName));
                    if (record != null
                        && record.State == "running"
                        && string.Equals(record.DomainEpoch, _domainEpoch, StringComparison.Ordinal)
                        && now - SafeTimestamp(record.StartedAtMs, now) <= RunningExecutionCeiling)
                    {
                        continue;
                    }
                    removable.Add((file, record?.StartedAtMs ?? new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeMilliseconds()));
                }
                catch
                {
                    removable.Add((file, new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeMilliseconds()));
                }
            }

            foreach (var candidate in removable.OrderBy(item => item.StartedAtMs))
            {
                if (total <= _maxBytes)
                    break;
                var length = candidate.File.Exists ? candidate.File.Length : 0;
                try { candidate.File.Delete(); }
                catch { continue; }
                total -= length;
            }
        }

        static DateTimeOffset SafeTimestamp(long value, DateTimeOffset fallback)
        {
            try { return DateTimeOffset.FromUnixTimeMilliseconds(value); }
            catch (ArgumentOutOfRangeException) { return fallback; }
        }

        OperationLedgerDecision Unknown(string operationId) =>
            OperationLedgerDecision.Replay(new ErrorResponse(
                "OPERATION_OUTCOME_UNKNOWN",
                $"Operation '{operationId}' may have executed before the Unity domain changed; it was not re-run."));

        OperationLedgerRecord ReadRequired(string operationId) =>
            Read(operationId) ?? throw new InvalidOperationException(
                $"Operation ledger record '{operationId}' is missing.");

        OperationLedgerRecord Read(string operationId)
        {
            var path = PathFor(operationId);
            if (!File.Exists(path))
                return null;
            return JsonConvert.DeserializeObject<OperationLedgerRecord>(File.ReadAllText(path));
        }

        void Write(OperationLedgerRecord record)
        {
            AtomicFile.WriteAllText(
                PathFor(record.OperationId),
                JsonConvert.SerializeObject(record, Formatting.None));
        }

        string PathFor(string operationId) =>
            Path.Combine(_root, operationId + ".json");
    }
}
