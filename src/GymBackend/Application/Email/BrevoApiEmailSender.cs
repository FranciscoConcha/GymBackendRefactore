using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymBackend.Models;
using GymBackend.Options;
using Microsoft.Extensions.Options;

namespace GymBackend.Application.Email;

public sealed class BrevoApiEmailSender(IHttpClientFactory clients, IOptions<EmailOptions> options)
{
    public async Task SendAsync(EmailOutbox email, CancellationToken ct = default)
    {
        try
        {
            var settings = options.Value;
            var sender = new Dictionary<string, string> { ["email"] = settings.From };
            if (!string.IsNullOrWhiteSpace(settings.FromName)) sender["name"] = settings.FromName;
            using var client = clients.CreateClient("brevo");
            using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");
            request.Headers.Add("api-key", settings.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = JsonContent.Create(new
            {
                sender,
                to = new[] { new { email = email.To } },
                subject = email.Subject,
                textContent = email.Body,
            });
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode != HttpStatusCode.Created)
                throw new InvalidOperationException("No se pudo enviar el correo.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (Exception)
        {
            // Never propagate provider content, request headers, or transport exception details.
            throw new InvalidOperationException("No se pudo enviar el correo.");
        }
    }
}
