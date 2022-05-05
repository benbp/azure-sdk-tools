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

func getBody(t *testing.T, r *http.Request) StatusBody {
	body, err := ioutil.ReadAll(r.Body)
	assert.NoError(t, err)
	status := StatusBody{}
	assert.NoError(t, json.Unmarshal(body, &status))
	return status
}

func TestCheckSuite(t *testing.T) {
	payload, err := ioutil.ReadFile("./testpayloads/check_suite_event.json")
	assert.NoError(t, err)
	response, err := ioutil.ReadFile("./testpayloads/status_response.json")
	assert.NoError(t, err)

	cs := NewCheckSuiteWebhook(payload)
	assert.NotNil(t, cs)
	assert.NotEmpty(t, cs)

	fn := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assert.Contains(t, cs.GetStatusesUrl(), r.URL.String())
		assert.Contains(t, r.URL.Path, cs.CheckSuite.HeadSha)

		status := getBody(t, r)
		assert.Equal(t, status.State, CommitStateSuccess)
		w.Write(response)
	})
	server := httptest.NewServer(fn)
	defer server.Close()

	gh, err := NewGithubClient(server.URL, "")
	assert.NoError(t, err)

	err = handleEvent(gh, payload)
	assert.NoError(t, err)
}

func TestCommentOverride(t *testing.T) {
	payload, err := ioutil.ReadFile("./testpayloads/issue_comment_event.json")
	assert.NoError(t, err)
	pullRequestResponse, err := ioutil.ReadFile("./testpayloads/pull_request_response.json")
	assert.NoError(t, err)
	statusResponse, err := ioutil.ReadFile("./testpayloads/status_response.json")
	assert.NoError(t, err)

	ic := NewIssueCommentWebhook(payload)
	assert.NotNil(t, ic)
	assert.NotEmpty(t, ic)
	pr := NewPullRequest(pullRequestResponse)
	assert.NotNil(t, pr)
	assert.NotEmpty(t, pr)

	handledComment, postedStatus := false, false

	fn := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		response := []byte{}
		if strings.Contains(ic.GetPullsUrl(), r.URL.String()) {
			response = pullRequestResponse
			handledComment = true
		} else if strings.Contains(pr.GetStatusesUrl(), r.URL.String()) {
			response = statusResponse
			postedStatus = true
		} else {
			assert.Fail(t, "Unexpected request to "+r.URL.String())
		}
		w.Write(response)
	})
	server := httptest.NewServer(fn)
	defer server.Close()

	gh, err := NewGithubClient(server.URL, "")
	assert.NoError(t, err)

	err = handleEvent(gh, payload)
	assert.NoError(t, err)

	assert.True(t, handledComment)
	assert.True(t, postedStatus)
}
