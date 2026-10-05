namespace Infrastructure.Models;

public sealed class SalesforceOptions
{
    public const string SectionName = "Salesforce";

    public string LoginUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string ApiVersion { get; set; } = "v68.0";
}