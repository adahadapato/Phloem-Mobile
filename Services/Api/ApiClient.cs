using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Phloem.Services.Api;

/// <summary>
/// The one way the app talks to the Phloem API. Every call returns an <see cref="ApiResult"/> /
/// <see cref="ApiResult{T}"/>: never throws for HTTP errors or a missing connection.
/// </summary>
/// <remarks>
/// <para>Paths are relative to the API's address, e.g. <c>"api/auth/login"</c> (see <see cref="Settings.ApiSettings"/>).</para>
/// <para>Tokens are added and renewed by <see cref="AuthTokenHandler"/>; feature services
/// (<c>AuthService</c>, <c>AccountService</c>, …) never handle them.</para>
/// <para>Failures are read from the API's ProblemDetails: <c>detail</c> → message, <c>code</c> → code,
/// <c>errors</c> → field errors.</para>
/// </remarks>
public sealed class ApiClient(IHttpClientFactory httpClientFactory, ILogger<ApiClient> logger)
{
    /// <summary>Named HttpClient for normal calls (with <see cref="AuthTokenHandler"/>).</summary>
    public const string ClientName = "PhloemApi";

    /// <summary>Named HttpClient for renewing tokens only (no token handler, so it can't loop).</summary>
    public const string RefreshClientName = "PhloemApiRefresh";

    // ───────────────────────────── 1. JSON calls ─────────────────────────────

