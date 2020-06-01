using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GitHubIssueDownloader
{
    public static class Program
    {
        public static Task Main(string[] args)
        {
            var rootCommand = new RootCommand("Downloads all issues from the specified GitHub repository as markdown files.")
            {
                new Argument<string>("repo", "The GitHub owner and repository names, separated by a forward slash: someowner/somerepo"),
                new Argument<string>("dir", "The output directory which will contain the downloaded markdown files."),
                new Option<string>("--token", "A GitHub personal access token with scope public_repo is required.")
            };

            rootCommand.Handler = CommandHandler.Create<string, string, string, CancellationToken>(DownloadAsync);

            return rootCommand.InvokeAsync(args);
        }

        private static async Task DownloadAsync(
            string repo,
            string dir,
            string token,
            CancellationToken cancellationToken)
        {
            var repoSeparator = repo.IndexOf('/');
            if (repoSeparator == -1) throw new NotImplementedException("Add argument validator");
            var owner = repo[..repoSeparator];
            repo = repo[(repoSeparator + 1)..^0];

            var outputDirectory = Path.GetFullPath(dir);

            using var client = new GitHubGraphQLClient(token, userAgent: nameof(GitHubIssueDownloader));

            var processedCount = 0;

            Console.Write("Fetching first 100 issues...");

            await foreach (var (issuePage, updatedTotalCount) in Queries.EnumerateIssuePagesAsync(client, repo, owner, cancellationToken).ConfigureAwait(false))
            {
                foreach (var issue in issuePage)
                {
                    Console.Write(
                        AnsiCodes.CursorHorizontalAbsolute1
                        + AnsiCodes.EraseLineFromCursorToEnd
                        + $"Fetching {updatedTotalCount} issues ({processedCount / (double)updatedTotalCount:p1} complete)...");

                    var issueNumber = issue.GetProperty("number").GetInt32();
                    var title = issue.GetProperty("title").GetString();

                    Directory.CreateDirectory(outputDirectory);

                    var fileName = $"{issueNumber}. {PathUtils.SanitizeFileName(title, maxLength: 150)}.md";

                    foreach (var sameIssueWithAnyTitle in Directory.GetFiles(outputDirectory, $"{issueNumber}. *.md"))
                    {
                        if (Path.GetFileName(sameIssueWithAnyTitle) != fileName)
                            File.Delete(sameIssueWithAnyTitle);
                    }

                    using var writer = File.CreateText(Path.Join(outputDirectory, fileName));

                    writer.Write("# ");
                    writer.Write(title);
                    writer.Write(" (#");
                    writer.Write(issueNumber);
                    writer.WriteLine(")");
                    writer.WriteLine();

                    WriteCommentMetadata(writer, issue);

                    writer.WriteLine(issue.GetProperty("body").GetString().Trim());

                    await foreach (var commentPage in Queries.EnumerateCommentPagesAsync(
                        client,
                        repo,
                        owner,
                        issueNumber,
                        initialCommentsElement: issue.GetProperty("comments"),
                        cancellationToken).ConfigureAwait(false))
                    {

                        foreach (var comment in commentPage)
                        {
                            writer.WriteLine();
                            writer.WriteLine("---");
                            writer.WriteLine();

                            WriteCommentMetadata(writer, comment);

                            writer.WriteLine(comment.GetProperty("body").GetString().Trim());
                        }
                    }

                    processedCount++;
                }
            }

            Console.WriteLine(
                AnsiCodes.CursorHorizontalAbsolute1
                + AnsiCodes.EraseLineFromCursorToEnd
                + "Successfully completed.");
        }

        private static void WriteCommentMetadata(TextWriter writer, JsonElement issueOrCommentElement)
        {
            var author = issueOrCommentElement.GetProperty("author");
            var authorLogin = author.ValueKind == JsonValueKind.Null ? "ghost" : author.GetProperty("login").GetString();
            var authorUrl = author.ValueKind == JsonValueKind.Null ? "https://github.com/ghost" : author.GetProperty("url").GetString();
            var createdAt = issueOrCommentElement.GetProperty("createdAt").GetDateTimeOffset();
            var issueUrl = issueOrCommentElement.GetProperty("url").GetString();

            writer.WriteLine($"##### [**{authorLogin}**]({authorUrl}) commented at [{createdAt}]({issueUrl})");
            writer.WriteLine();
        }
    }
}
