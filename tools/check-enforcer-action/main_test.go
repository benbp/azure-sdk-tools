package main

import (
	"encoding/json"
	"github.com/stretchr/testify/assert"
	"io/ioutil"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

type Payloads struct {
	CheckSuiteEvent     []byte
	IssueCommentEvent   []byte
	PullRequestResponse []byte
	CheckSuiteResponse  []byte
	StatusResponse      []byte
}

func getPayloads() (Payloads, error) {
	payloads := Payloads{}
	var err error
	payloads.CheckSuiteEvent, err = ioutil.ReadFile("./testpayloads/check_suite_event.json")
	if err != nil {
		return Payloads{}, err
	}
	payloads.IssueCommentEvent, err = ioutil.ReadFile("./testpayloads/issue_comment_event.json")
	if err != nil {
		return Payloads{}, err
	}
	payloads.PullRequestResponse, err = ioutil.ReadFile("./testpayloads/pull_request_response.json")
	if err != nil {
		return Payloads{}, err
	}
	payloads.CheckSuiteResponse, err = ioutil.ReadFile("./testpayloads/check_suite_response.json")
	if err != nil {
		return Payloads{}, err
	}
	payloads.StatusResponse, err = ioutil.ReadFile("./testpayloads/status_response.json")
	if err != nil {
		return Payloads{}, err
	}
	return payloads, nil
}

func getBody(t *testing.T, r *http.Request) StatusBody {
	body, err := ioutil.ReadAll(r.Body)
	assert.NoError(t, err)
	status := StatusBody{}
	assert.NoError(t, json.Unmarshal(body, &status))
	return status
}

func TestCheckSuite(t *testing.T) {
	payloads, err := getPayloads()
	assert.NoError(t, err)
	cs := NewCheckSuiteWebhook(payloads.CheckSuiteEvent)
	assert.NotEmpty(t, cs)

	postedStatus := false

	fn := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assert.Contains(t, cs.GetStatusesUrl(), r.URL.String())
		assert.Contains(t, r.URL.Path, cs.CheckSuite.HeadSha)

		assert.Equal(t, "POST", r.Method)
		status := getBody(t, r)
		assert.Equal(t, status.State, CommitStateSuccess)
		postedStatus = true

		w.Write(payloads.StatusResponse)
	})
	server := httptest.NewServer(fn)
	defer server.Close()

	gh, err := NewGithubClient(server.URL, "", "octocoders-linter")
	assert.NoError(t, err)

	err = handleEvent(gh, payloads.IssueCommentEvent)
	assert.NoError(t, err)
	assert.True(t, postedStatus, "Should POST status")
}

func TestCommentOverride(t *testing.T) {
	payloads, err := getPayloads()
	assert.NoError(t, err)
	ic := NewIssueCommentWebhook(payloads.IssueCommentEvent)
	assert.NotEmpty(t, ic)
	pr := NewPullRequest(payloads.PullRequestResponse)
	assert.NotEmpty(t, pr)

	cases := []struct {
		Comment       string
		ExpectedState CommitState
		PostStatus    bool
	}{
		{"/check-enforcer override", CommitStateSuccess, true},
		{"/check-enforcer reset", CommitStatePending, true},
		{"/check-enforcer evaluate", CommitStateSuccess, true},
		{"/check-enforcer foobar", "", false},
		{"/azp run", "", false},
	}

	for _, tc := range cases {
		postedStatus := false

		fn := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			response := []byte{}
			if strings.Contains(ic.GetPullsUrl(), r.URL.String()) {
				response = payloads.PullRequestResponse
			} else if strings.Contains(pr.GetCheckSuiteUrl(), r.URL.String()) {
				response = payloads.CheckSuiteResponse
			} else if strings.Contains(pr.GetStatusesUrl(), r.URL.String()) {
				response = payloads.StatusResponse
				assert.Equal(t, "POST", r.Method)
				status := getBody(t, r)
				assert.Equal(t, status.State, tc.ExpectedState)
				postedStatus = true
			} else {
				assert.Fail(t, "Unexpected request to "+r.URL.String())
			}
			w.Write(response)
		})
		server := httptest.NewServer(fn)
		defer server.Close()

		gh, err := NewGithubClient(server.URL, "", "Octocat App")
		assert.NoError(t, err)

		replaced := strings.ReplaceAll(string(payloads.IssueCommentEvent), "You are totally right! I'll get this fixed right away.", tc.Comment)
		err = handleEvent(gh, []byte(replaced))
		assert.NoError(t, err)
		assert.True(t, postedStatus, "Should post status")
		assert.Equal(t, tc.PostStatus, postedStatus, "Should post status: %b", tc.PostStatus)
	}
}
