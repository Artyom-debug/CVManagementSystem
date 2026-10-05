using Application.Commands.Integrations;
using Application.Common.Exceptions;
using Application.Dtos;
using Infrastructure.Models;
using Infrastructure.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

var handler = new RecordingDropboxHandler();
var service = new DropboxSupportTicketFileService(
    new HttpClient(handler),
    Options.Create(new DropboxOptions
    {
        AppKey = "test-app-key",
        AppSecret = "test-app-secret",
        RefreshToken = "test-refresh-token",
        Folder = "/SupportTickets"
    }));

await service.UploadAsync(new SupportTicketFileDto(
    "Cannot submit a CV",
    "candidate@example.test (Candidate)",
    "Backend Developer",
    "https://example.test/positions/123",
    "High",
    ["admin1@example.test", "admin2@example.test"]), CancellationToken.None);

Check(handler.TokenRequested, "Dropbox refresh-token request was not sent.");
Check(handler.UploadRequested, "Dropbox upload request was not sent.");
Check(handler.UploadPath.StartsWith("/SupportTickets/support-ticket-") &&
      handler.UploadPath.EndsWith(".json"), "Dropbox path is incorrect.");
using (var document = JsonDocument.Parse(handler.UploadedContent))
{
    var root = document.RootElement;
    Check(root.GetProperty("summary").GetString() == "Cannot submit a CV", "Summary JSON property is incorrect.");
    Check(root.GetProperty("reportedBy").GetString() == "candidate@example.test (Candidate)", "Reporter JSON property is incorrect.");
    Check(root.GetProperty("position").GetString() == "Backend Developer", "Position JSON property is incorrect.");
    Check(root.GetProperty("link").GetString() == "https://example.test/positions/123", "Link JSON property is incorrect.");
    Check(root.GetProperty("priority").GetString() == "High", "Priority JSON property is incorrect.");
    Check(root.GetProperty("adminEmails").GetArrayLength() == 2, "Administrator recipients are incorrect.");
}

var validator = new CreateSupportTicketCommandValidator();
Check(validator.Validate(new CreateSupportTicketCommand("A problem", "Average", "https://example.test/page", null)).IsValid,
    "Valid support ticket was rejected.");
Check(!validator.Validate(new CreateSupportTicketCommand("A problem", "Urgent", "https://example.test/page", null)).IsValid,
    "Unknown priority was accepted.");
Check(!validator.Validate(new CreateSupportTicketCommand("A problem", "Low", "javascript:alert(1)", null)).IsValid,
    "Unsafe URL was accepted.");

handler.FailUpload = true;
try
{
    await service.UploadAsync(new SupportTicketFileDto("Test", "Reporter", "Not applicable", "https://example.test", "Low", ["admin@example.test"]), CancellationToken.None);
    throw new Exception("Failed Dropbox upload was accepted.");
}
catch (SupportTicketDeliveryException)
{
    // A failed upload must not be reported as a successfully submitted ticket.
}

Console.WriteLine("PASS: Dropbox OAuth, JSON contract, path, command validation, and upload failure.");

static void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

sealed class RecordingDropboxHandler : HttpMessageHandler
{
    public bool TokenRequested { get; private set; }
    public bool UploadRequested { get; private set; }
    public bool FailUpload { get; set; }
    public string UploadPath { get; private set; } = string.Empty;
    public byte[] UploadedContent { get; private set; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsoluteUri == "https://api.dropboxapi.com/oauth2/token")
        {
            TokenRequested = true;
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (!form.Contains("grant_type=refresh_token") || !form.Contains("refresh_token=test-refresh-token"))
                throw new Exception("Dropbox refresh-token form is incorrect.");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"test-access-token\"}", Encoding.UTF8, "application/json")
            };
        }

        if (request.RequestUri?.AbsoluteUri == "https://content.dropboxapi.com/2/files/upload")
        {
            UploadRequested = true;
            if (request.Headers.Authorization?.Parameter != "test-access-token")
                throw new Exception("Dropbox bearer token is missing.");
            if (request.Content?.Headers.ContentType?.MediaType != "application/octet-stream")
                throw new Exception("Dropbox content type is incorrect.");
            var arguments = request.Headers.GetValues("Dropbox-API-Arg").Single();
            using var document = JsonDocument.Parse(arguments);
            UploadPath = document.RootElement.GetProperty("path").GetString()!;
            UploadedContent = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(FailUpload ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
        }

        throw new Exception("Unexpected Dropbox request: " + request.RequestUri);
    }
}
