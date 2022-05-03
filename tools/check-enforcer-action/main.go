package main

import (
	"encoding/json"
	"fmt"
	"io/ioutil"
	"os"
)

const GithubTokenKey = "GITHUB_TOKEN"
const CommitStatusContext = "azsdk/check-enforcer-action"

func main() {
	if len(os.Args) <= 2 {
		help()
		os.Exit(1)
	}

	cmd := os.Args[1]
	payloadPath := os.Args[2]

	payload, err := ioutil.ReadFile(payloadPath)
	handleError(err)

	github_token := os.Getenv(GithubTokenKey)
	if github_token == "" {
		fmt.Println(fmt.Sprintf("WARNING: environment variable '%s' is not set", GithubTokenKey))
	}

	gh, err := NewGithubClient("https://api.github.com", github_token)
	handleError(err)

	if cmd == "create" {
		create(gh, string(payload))
		handleError(err)
		return
	}

	if cmd == "complete" {
		err := complete(gh, string(payload))
		handleError(err)
		return
	}

	help()
	os.Exit(1)
}

func handleError(err error) {
	if err != nil {
		fmt.Println(err)
		os.Exit(1)
	}
}

func create(gh *GithubClient, payload string) error {
	var pr PullRequestWebhook
	err := json.Unmarshal([]byte(payload), &pr)
	if err != nil {
		fmt.Println("Error deserializing pull request payload:")
		return err
	}
	body := StatusBody{
		State:       CommitStatePending,
		Description: "Waiting for all checks to complete",
		Context:     CommitStatusContext,
	}
	return gh.SetStatus(pr.PullRequest.StatusesUrl, pr.PullRequest.Head.Sha, body)
}

func complete(gh *GithubClient, payload string) error {
	return nil
}

func help() {
	help := `Update pull request status checks based on github webhook events.

USAGE
  go run main.go [create|complete] <payload json file>

COMMANDS
  create:
    Creates or sets a new status for a commit to state 'pending'
    Expects payload: https://docs.github.com/en/developers/webhooks-and-events/webhooks/webhook-events-and-payloads#pull_request
  complete:
    Sets the check enforcer status for a commit to the value of the check_suite status
    Expects payload: https://docs.github.com/en/developers/webhooks-and-events/webhooks/webhook-events-and-payloads#check_suite`

	fmt.Println(help)
}
