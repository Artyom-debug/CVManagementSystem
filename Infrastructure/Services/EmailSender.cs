using Application.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;

namespace Infrastructure.Services;

public sealed class EmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly UserCredential _credential;
    private readonly string _senderEmail;
    private readonly string _confirmationUrl;

    public EmailSender(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _senderEmail = GetRequiredSetting(configuration, "GoogleGmail:SenderEmail");
        _confirmationUrl = GetRequiredSetting(configuration, "GoogleGmail:ConfirmationUrl");

        var clientId = GetRequiredSetting(configuration, "GoogleGmail:ClientId");
        var clientSecret = GetRequiredSetting(configuration, "GoogleGmail:ClientSecret");
        var refreshToken = GetRequiredSetting(configuration, "GoogleGmail:RefreshToken");

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            }
        });

        _credential = new UserCredential(flow, _senderEmail, new TokenResponse
        {
            RefreshToken = refreshToken
        });
    }

    public async Task SendConfirmationEmailAsync(string toEmail, string confirmationToken, CancellationToken cancellationToken)
    {
        var recipient = new MailAddress(toEmail).Address;
        var sender = new MailAddress(_senderEmail).Address;
        var confirmationLink = QueryHelpers.AddQueryString(_confirmationUrl, "token", confirmationToken);

        if (!Uri.TryCreate(confirmationLink, UriKind.Absolute, out var link) || link.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("Email confirmation URL is invalid.");

        var accessToken = await _credential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Google did not return an access token.");

        var encodedLink = HtmlEncoder.Default.Encode(link.AbsoluteUri);
        var rawMime =
            $"To: {recipient}\r\n" +
            $"From: {sender}\r\n" +
            "Subject: Confirm your email address\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n\r\n" +
            "<h1>Email confirmation</h1>" +
            $"<p><a href=\"{encodedLink}\">Confirm email address</a></p>";

        var payload = new
        {
            raw = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawMime))
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"Gmail API returned {(int)response.StatusCode}: {error}", null, response.StatusCode);
    }

    private static string GetRequiredSetting(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Configuration value '{key}' was not provided.");
    }
}
