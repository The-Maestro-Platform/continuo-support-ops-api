namespace SupportOpsApi.Endpoints.Contracts;

public record GithubInfo(string? Repo, string? Branch, string? Commit, string? PullRequest);
