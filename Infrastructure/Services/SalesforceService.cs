using Application.Dtos;
using Application.Interfaces;
using Infrastructure.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Infrastructure.Services;

public sealed class SalesforceService : ISalesForceService
{
    private readonly HttpClient _httpClient;
    private readonly SalesforceOptions _options;
    private readonly Uri _loginUri;

    public SalesforceService(HttpClient httpClient, IOptions<SalesforceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException("Salesforce client credentials were not configured.");

        if (!Uri.TryCreate(_options.LoginUrl, UriKind.Absolute, out var loginUri) || !IsSalesforceUri(loginUri))
            throw new InvalidOperationException("Salesforce:LoginUrl must be an HTTPS Salesforce org URL.");

        if (!Regex.IsMatch(_options.ApiVersion, @"^v\d+\.\d+$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Salesforce:ApiVersion must have a value such as 'v68.0'.");

        _loginUri = loginUri;
    }

    public async Task CreateAccountWithContactAsync(SalesForceDto data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);

        var firstName = data.FirstName?.Trim();
        var lastName = data.LastName?.Trim();
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("A contact must have a first name and last name.", nameof(data));
        if (string.IsNullOrWhiteSpace(data.Email))
            throw new ArgumentException("A contact must have an email address.", nameof(data));

        var accountName = string.IsNullOrWhiteSpace(data.OrganizationName)
            ? $"Individual - {firstName} {lastName}"
            : data.OrganizationName.Trim();

        var (accessToken, instanceUri) = await GetAccessTokenAsync(cancellationToken);
        var payload = CreateAccountTree(data, accountName, firstName, lastName);
        var endpoint = new Uri(instanceUri, $"/services/data/{_options.ApiVersion}/composite/tree/Account");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Salesforce Account/Contact creation failed with HTTP {(int)response.StatusCode}. {GetErrorCodes(body)}", null, response.StatusCode);

        using var result = JsonDocument.Parse(body);
        if (!result.RootElement.TryGetProperty("hasErrors", out var hasErrors) ||
            hasErrors.ValueKind != JsonValueKind.False ||
            !result.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() != 2)
        {
            throw new InvalidOperationException($"Salesforce did not confirm creation of both the Account and Contact. {GetErrorCodes(body)}");
        }
    }

    private async Task<(string AccessToken, Uri InstanceUri)> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_loginUri, "/services/oauth2/token"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Salesforce OAuth token request failed with HTTP {(int)response.StatusCode}. {GetErrorCodes(body)}", null, response.StatusCode);

        using var token = JsonDocument.Parse(body);
        var accessToken = GetString(token.RootElement, "access_token");
        var instanceUrl = GetString(token.RootElement, "instance_url");

        if (string.IsNullOrWhiteSpace(accessToken) ||
            !Uri.TryCreate(instanceUrl, UriKind.Absolute, out var instanceUri) ||
            !IsSalesforceUri(instanceUri))
        {
            throw new InvalidOperationException("Salesforce returned an invalid OAuth token response.");
        }

        return (accessToken, instanceUri);
    }

    private static object CreateAccountTree(SalesForceDto data, string accountName, string firstName, string lastName)
    {
        var account = new Dictionary<string, object>
        {
            ["attributes"] = new { type = "Account", referenceId = "account1" },
            ["Name"] = accountName
        };

        AddOptional(account, "Phone", data.OrganizationPhone);
        AddOptional(account, "Website", data.OrganizationWebSite);
        AddOptional(account, "Industry", data.Industry);

        var contact = new Dictionary<string, object>
        {
            ["attributes"] = new { type = "Contact", referenceId = "contact1" },
            ["FirstName"] = firstName,
            ["LastName"] = lastName
        };

        AddOptional(contact, "Email", data.Email);
        AddOptional(contact, "Title", data.Position);
        AddOptional(contact, "Phone", data.Phone);

        account["Contacts"] = new { records = new[] { contact } };
        return new { records = new[] { account } };
    }

    private static void AddOptional(Dictionary<string, object> record, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            record[name] = value.Trim();
    }

    private static bool IsSalesforceUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) &&
        uri.AbsolutePath == "/" &&
        uri.Host.EndsWith(".salesforce.com", StringComparison.OrdinalIgnoreCase);

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string GetErrorCodes(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var codes = new List<string>();
            CollectErrorCodes(document.RootElement, codes);

            return codes.Count == 0 ? string.Empty : $"Error code: {string.Join(", ", codes.Distinct())}.";
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void CollectErrorCodes(JsonElement element, List<string> codes)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectErrorCodes(item, codes);

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return;

        foreach (var name in new[] { "error", "errorCode", "statusCode" })
        {
            if (GetString(element, name) is { } code)
                codes.Add(code);
        }

        if (element.TryGetProperty("results", out var results))
            CollectErrorCodes(results, codes);
        if (element.TryGetProperty("errors", out var errors))
            CollectErrorCodes(errors, codes);
    }
}
