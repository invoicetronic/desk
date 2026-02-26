using Desk.Models;

namespace Desk;

public class ApiManager(ApiClient apiClient)
{
    private static readonly Dictionary<Type, string> Endpoints = new()
    {
        [typeof(Company)] = "company",
        [typeof(Send)] = "send",
        [typeof(Receive)] = "receive",
        [typeof(Update)] = "update",
        [typeof(Status)] = "status",
        [typeof(InvoiceUploadResponse)] = "send/file"
    };

    public async Task<(List<T>? entity, int totalCount)> List<T>(
        int page = 1, int pageSize = 100, string? sort = null, string? extraQuery = null) where T : BaseModel
    {
        var endpoint = Endpoints[typeof(T)];
        var qs = $"{endpoint}?page={page}&page_size={pageSize}";

        if (sort is not null)
            qs += $"&sort={sort}";

        if (extraQuery is not null)
            qs += $"&{extraQuery.TrimStart('&')}";

        return await apiClient.List<T>(qs);
    }

    public async Task<T?> Get<T>(int id, string? extraQuery = null) where T : class
    {
        var endpoint = Endpoints[typeof(T)];
        var url = $"{endpoint}/{id}";
        if (extraQuery is not null)
            url += $"?{extraQuery.TrimStart('?')}";
        return await apiClient.Get<T>(url);
    }

    public async Task<T?> Add<T>(T item) where T : BaseModel
    {
        var endpoint = Endpoints[typeof(T)];
        return await apiClient.Post<T>(endpoint, item);
    }

    public async Task<T?> Update<T>(T item) where T : BaseModel
    {
        var endpoint = Endpoints[typeof(T)];
        return await apiClient.Put<T>(endpoint, item);
    }

    public async Task Delete<T>(int id, bool force = false) where T : class
    {
        var endpoint = Endpoints[typeof(T)];
        var url = $"{endpoint}/{id}";
        if (force) url += "?force=true";
        _ = await apiClient.Delete<T>(url);
    }

    public async Task<InvoiceUploadResponse?> UploadInvoice(IFormFile file)
        => await apiClient.PostMultipart<InvoiceUploadResponse>(Endpoints[typeof(InvoiceUploadResponse)], file);

    public async Task<byte[]?> Export(string queryString)
        => await apiClient.GetBytes($"export?{queryString.TrimStart('?')}");

    public async Task<Status?> GetStatus()
        => await apiClient.Get<Status>("status");
}
