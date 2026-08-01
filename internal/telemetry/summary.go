package telemetry

import "sort"

type Summary struct {
	Tasks                int64 `json:"tasks"`
	FirstAttemptSuccess  int64 `json:"first_attempt_success"`
	FinalTaskSuccess     int64 `json:"final_task_success"`
	WrongToolAction      int64 `json:"wrong_tool_action"`
	InvalidArgument      int64 `json:"invalid_argument"`
	ModelCalls           int64 `json:"model_calls"`
	HostCalls            int64 `json:"host_calls"`
	ProcessLaunches      int64 `json:"process_launches"`
	UnityHTTPRequests    int64 `json:"unity_http_requests"`
	RepairCalls          int64 `json:"repair_calls"`
	RawTokens            int64 `json:"raw_tokens"`
	CachedTokens         int64 `json:"cached_tokens"`
	BilledTokens         int64 `json:"billed_tokens"`
	ToolResultTokens     int64 `json:"tool_result_tokens"`
	ElapsedMS            int64 `json:"elapsed_ms"`
	P50ElapsedMS         int64 `json:"p50_elapsed_ms"`
	P95ElapsedMS         int64 `json:"p95_elapsed_ms"`
	DuplicateSideEffects int64 `json:"duplicate_side_effects"`
	UnsafeMutations      int64 `json:"unsafe_mutations"`
	ReloadRecoveries     int64 `json:"reload_recoveries"`
	HumanInterventions   int64 `json:"human_interventions"`
}

func Summarize(events []Event) (Summary, error) {
	var result Summary
	durations := make([]int64, 0, len(events))
	for _, event := range events {
		if err := event.Validate(); err != nil {
			return Summary{}, err
		}
		result.Tasks++
		if event.FirstAttemptSuccess {
			result.FirstAttemptSuccess++
		}
		if event.FinalTaskSuccess {
			result.FinalTaskSuccess++
		}
		result.add(event)
		durations = append(durations, event.ElapsedMS)
	}
	if len(durations) == 0 {
		return result, nil
	}
	sort.Slice(durations, func(i, j int) bool { return durations[i] < durations[j] })
	result.P50ElapsedMS = percentile(durations, 50)
	result.P95ElapsedMS = percentile(durations, 95)
	return result, nil
}

func (summary *Summary) add(event Event) {
	summary.WrongToolAction += event.WrongToolAction
	summary.InvalidArgument += event.InvalidArgument
	summary.ModelCalls += event.ModelCalls
	summary.HostCalls += event.HostCalls
	summary.ProcessLaunches += event.ProcessLaunches
	summary.UnityHTTPRequests += event.UnityHTTPRequests
	summary.RepairCalls += event.RepairCalls
	summary.RawTokens += event.RawTokens
	summary.CachedTokens += event.CachedTokens
	summary.BilledTokens += event.BilledTokens
	summary.ToolResultTokens += event.ToolResultTokens
	summary.ElapsedMS += event.ElapsedMS
	summary.DuplicateSideEffects += event.DuplicateSideEffects
	summary.UnsafeMutations += event.UnsafeMutations
	summary.ReloadRecoveries += event.ReloadRecoveries
	summary.HumanInterventions += event.HumanInterventions
}

func percentile(sorted []int64, percentage int) int64 {
	index := (len(sorted)*percentage + 99) / 100
	return sorted[index-1]
}
