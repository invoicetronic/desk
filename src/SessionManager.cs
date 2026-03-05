using System.Text.Json;
using Desk.Models;

namespace Desk;

public class SessionManager(IHttpContextAccessor httpContextAccessor, DeskConfig config)
{
    private const string ApiKeySessionKey = "ApiKey";
    private const string SelectedCompanyIdKey = "SelectedCompanyId";
    private const string SelectedCompanyCookieKey = "SelectedCompanyId";
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
        // Session has priority; empty string is sentinel for "all companies"
        var value = HttpContext?.Session.GetString(SelectedCompanyIdKey);
        if (value is not null)
            return value.Length > 0 ? int.Parse(value) : null;

        // No session value → fall back to persistent cookie
        var cookie = HttpContext?.Request.Cookies[SelectedCompanyCookieKey];
        if (cookie is not null && int.TryParse(cookie, out var cookieId))
        {
            HttpContext?.Session.SetString(SelectedCompanyIdKey, cookie);
            return cookieId;
        }

        return null;
    }

    public void SetSelectedCompanyId(int? companyId)
    {
        if (companyId is null)
        {
            // Use empty string as sentinel so cookie fallback is skipped this request
            HttpContext?.Session.SetString(SelectedCompanyIdKey, "");
            HttpContext?.Response.Cookies.Delete(SelectedCompanyCookieKey);
        }
        else
        {
            var value = companyId.Value.ToString();
            HttpContext?.Session.SetString(SelectedCompanyIdKey, value);
            HttpContext?.Response.Cookies.Append(SelectedCompanyCookieKey, value, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(365),
                IsEssential = true
            });
        }
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
