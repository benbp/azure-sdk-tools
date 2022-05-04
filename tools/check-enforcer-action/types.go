package main

import (
	"encoding/json"
	"strings"
)

const (
	CommitStatePending CommitState = "pending"
	CommitStateSuccess CommitState = "success"
	CommitStateFailure             = "failure"
	CommitStateError               = "error"

	CheckSuiteActionCompleted   CheckSuiteAction = "completed"
	CheckSuiteActionRequested   CheckSuiteAction = "requested"
	CheckSuiteActionReRequested CheckSuiteAction = "rerequested"

	CheckSuiteStatusRequested  CheckSuiteStatus = "requested"
	CheckSuiteStatusInProgress CheckSuiteStatus = "in_progress"
	CheckSuiteStatusCompleted  CheckSuiteStatus = "completed"

	CheckSuiteConclusionSuccess        CheckSuiteConclusion = "success"
	CheckSuiteConclusionFailure        CheckSuiteConclusion = "failure"
	CheckSuiteConclusionNeutral        CheckSuiteConclusion = "neutral"
	CheckSuiteConclusionCancelled      CheckSuiteConclusion = "cancelled"
	CheckSuiteConclusionTimedOut       CheckSuiteConclusion = "timed_out"
	CheckSuiteConclusionActionRequired CheckSuiteConclusion = "action_required"
	CheckSuiteConclusionStale          CheckSuiteConclusion = "stale"
)

type CommitState string
type CheckSuiteAction string
type CheckSuiteStatus string
type CheckSuiteConclusion string

type StatusBody struct {
	State       CommitState `json:"state"`
	Description string      `json:"description"`
	Context     string      `json:"context"`
}

type Label struct {
	Name string
}

type Repo struct {
	Id          int    `json:"id"`
	Name        string `json:"name"`
	Url         string `json:"url"`
	HtmlUrl     string `json:"html_url"`
	StatusesUrl string `json:"statuses_url"`
}

type CheckSuite struct {
	Id           int                  `json:"id"`
	HeadBranch   string               `json:"head_branch"`
	HeadSha      string               `json:"head_sha"`
	Status       CheckSuiteStatus     `json:"status"`
	Conclusion   CheckSuiteConclusion `json:"conclusion"`
	Url          string               `json:"url"`
	CheckRunsUrl string               `json:"check_runs_url"`
}

type CheckSuiteWebhook struct {
	Action     CheckSuiteAction `json:"action"`
	CheckSuite CheckSuite       `json:"check_suite"`
	Repo       Repo             `json:"repository"`
}

type IssueCommentWebhook struct {
}

func (cs *CheckSuiteWebhook) IsSucceeded() bool {
	return cs.CheckSuite.Conclusion == CheckSuiteConclusionSuccess
}

func (cs *CheckSuiteWebhook) IsFailed() bool {
	return cs.CheckSuite.Conclusion == CheckSuiteConclusionFailure ||
		cs.CheckSuite.Conclusion == CheckSuiteConclusionTimedOut
}

func (cs *CheckSuiteWebhook) GetStatusesUrl() string {
	return strings.ReplaceAll(cs.Repo.StatusesUrl, "{sha}", cs.CheckSuite.HeadSha)
}

func NewCheckSuiteWebhook(payload []byte) *CheckSuiteWebhook {
	var cs CheckSuiteWebhook
	if err := json.Unmarshal(payload, &cs); err != nil {
		return nil
	}
	if cs.CheckSuite.Id == 0 {
		return nil
	}
	return &cs
}
