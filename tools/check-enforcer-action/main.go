package main

import (
	"fmt"
	"os"
)

func main() {
	if len(os.Args) <= 2 {
		help()
		os.Exit(1)
	}

	cmd := os.Args[1]
	payload := os.Args[2]

	if cmd == "create" {
		if err := create(payload); err != nil {
			fmt.Println(err)
			os.Exit(1)
		}
		return
	}

	if cmd == "complete" {
		if err := complete(payload); err != nil {
			fmt.Println(err)
			os.Exit(1)
		}
		return
	}

	help()
	os.Exit(1)
}

func create(payload string) error {
	return nil
}

func complete(payload string) error {
	return nil
}

func help() {
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
}
