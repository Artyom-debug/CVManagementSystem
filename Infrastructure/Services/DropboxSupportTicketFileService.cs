using Application.Common.Exceptions;
using Application.Dtos;
using Application.Interfaces;
using Infrastructure.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Infrastructure.Services;

public sealed class DropboxSupportTicketFileService : ISupportTicketFileService
{
    private static readonly Uri TokenEndpoint = new("https://api.dropboxapi.com/oauth2/token");
    private static readonly Uri UploadEndpoint = new("https://content.dropboxapi.com/2/files/upload");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly DropboxOptions _options;
    private readonly string _folder;

    public DropboxSupportTicketFileService(HttpClient httpClient, IOptions<DropboxOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.AppKey) || string.IsNullOrWhiteSpace(_options.AppSecret) || string.IsNullOrWhiteSpace(_options.RefreshToken))
            throw new InvalidOperationException("Dropbox app credentials were not configured.");

        _folder = _options.Folder.Trim().TrimEnd('/');
        if (!_folder.StartsWith('/') || _folder.Length < 2 || _folder.Contains("//", StringComparison.Ordinal) || _folder.Split('/').Any(segment => segment is "." or ".."))
            throw new InvalidOperationException("Dropbox:Folder must be an absolute Dropbox folder path, such as '/SupportTickets'.");
    }

    public async Task UploadAsync(SupportTicketFileDto ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        try
        {
            var accessToken = await GetAccessTokenAsync(cancellationToken);
            var fileName = $"support-ticket-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.json";
            var path = $"{_folder}/{fileName}";
            var fileBytes = JsonSerializer.SerializeToUtf8Bytes(ticket, JsonOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("Dropbox-API-Arg", JsonSerializer.Serialize(new
            {
                path,
                mode = "add",
                autorename = false,
                mute = true
            }));
            request.Content = new ByteArrayContent(fileBytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new SupportTicketDeliveryException($"Dropbox file upload failed with HTTP {(int)response.StatusCode}.");
        }
        catch (HttpRequestException exception)
        {
            throw new SupportTicketDeliveryException("Dropbox could not be reached while uploading the support ticket.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SupportTicketDeliveryException("Dropbox timed out while uploading the support ticket.", exception);
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = _options.RefreshToken,
            ["client_id"] = _options.AppKey,
            ["client_secret"] = _options.AppSecret
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new SupportTicketDeliveryException($"Dropbox OAuth token request failed with HTTP {(int)response.StatusCode}.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("access_token", out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new SupportTicketDeliveryException("Dropbox did not return an access token.");
        }

        return value.GetString()!;
    }
}
