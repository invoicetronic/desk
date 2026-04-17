using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Models;

namespace Desk;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly SessionManager _sessionManager;
    private readonly ILogger<ApiClient>? _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiClient(DeskConfig config, SessionManager sessionManager, HttpClient httpClient, ILogger<ApiClient>? logger = null)
    {
        _sessionManager = sessionManager;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(config.ApiUrl.TrimEnd('/') + '/');
        var version = typeof(ApiClient).Assembly.GetName().Version;
        var mode = config.IsStandalone ? "standalone" : config.IsHosted ? "hosted" : "self-hosted";
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"Invoicetronic-Desk/{version!.Major}.{version.Minor}.{version.Build} ({mode})");
        _logger = logger;
    }

    public static string ExtractErrorMessage(string errorBody, System.Net.HttpStatusCode? statusCode = null)
    {
        try
        {
            var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(errorBody, JsonOptions);

            if (errorResponse?.ProblemDetails?.Detail is not null)
                return errorResponse.ProblemDetails.Detail;

            if (errorResponse?.ProblemDetails?.Title is not null)
                return errorResponse.ProblemDetails.Title;
        }
        catch
        {
            // Fall through to status code fallback
        }

        var code = statusCode is not null ? $" ({(int)statusCode} {statusCode})" : "";
        return $"Errore nella richiesta API{code}.";
    }

    private static string BuildErrorMessage(HttpResponseMessage response, string errorBody)
    {
        if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
            return ExtractErrorMessage(errorBody, response.StatusCode);

        var retryAfter = response.Headers.RetryAfter;
        int? seconds = retryAfter switch
        {
            { Delta: { } d } => (int)Math.Ceiling(d.TotalSeconds),
            { Date: { } date } => (int)Math.Max(1, Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds)),
            _ => null
        };

        return seconds is > 0
            ? $"Troppe richieste all'API. Riprova tra {seconds} secondi."
            : "Troppe richieste all'API. Riprova tra qualche istante.";
    }

    private void SetAuth(string? apiKey = null)
    {
        var key = apiKey ?? _sessionManager.GetApiKey();
        if (key is null) return;
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
    }

    private async Task<(T? entity, HttpResponseMessage response)> SendRequest<T>(
        string endpoint, HttpMethod method, object? requestBody = null, string? apiKey = null)
    {
        SetAuth(apiKey);

        var request = new HttpRequestMessage(method, endpoint);

        if (requestBody is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger?.LogError("API Error ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
                throw new HttpRequestException(BuildErrorMessage(response, errorBody), null, response.StatusCode);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(responseContent))
                return (default, response);

            var result = JsonSerializer.Deserialize<T>(responseContent, JsonOptions);
            return (result, response);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"Errore nella richiesta API. {ex.Message}", ex);
        }
    }

    public async Task<T?> Get<T>(string endpoint, string? apiKey = null)
        => (await SendRequest<T>(endpoint, HttpMethod.Get, apiKey: apiKey)).entity;

    public async Task<(List<T>? entity, int totalCount)> List<T>(string endpoint, string? apiKey = null)
    {
        var (entity, response) = await SendRequest<List<T>>(endpoint, HttpMethod.Get, apiKey: apiKey);
        var totalCount = response.Headers.TryGetValues("Invoicetronic-Total-Count", out var values)
            ? int.Parse(values.First())
            : 0;
        return (entity, totalCount);
    }

    public async Task<T?> Post<T>(string endpoint, object body, string? apiKey = null)
        => (await SendRequest<T>(endpoint, HttpMethod.Post, body, apiKey)).entity;

    public async Task<T?> PostMultipart<T>(string endpoint, IFormFile file, string? apiKey = null)
    {
        SetAuth(apiKey);

        using var content = new MultipartFormDataContent();
        await using var fileStream = file.OpenReadStream();
        var fileContent = new StreamContent(fileStream);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        fileContent.Headers.ContentType = extension switch
        {
            ".p7m" => new MediaTypeHeaderValue("application/pkcs7-mime"),
            ".xml" => new MediaTypeHeaderValue("text/xml"),
            _ => new MediaTypeHeaderValue("application/octet-stream")
        };

        var sanitizedFileName = Path.GetFileName(file.FileName);
        content.Add(fileContent, "file", sanitizedFileName);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };

        try
        {
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger?.LogError("API Error ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
                throw new HttpRequestException(BuildErrorMessage(response, errorBody), null, response.StatusCode);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(responseContent))
                return default;

            return JsonSerializer.Deserialize<T>(responseContent, JsonOptions);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"Errore nella richiesta API. {ex.Message}", ex);
        }
    }

    public async Task<T?> Put<T>(string endpoint, object body, string? apiKey = null)
        => (await SendRequest<T>(endpoint, HttpMethod.Put, body, apiKey)).entity;

    public async Task<T?> Delete<T>(string endpoint, string? apiKey = null)
        => (await SendRequest<T>(endpoint, HttpMethod.Delete, apiKey: apiKey)).entity;

    public async Task<byte[]?> GetBytes(string endpoint, string? apiKey = null)
    {
        SetAuth(apiKey);
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

        try
        {
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger?.LogError("API Error ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
                throw new HttpRequestException(BuildErrorMessage(response, errorBody), null, response.StatusCode);
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"Errore nella richiesta API. {ex.Message}", ex);
        }
    }
}
