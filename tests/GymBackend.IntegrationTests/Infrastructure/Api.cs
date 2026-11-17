using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GymBackend.IntegrationTests.Infrastructure;

internal static class Api
{
    internal static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static async Task<JsonElement> Read(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, Ct);

    internal static async Task<JsonElement> Expect(HttpResponseMessage response, HttpStatusCode status, string? message = null)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            var json = await Read(response);
            if (message is not null) Assert.Equal(message, json.GetProperty("message").GetString());
            return json;
        }
    }

    internal static Task<HttpResponseMessage> Reserve(HttpClient client, int slotId, DateOnly date) =>
        client.PostAsJsonAsync("/api/reservations", new { scheduleSlotId = slotId, date }, JsonOptions, Ct);

    internal static Task<HttpResponseMessage> Status(HttpClient client, int id, bool enabled) =>
        client.PatchAsJsonAsync($"/api/auth/users/{id}/status", new { enabled }, JsonOptions, Ct);

    internal static async Task<JsonElement> Reservation(HttpClient client, int slotId, DateOnly date) =>
        (await Expect(await Reserve(client, slotId, date), HttpStatusCode.OK, "Reserva creada correctamente.")).GetProperty("reservation");

    internal static async Task<string> Qr(HttpClient client) =>
        (await Expect(await client.GetAsync("/api/qr/my-code", Ct), HttpStatusCode.OK)).GetProperty("token").GetString()!;

    internal static Task<HttpResponseMessage> Attendance(HttpClient client, string action, string token) =>
        client.PostAsJsonAsync($"/api/attendance/{action}", new { token }, JsonOptions, Ct);
}
