package main

import (
	"fmt"
	"os"
)

func main() {
	if len(os.Args) <= 2 {
		help := `Update pull request status checks based on github webhook events.

USAGE
  go run main.go [create|complete] <payload>

COMMANDS
  create:
    Creates or sets a new status for a commit to state 'pending'
    Expects payload: https://docs.github.com/en/developers/webhooks-and-events/webhooks/webhook-events-and-payloads#pull_request
  complete:
    Sets the check enforcer status for a commit to the value of the check_suite status
    Expects payload: https://docs.github.com/en/developers/webhooks-and-events/webhooks/webhook-events-and-payloads#check_suite`

		fmt.Println(help)
		os.Exit(1)
	}

}
