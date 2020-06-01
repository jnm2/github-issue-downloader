using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GitHubIssueDownloader
{
    public sealed class GitHubGraphQLClient : IDisposable
    {
        private readonly HttpClient client;

        public GitHubGraphQLClient(string accessToken, string userAgent)
        {
            client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = ~DecompressionMethods.None
            })
            {
                BaseAddress = new Uri("https://api.github.com/graphql"),
                DefaultRequestHeaders =
                {
                    Authorization = new AuthenticationHeaderValue("bearer", accessToken),
                    UserAgent = { ProductInfoHeaderValue.Parse(userAgent) },
                },
            };
        }

        public void Dispose() => client.Dispose();

        [DebuggerNonUserCode]
        public async Task<JsonDocument> GetQueryDataDocumentAsync(string query, IReadOnlyDictionary<string, object?>? variables, CancellationToken cancellationToken)
        {
            return GetDataDocument(await GetQueryBytesAsync(query, variables, cancellationToken).ConfigureAwait(false));
        }

        private async Task<byte[]> GetQueryBytesAsync(string query, IReadOnlyDictionary<string, object?>? variables, CancellationToken cancellationToken)
        {
            using var requestStream = CreateRequestStream(query, variables);

            using var response = await client.PostAsync(client.BaseAddress, new StreamContent(requestStream), cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        private static Stream CreateRequestStream(string query, IReadOnlyDictionary<string, object?>? variables)
        {
            var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("query", query);

                if (variables != null && variables.Any())
                {
                    writer.WritePropertyName("variables");
                    JsonSerializer.Serialize(writer, variables);
                }

                writer.WriteEndObject();
            }

            stream.Position = 0;

            return stream;
        }

        [DebuggerNonUserCode]
        private static JsonDocument GetDataDocument(byte[] queryBytes)
        {
            var reader = new Utf8JsonReader(queryBytes);

            if (!(reader.Read() && reader.TokenType == JsonTokenType.StartObject))
                throw new InvalidDataException("Expected JSON object");

            var document = (JsonDocument?)null;

            while (true)
            {
                if (!reader.Read())
                    throw new InvalidDataException("Expected property name or end object.");

                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new InvalidOperationException();

                switch (reader.GetString())
                {
                    case "data":
                        document = JsonDocument.ParseValue(ref reader);
                        break;
                    case "errors":
                        throw new GraphQLException(CreateErrorMessage(ref reader));
                    default:
                        reader.Skip();
                        break;
                }
            }

            return document ?? throw new InvalidDataException("Expected \"errors\" or \"data\" properties.");
        }

        private static string CreateErrorMessage(ref Utf8JsonReader reader)
        {
            if (!(reader.Read() && reader.TokenType == JsonTokenType.StartArray))
                throw new InvalidDataException("Expected JSON array");

            var message = new StringBuilder();

            while (true)
            {
                if (!reader.Read())
                    throw new InvalidDataException("Expected start object or end array.");

                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new InvalidDataException("Expected start object.");

                while (true)
                {
                    if (!reader.Read())
                        throw new InvalidDataException("Expected property name or end object.");

                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new InvalidOperationException();

                    switch (reader.GetString())
                    {
                        case "message":
                            if (!(reader.Read() && reader.TokenType == JsonTokenType.String))
                                throw new InvalidDataException("Expected string.");

                            if (message.Length != 0)
                                message.AppendLine().AppendLine();

                            message.Append(reader.GetString());
                            break;

                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            return message.ToString();
        }
    }
}
