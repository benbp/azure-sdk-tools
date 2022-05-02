package main

type PullRequestWebhook struct {
	Action      string      `json:"action"`
	Number      int         `json:"number"`
	PullRequest PullRequest `json:"pull_request"`
}

type PullRequest struct {
	Url     string `json:"url"`
	HtmlUrl string `json:"html_url"`
	Id      string `json:"id"`
	Number  int    `json:"number"`
	State   string `json:"state"`
	Title   string `json:"title"`
	Head    struct {
		Sha string `json:"sha"`
	} `json:"head"`
	Repo Repo `json:"repo"`
	Base struct {
		Repo Repo `json:"repo"`
	} `json:"base"`
}

type Repo struct {
	Id      string `json:"id"`
	Name    string `json:"name"`
	Url     string `json:"url"`
	HtmlUrl string `json:"html_url"`
}

type CheckSuiteWebhook struct {
}

type IssueCommentWebhook struct {
}