    /// <summary>GET a value.</summary>
    public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, path), ct);

    /// <summary>POST a body and read the answer.</summary>
    public Task<ApiResult<T>> PostAsync<T>(string path, object? body, CancellationToken ct = default) =>
        SendAsync<T>(WithJson(HttpMethod.Post, path, body), ct);

    /// <summary>POST a body; the answer has no content (204).</summary>
    public Task<ApiResult> PostAsync(string path, object? body = null, CancellationToken ct = default) =>
        SendAsync(WithJson(HttpMethod.Post, path, body), ct);

    /// <summary>PUT a body and read the answer.</summary>
    public Task<ApiResult<T>> PutAsync<T>(string path, object? body, CancellationToken ct = default) =>
        SendAsync<T>(WithJson(HttpMethod.Put, path, body), ct);

    /// <summary>PUT a body; the answer has no content (204).</summary>
    public Task<ApiResult> PutAsync(string path, object? body, CancellationToken ct = default) =>
        SendAsync(WithJson(HttpMethod.Put, path, body), ct);

    /// <summary>DELETE; the answer has no content (204).</summary>
    public Task<ApiResult> DeleteAsync(string path, CancellationToken ct = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Delete, path), ct);

    // ───────────────────────────── 2. File uploads ─────────────────────────────

    /// <summary>Uploads one file as a form (multipart), e.g. a photo or a document.</summary>
    /// <param name="method">POST or PUT, as the endpoint expects.</param>
    /// <param name="path">The endpoint.</param>
    /// <param name="fieldName">The form field the endpoint reads the file from (e.g. "photo", "logo", "file").</param>
    /// <param name="content">The file.</param>
    /// <param name="fileName">The file's name.</param>
    /// <param name="contentType">The file's type, e.g. "image/jpeg".</param>
    /// <param name="fields">Other form fields (e.g. a document type), or null.</param>
    /// <param name="ct">Cancellation.</param>
    public Task<ApiResult<T>> UploadAsync<T>(HttpMethod method, string path, string fieldName, Stream content,
                                             string fileName, string contentType,
                                             IReadOnlyDictionary<string, string>? fields = null,
                                             CancellationToken ct = default) =>
        SendAsync<T>(new HttpRequestMessage(method, path)
        {
            Content = Form(fieldName, content, fileName, contentType, fields)
        }, ct);

    /// <summary>Uploads one file as a form, for endpoints that answer with no body (204), e.g. "Delivered".</summary>
    /// <inheritdoc cref="UploadAsync{T}"/>
    public Task<ApiResult> UploadAsync(HttpMethod method, string path, string fieldName, Stream content,
                                       string fileName, string contentType,
                                       IReadOnlyDictionary<string, string>? fields = null,
                                       CancellationToken ct = default) =>
        SendAsync(new HttpRequestMessage(method, path)
        {
            Content = Form(fieldName, content, fileName, contentType, fields)
        }, ct);

    /// <summary>The multipart form: the file, then the other fields.</summary>
    private static MultipartFormDataContent Form(string fieldName, Stream content, string fileName, string contentType,
                                                 IReadOnlyDictionary<string, string>? fields)
    {
        var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, fieldName, fileName);

        foreach (var (name, value) in fields ?? new Dictionary<string, string>())
            form.Add(new StringContent(value), name);

        return form;
    }

    // ───────────────────────────── 3. Sending ─────────────────────────────

    private static HttpRequestMessage WithJson(HttpMethod method, string path, object? body) => new(method, path)
    {
        Content = body is null ? null : JsonContent.Create(body, body.GetType(), options: ApiJson.Options)
    };

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        var (response, error) = await TrySendAsync(request, ct);
        if (error is not null) return error;

        using (response)
        {
            if (!response!.IsSuccessStatusCode) return await ReadErrorAsync(response, ct);

            try
            {
                var value = await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, ct);
                return value is null ? ApiError.Unexpected((int)response.StatusCode) : ApiResult<T>.Success(value);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "The API's answer to {Method} {Path} couldn't be read.", request.Method,
                                request.RequestUri);
                return ApiError.Unexpected((int)response.StatusCode);
            }
        }
    }

    private async Task<ApiResult> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var (response, error) = await TrySendAsync(request, ct);
        if (error is not null) return error;

        using (response)
        {
            return response!.IsSuccessStatusCode ? ApiResult.Success() : await ReadErrorAsync(response, ct);
        }
    }

    /// <summary>Sends, turning "no connection" and "too slow" into errors instead of exceptions.</summary>
    private async Task<(HttpResponseMessage? Response, ApiError? Error)> TrySendAsync(HttpRequestMessage request,
                                                                                     CancellationToken ct)
    {
        using (request)
        {
            try
            {
                var client = httpClientFactory.CreateClient(ClientName);
                return (await client.SendAsync(request, ct), null);
            }
            catch (Exception ex) when (ct.IsCancellationRequested && ex is not OperationCanceledException)
            {
                // The CALLER cancelled (e.g. the user picked another country). On Android the request fails with
                // a WebException "Canceled" instead of the usual OperationCanceledException, so it's turned into the
                // usual one here: screens already expect that and simply ignore it
                throw new OperationCanceledException("The request was cancelled.", ex, ct);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Couldn't reach the API for {Method} {Path}.", request.Method, request.RequestUri);
                return (null, ApiError.Unreachable);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return (null, ApiError.Timeout);
            }
            catch (Exception ex) when (ex is WebException or IOException)
            {
                // Android's network layer reports a lost or refused connection this way
                logger.LogWarning(ex, "Couldn't reach the API for {Method} {Path}.", request.Method, request.RequestUri);
                return (null, ApiError.Unreachable);
            }
        }
    }

    // ───────────────────────────── 4. Errors ─────────────────────────────

    /// <summary>Reads the API's ProblemDetails answer.</summary>
    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return Fallback(status);

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            var message = Text(root, "detail") ?? Text(root, "title") ?? Fallback(status).Message;
            var code = Text(root, "code") ?? $"Http.{status}";

            var fields = new Dictionary<string, string[]>();
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject().Where(f => f.Value.ValueKind == JsonValueKind.Array))
                    fields[field.Name] = field.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty)
                                                    .Where(m => m.Length > 0).ToArray();
            }

            return new ApiError(status, code, message, fields);
        }
        catch (JsonException)
        {
            return Fallback(status);
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static ApiError Fallback(int status) => status == 401 ? ApiError.SessionExpired : ApiError.Unexpected(status);
}
