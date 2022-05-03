package main

import (
	"errors"
	"fmt"
	"io/ioutil"
	"os"
)

const GithubTokenKey = "GITHUB_TOKEN"
const CommitStatusContext = "azsdk/check-enforcer-action"

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

	if pr := NewPullRequestWebhook(payload); pr != nil {
		create(gh, pr)
		handleError(err)
		return
	}

	if cs := NewCheckSuiteWebhook(payload); cs != nil {
		complete(gh, cs)
		handleError(err)
		return
	}

	handleError(errors.New("Error: Invalid or unsupported payload body."))
}

func handleError(err error) {
	if err != nil {
		fmt.Println(err)
		os.Exit(1)
	}
}

func create(gh *GithubClient, pr *PullRequestWebhook) error {
	body := StatusBody{
		State:       CommitStatePending,
		Description: "Waiting for all checks to complete",
		Context:     CommitStatusContext,
	}
	return gh.SetStatus(pr.PullRequest.StatusesUrl, pr.PullRequest.Head.Sha, body)
}

func complete(gh *GithubClient, cs *CheckSuiteWebhook) error {
	return nil
}

func help() {
	help := `Update pull request status checks based on github webhook events.

USAGE
  go run main.go <payload json file>

BEHAVIORS
  create:
    Creates or sets a new status for a commit to state 'pending'
    Handles payload type: https://docs.github.com/en/developers/webhooks-and-events/webhooks/webhook-events-and-payloads#pull_request
  complete:
    Sets the check enforcer status for a commit to the value of the check_suite status
    Handles payload type: https://docs.github.com/en/developers/webhooks-and-events/webhooks/webhook-events-and-payloads#check_suite`

	fmt.Println(help)
}
