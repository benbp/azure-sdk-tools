package main

import (
	"errors"
	"fmt"
	"io/ioutil"
	"os"
)

const GithubTokenKey = "GITHUB_TOKEN"
const CommitStatusContext = "https://aka.ms/azsdk/checkenforcer"

var pendingBody = StatusBody{
	State:       CommitStatePending,
	Description: "Waiting for all checks to complete",
	Context:     CommitStatusContext,
}

var succeededBody = StatusBody{
	State:       CommitStateSuccess,
	Description: "All checks passed",
	Context:     CommitStatusContext,
}

var failedBody = StatusBody{
	State:       CommitStateFailure,
	Description: "Some checks failed",
	Context:     CommitStatusContext,
}

func main() {
	if len(os.Args) <= 1 {
		help()
		os.Exit(1)
	}

	payloadPath := os.Args[1]
	payload, err := ioutil.ReadFile(payloadPath)
	handleError(err)

	github_token := os.Getenv(GithubTokenKey)
	if github_token == "" {
		fmt.Println(fmt.Sprintf("WARNING: environment variable '%s' is not set", GithubTokenKey))
	}

	gh, err := NewGithubClient("https://api.github.com", github_token)
	handleError(err)

	err = handleEvent(gh, payload)
	handleError(err)
}

func handleEvent(gh *GithubClient, payload []byte) error {
	fmt.Println("Handling Event. Payload:")
	fmt.Println(string(payload))

	if cs := NewCheckSuiteWebhook(payload); cs != nil {
		fmt.Println("Handling check suite event.")
		err := complete(gh, cs)
		handleError(err)
		return nil
	}

	return errors.New("Error: Invalid or unsupported payload body.")
}

func handleError(err error) {
	if err != nil {
		fmt.Println(err)
		os.Exit(1)
	}
}

func complete(gh *GithubClient, cs *CheckSuiteWebhook) error {
	if cs.IsSucceeded() {
		return gh.SetStatus(cs.GetStatusesUrl(), cs.CheckSuite.HeadSha, succeededBody)
	}
	if cs.IsFailed() {
		return gh.SetStatus(cs.GetStatusesUrl(), cs.CheckSuite.HeadSha, failedBody)
	}
	// This is redundant as the status is already set on pull request open, but it can't hurt in case we have
	// failed to handle previous events due to dropped webhooks or API/runner failures.
	return gh.SetStatus(cs.GetStatusesUrl(), cs.CheckSuite.HeadSha, pendingBody)
}

func help() {
	help := `Update pull request status checks based on github webhook events.

USAGE
  go run main.go <payload json file>

BEHAVIORS
  complete:
    Sets the check enforcer status for a commit to the value of the check_suite status
    Handles payload type: https://docs.github.com/en/developers/webhooks-and-events/webhooks/webhook-events-and-payloads#check_suite`

	fmt.Println(help)
}
