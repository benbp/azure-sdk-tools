package main

import (
	"encoding/json"
	"fmt"
	"github.com/stretchr/testify/assert"
	"io/ioutil"
	"net/http"
	"net/http/httptest"
	"testing"
)

func startTestServer(assertionFunc func(r *http.Request), response []byte) *httptest.Server {
	return httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assertionFunc(r)
		w.Write(response)
	}))
}

func getBody(t *testing.T, r *http.Request) StatusBody {
	body, err := ioutil.ReadAll(r.Body)
	assert.NoError(t, err)
	status := StatusBody{}
	assert.NoError(t, json.Unmarshal(body, &status))
	return status
}

func TestCreate(t *testing.T) {
	payload, err := ioutil.ReadFile("./testpayloads/pull_request_event.json")
	assert.NoError(t, err)
	response, err := ioutil.ReadFile("./testpayloads/status_response.json")
	assert.NoError(t, err)

	pr := NewPullRequestWebhook(payload)
	assert.NotNil(t, pr)
	assert.NotEmpty(t, pr.PullRequest.Head.Sha)

	server := startTestServer(func(r *http.Request) {
		assert.Contains(t, pr.GetStatusesUrl(), r.URL.Path)
		assert.Contains(t, r.URL.Path, pr.PullRequest.Head.Sha)
		status := getBody(t, r)
		assert.Equal(t, status.State, CommitStatePending)
	}, response)
	defer server.Close()

	gh, err := NewGithubClient(server.URL, "")
	assert.NoError(t, err)

	err = handleEvent(gh, payload)
	assert.NoError(t, err)
}

func TestComplete(t *testing.T) {
	payload, err := ioutil.ReadFile("./testpayloads/check_suite_event.json")
	assert.NoError(t, err)
	response, err := ioutil.ReadFile("./testpayloads/status_response.json")
	assert.NoError(t, err)

	cs := NewCheckSuiteWebhook(payload)
	assert.NotNil(t, cs)
	assert.NotEmpty(t, cs)

	server := startTestServer(func(r *http.Request) {
		assert.Contains(t, cs.GetStatusesUrl(), r.URL.String())
		fmt.Println(r.URL.String())
		assert.Contains(t, r.URL.Path, cs.CheckSuite.HeadSha)

		status := getBody(t, r)
		assert.Equal(t, status.State, CommitStateSuccess)
	}, response)
	defer server.Close()

	gh, err := NewGithubClient(server.URL, "")
	assert.NoError(t, err)

	err = handleEvent(gh, payload)
	assert.NoError(t, err)
}
