package main

import (
	"encoding/json"
	"github.com/stretchr/testify/assert"
	"io/ioutil"
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestCreate(t *testing.T) {
	payload, err := ioutil.ReadFile("./testpayloads/pull_request_event.json")
	assert.NoError(t, err)
	response, err := ioutil.ReadFile("./testpayloads/status_response_gh.json")
	assert.NoError(t, err)
	var pr PullRequestWebhook
	err = json.Unmarshal([]byte(payload), &pr)
	assert.NoError(t, err)
	assert.NotEmpty(t, pr.PullRequest.Head.Sha)

	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assert.Contains(t, r.URL.String(), pr.PullRequest.Head.Sha)
		w.Write(response)
	}))
	defer server.Close()

	gh, err := NewGithubClient(server.URL, "")
	assert.NoError(t, err)

	err = create(gh, string(payload))
	assert.NoError(t, err)
}
