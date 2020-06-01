using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GitHubIssueDownloader
{
    internal static class Queries
    {
        public static async IAsyncEnumerable<(JsonElement.ArrayEnumerator Nodes, int TotalCount)> EnumerateIssuePagesAsync(
            GitHubGraphQLClient client,
            string repo,
            string owner,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var previousCursor = (string?)null;

            while (true)
            {
                var data = await GetIssuePageAsync(client, repo, owner, previousCursor, cancellationToken).ConfigureAwait(false);
                var page = data.RootElement.GetProperty("repository").GetProperty("issues");

                yield return (
                    Nodes: page.GetProperty("nodes").EnumerateArray(),
                    TotalCount: page.GetProperty("totalCount").GetInt32());

                var pageInfo = page.GetProperty("pageInfo");

                if (!pageInfo.GetProperty("hasNextPage").GetBoolean()) break;

                previousCursor = pageInfo.GetProperty("endCursor").GetString();
            }
        }

        public static async Task<JsonDocument> GetIssuePageAsync(
            GitHubGraphQLClient client,
            string repo,
            string owner,
            string? previousCursor,
            CancellationToken cancellationToken)
        {
            return await client.GetQueryDataDocumentAsync(@"
query ($owner: String!, $repo: String!, $previousCursor: String) {
  repository(owner: $owner, name: $repo) {
    issues(first: 100, orderBy: {field: UPDATED_AT, direction: DESC}, after: $previousCursor) {
      nodes {
        number
        title
        author {
          login
          url
        }
        createdAt
        url
        body
        comments(first: 100) {
          nodes {
            body
            author {
              login
              url
            }
            createdAt
            url
          }
          pageInfo {
            hasNextPage
            endCursor
          }
        }
      }
      pageInfo {
        hasNextPage
        endCursor
      }
      totalCount
    }
  }
}",
                new Dictionary<string, object?> { ["owner"] = owner, ["repo"] = repo, ["previousCursor"] = previousCursor },
                cancellationToken).ConfigureAwait(false);
        }

        public static async IAsyncEnumerable<JsonElement.ArrayEnumerator> EnumerateCommentPagesAsync(
            GitHubGraphQLClient client,
            string repo,
            string owner,
            int issueNumber,
            JsonElement initialCommentsElement,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var page = initialCommentsElement;

            while (true)
            {
                yield return page.GetProperty("nodes").EnumerateArray();

                var pageInfo = page.GetProperty("pageInfo");
                if (!pageInfo.GetProperty("hasNextPage").GetBoolean()) break;

                var previousCursor = pageInfo.GetProperty("endCursor").GetString();
                var data = await GetCommentPageAsync(client, repo, owner, issueNumber, previousCursor, cancellationToken).ConfigureAwait(false);

                page = data.RootElement.GetProperty("repository").GetProperty("issue").GetProperty("comments");
            }
        }

        public static async Task<JsonDocument> GetCommentPageAsync(
            GitHubGraphQLClient client,
            string repo,
            string owner,
            int issueNumber,
            string? previousCursor,
            CancellationToken cancellationToken)
        {
            return await client.GetQueryDataDocumentAsync(@"
query ($owner: String!, $repo: String!, $issueNumber: Int!, $previousCursor: String) {
  repository(owner: $owner, name: $repo) {
    issue(number: $issueNumber) {
      comments(first: 100, after: $previousCursor) {
        nodes {
          body
          author {
            login
            url
          }
          createdAt
          url
        }
        pageInfo {
          hasNextPage
          endCursor
        }
      }
    }
  }
}",
                new Dictionary<string, object?> { ["owner"] = owner, ["repo"] = repo, ["issueNumber"] = issueNumber, ["previousCursor"] = previousCursor },
                cancellationToken).ConfigureAwait(false);
        }
    }
}
