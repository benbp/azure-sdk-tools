package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
)

type GithubClient struct {
	client  *http.Client
	token   string
	BaseUrl url.URL
}

func NewGithubClient(baseUrl string, token string) (*GithubClient, error) {
	u, err := url.Parse(baseUrl)
	if err != nil {
		return nil, err
	}
	return &GithubClient{
		client:  &http.Client{},
		BaseUrl: *u,
		token:   token,
	}, nil
}

func (gh *GithubClient) setHeaders(req *http.Request) {
	req.Header.Add("Accept", "application/vnd.github.v3+json")
	req.Header.Add("Authorization", fmt.Sprintf("token %s", gh.token))
}

func (gh *GithubClient) getUrl(target string) (*url.URL, error) {
	targetUrl, err := url.Parse(target)
	if err != nil {
		return nil, err
	}

	targetUrl.Scheme = gh.BaseUrl.Scheme
	targetUrl.Host = gh.BaseUrl.Host
	return targetUrl, nil
}

func (gh *GithubClient) SetStatus(statusUrl string, commit string, status StatusBody) error {
	body, err := json.Marshal(status)
	if err != nil {
		return err
	}

	target, err := gh.getUrl(statusUrl)
	if err != nil {
		return err
	}

	reader := bytes.NewReader(body)

	req, err := http.NewRequest("POST", target.String(), reader)
	if err != nil {
		return err
	}

	gh.setHeaders(req)

	fmt.Println("POST to", statusUrl, "with state", status.State)
	resp, err := gh.client.Do(req)
	if err != nil {
		return err
	}

	defer resp.Body.Close()
	fmt.Println("Received", resp.Status)
	fmt.Println("Response:")
	if data, err := io.ReadAll(resp.Body); err != nil {
		return err
	} else {
		fmt.Println(fmt.Sprintf("%s", data))
	}

	if resp.StatusCode >= 400 {
		return errors.New(fmt.Sprintf("Received http error %d", resp.StatusCode))
	}

	return nil
}

func (gh *GithubClient) GetLabels(issueUrl string) ([]Label, error) {
	target, err := gh.getUrl(issueUrl)
	if err != nil {
		return nil, err
	}
	target.Path = target.Path + "/labels"

	req, err := http.NewRequest("GET", target.String(), nil)
	if err != nil {
		return nil, err
	}

	gh.setHeaders(req)

	fmt.Println("GET to", issueUrl)
	resp, err := gh.client.Do(req)
	if err != nil {
		return nil, err
	}

	defer resp.Body.Close()
	fmt.Println("Received", resp.Status)
	fmt.Println("Response:")
	if data, err := io.ReadAll(resp.Body); err != nil {
		return nil, err
	} else {
		fmt.Println(fmt.Sprintf("%s", data))
	}

	if resp.StatusCode >= 400 {
		return nil, errors.New(fmt.Sprintf("Received http error %d", resp.StatusCode))
	}

	return nil, nil
}
