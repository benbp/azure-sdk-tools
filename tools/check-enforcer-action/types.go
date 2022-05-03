package main

import (
	"encoding/json"
)

type PullRequestWebhook struct {
	Action      string      `json:"action"`
	Number      int         `json:"number"`
	PullRequest PullRequest `json:"pull_request"`
}

type PullRequest struct {
	Url         string `json:"url"`
	HtmlUrl     string `json:"html_url"`
	Id          int    `json:"id"`
	Number      int    `json:"number"`
	State       string `json:"state"`
	Title       string `json:"title"`
	StatusesUrl string `json:"statuses_url"`
	Head        struct {
		Sha string `json:"sha"`
	} `json:"head"`
	Repo Repo `json:"repo"`
	Base struct {
		Repo Repo `json:"repo"`
	} `json:"base"`
}

type Repo struct {
	Id      int    `json:"id"`
	Name    string `json:"name"`
	Url     string `json:"url"`
	HtmlUrl string `json:"html_url"`
}

type CheckSuiteWebhook struct {
}

type IssueCommentWebhook struct {
}

func NewPullRequestWebhook(payload []byte) *PullRequestWebhook {
	var pr PullRequestWebhook
	if err := json.Unmarshal(payload, &pr); err != nil {
		return nil
	}
	return &pr
}

func NewCheckSuiteWebhook(payload []byte) *CheckSuiteWebhook {
	var cs CheckSuiteWebhook
	if err := json.Unmarshal(payload, &cs); err != nil {
		return nil
	}
	return &cs
}
