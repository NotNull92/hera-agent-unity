using System;
using System.IO;
using System.Linq;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HeraAgent.Tests
{
    internal static class ExecJournalTests
    {
        internal static void RunTests()
        {
            var directory = Path.Combine(Path.GetTempPath(), "hera-journal-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.AreEqual("none", new ExecJournal.Observation().CompileCache);
                var observation = new ExecJournal.Observation { Mode = "restricted", CompileCache = "memory_hit" };
                observation.Timings["compile_ms"] = 12;
                observation.Timings["execute_ms"] = 3;
                var response = new SuccessResponse("private-response", "private-result");
                ExecJournal.Append("private-source", "op_journal_test", observation, response, directory);
                var path = Path.Combine(directory, "exec-journal.jsonl");
                var text = File.ReadAllText(path);
                var row = JObject.Parse(text);
                CollectionAssert.AreEquivalent(new[] { "schema_version", "ts_ms", "operation_id", "code_sha256",
                    "mode", "compile_cache", "compile_ms", "execute_ms", "outcome", "domain_epoch" },
                    row.Properties().Select(property => property.Name));
                Assert.AreEqual(1, row["schema_version"].Value<int>());
                Assert.Greater(row["ts_ms"].Value<long>(), 0);
                Assert.AreEqual("op_journal_test", row["operation_id"].Value<string>());
                Assert.AreEqual("831a93071a15afdd34c7ad86d5e00071639c24bffcddf1e1d6b53b8666417820", row["code_sha256"].Value<string>());
                Assert.AreEqual("restricted", row["mode"].Value<string>());
                Assert.AreEqual("memory_hit", row["compile_cache"].Value<string>());
                Assert.AreEqual(12, row["compile_ms"].Value<long>());
                Assert.AreEqual(3, row["execute_ms"].Value<long>());
                Assert.AreEqual(observation.DomainEpoch, row["domain_epoch"].Value<string>());
                Assert.AreEqual("ok", row["outcome"].Value<string>());
                Assert.IsFalse(text.Contains("private-"));
                foreach (var code in new[] { "EXEC_COMPILE_ERROR", "EXEC_RUNTIME_ERROR" })
                    ExecJournal.Append("secret-source", "op_error_test", observation, new ErrorResponse(code, "secret-message"), directory);
                var errors = File.ReadAllLines(path).Skip(1).Select(line => JObject.Parse(line)).ToArray();
                CollectionAssert.AreEqual(new[] { "EXEC_COMPILE_ERROR", "EXEC_RUNTIME_ERROR" }, errors.Select(error => error["outcome"].Value<string>()));
                Assert.IsFalse(File.ReadAllText(path).Contains("secret-"));

                File.WriteAllText(path, string.Concat(Enumerable.Repeat(text, ExecJournal.MaxBytes / text.Length + 2)));
                ExecJournal.Append("private-source", "op_last_test", observation, response, directory);
                Assert.LessOrEqual(new FileInfo(path).Length, ExecJournal.MaxBytes / 2);
                var retained = File.ReadAllLines(path).Select(line => JObject.Parse(line)).ToArray();
                Assert.Greater(retained.Length, 0);
                Assert.AreEqual("op_last_test", retained.Last()["operation_id"].Value<string>());
                Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));

                var blocker = Path.Combine(directory, "not-a-directory");
                File.WriteAllText(blocker, "keep");
                Assert.DoesNotThrow(() => ExecJournal.Append("private-source", "op_failure_test", observation, response, blocker));
                Assert.AreEqual("keep", File.ReadAllText(blocker));
                Assert.AreEqual("private-response", response.message);
                Assert.AreEqual("private-result", response.data);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
