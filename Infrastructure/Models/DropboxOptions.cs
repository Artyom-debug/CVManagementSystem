namespace Infrastructure.Models;

public sealed class DropboxOptions
{
    public const string SectionName = "Dropbox";

    public string AppKey { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string Folder { get; set; } = "/SupportTickets";
}
