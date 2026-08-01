package telemetry

import "testing"

func TestSummarizeAccountsTaskEconomics(t *testing.T) {
	a := validEvent()
	a.ElapsedMS = 10
	a.ModelCalls, a.HostCalls, a.ProcessLaunches, a.UnityHTTPRequests = 1, 2, 1, 2
	a.RawTokens, a.CachedTokens, a.BilledTokens, a.ToolResultTokens = 100, 20, 80, 30
	b := validEvent()
	b.TaskID, b.OperationID, b.ElapsedMS = "task_2", "operation_2", 30
	b.FirstAttemptSuccess, b.FinalTaskSuccess = false, true
	b.WrongToolAction, b.InvalidArgument, b.RepairCalls = 1, 1, 2
	b.DuplicateSideEffects, b.UnsafeMutations = 1, 1
	b.ReloadRecoveries, b.HumanInterventions = 1, 1

	summary, err := Summarize([]Event{a, b})
	if err != nil {
		t.Fatal(err)
	}
	if summary.Tasks != 2 || summary.FirstAttemptSuccess != 1 || summary.FinalTaskSuccess != 2 {
		t.Fatalf("success summary = %#v", summary)
	}
	if summary.P50ElapsedMS != 10 || summary.P95ElapsedMS != 30 {
		t.Fatalf("percentiles = %v/%v", summary.P50ElapsedMS, summary.P95ElapsedMS)
	}
	if summary.WrongToolAction != 1 || summary.InvalidArgument != 1 || summary.RepairCalls != 2 {
		t.Fatalf("repair summary = %#v", summary)
	}
	if summary.DuplicateSideEffects != 1 || summary.UnsafeMutations != 1 || summary.ReloadRecoveries != 1 || summary.HumanInterventions != 1 {
		t.Fatalf("safety summary = %#v", summary)
	}
}
