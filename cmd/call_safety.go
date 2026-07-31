package cmd

import (
	"bytes"
	"encoding/json"
	"fmt"
	"slices"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

type safetyCondition struct {
	Const any `json:"const"`
}

func resolveCallSafety(
	tool toolregistry.Tool,
	params map[string]any,
) (string, toolregistry.Safety, error) {
	actionName, _ := params["action"].(string)
	fallback := tool.Safety
	for _, action := range tool.Actions {
		if action.Name == actionName || slices.Contains(action.Aliases, actionName) {
			actionName = action.Name
			fallback = action.Safety
			break
		}
	}

	bestSpecificity := -1
	var matched *toolregistry.SafetyRule
	for index := range tool.Safety.Rules {
		rule := &tool.Safety.Rules[index]
		matches, specificity, err := safetyRuleMatches(*rule, params)
		if err != nil {
			return "", toolregistry.Safety{}, err
		}
		if !matches || specificity < bestSpecificity {
			continue
		}
		if specificity == bestSpecificity {
			return "", toolregistry.Safety{}, fmt.Errorf("ambiguous safety rules matched")
		}
		bestSpecificity = specificity
		matched = rule
	}
	if matched != nil {
		return actionName, matched.Safety, nil
	}
	return actionName, fallback, nil
}

func safetyRuleMatches(
	rule toolregistry.SafetyRule,
	params map[string]any,
) (bool, int, error) {
	decoder := json.NewDecoder(bytes.NewReader(rule.When))
	decoder.UseNumber()
	var conditions map[string]safetyCondition
	if err := decoder.Decode(&conditions); err != nil {
		return false, 0, fmt.Errorf("decode safety rule %q: %w", rule.Operation, err)
	}
	for name, condition := range conditions {
		actual, ok := params[name]
		if !ok || !sameJSONValue(actual, condition.Const) {
			return false, len(conditions), nil
		}
	}
	return true, len(conditions), nil
}

func sameJSONValue(left any, right any) bool {
	leftJSON, leftErr := json.Marshal(left)
	rightJSON, rightErr := json.Marshal(right)
	return leftErr == nil && rightErr == nil && bytes.Equal(leftJSON, rightJSON)
}
