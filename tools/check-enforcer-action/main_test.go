package main

import (
	"encoding/json"
	"github.com/stretchr/testify/assert"
	"io/ioutil"
	"testing"
)

func TestCreate(t *testing.T) {
	payload, err := ioutil.ReadFile("./testpayloads/pull_request.json")
	assert.NoError(t, err)

	var pr PullRequest
	err = json.Unmarshal([]byte(payload), &pr)
}
