using System.Text.Json;
using Desk.Models;

namespace Desk;

public class SessionManager(IHttpContextAccessor httpContextAccessor, DeskConfig config)
{
    private const string ApiKeySessionKey = "ApiKey";
    private const string SelectedCompanyIdKey = "SelectedCompanyId";
    private const string CompaniesKey = "Companies";

    private HttpContext? HttpContext => httpContextAccessor.HttpContext;

    public string? GetApiKey()
    {
        if (config.IsStandalone)
            return config.ApiKey;

        return HttpContext?.Session.GetString(ApiKeySessionKey);
    }

    public void SetApiKey(string apiKey)
    {
        HttpContext?.Session.SetString(ApiKeySessionKey, apiKey);
    }

    public int? GetSelectedCompanyId()
    {
        var value = HttpContext?.Session.GetString(SelectedCompanyIdKey);
        return value is not null ? int.Parse(value) : null;
    }

    public void SetSelectedCompanyId(int? companyId)
    {
        if (companyId is null)
            HttpContext?.Session.Remove(SelectedCompanyIdKey);
        else
            HttpContext?.Session.SetString(SelectedCompanyIdKey, companyId.Value.ToString());
    }

    public List<Company>? GetCompanies()
    {
        var json = HttpContext?.Session.GetString(CompaniesKey);
        return json is not null ? JsonSerializer.Deserialize<List<Company>>(json) : null;
    }

    public void SetCompanies(List<Company> companies)
    {
        HttpContext?.Session.SetString(CompaniesKey, JsonSerializer.Serialize(companies));
    }

    public void ClearCompanies()
    {
        HttpContext?.Session.Remove(CompaniesKey);
        HttpContext?.Session.Remove(SelectedCompanyIdKey);
    }
}
