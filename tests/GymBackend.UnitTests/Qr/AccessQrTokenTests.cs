using System.Security.Cryptography;
using System.Text;
using GymBackend.Application.Qr;
using Microsoft.AspNetCore.WebUtilities;

namespace GymBackend.UnitTests.Qr;

public sealed class AccessQrTokenTests
{
    private const string Key = "qr-signing-key-at-least-32-characters";
    private static readonly AccessQrPayload Payload = new(2, "R", 12, null, 34, 1794915900, 1794920400);

    [Theory]
    [InlineData("R")]
    [InlineData("G")]
    public void RoundTrip(string kind)
    {
        var original = Payload with { Kind = kind, ReservationId = kind == "R" ? 12 : null, GroupBookingId = kind == "G" ? 56 : null };
        Assert.True(AccessQrToken.TryRead(AccessQrToken.Create(original, Key), Key, out var parsed));
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void AlteredSignatureAndPayloadAreRejected()
    {
        var parts = AccessQrToken.Create(Payload, Key).Split('.');
        var signature = WebEncoders.Base64UrlDecode(parts[1]);
        signature[0] ^= 1;
        Assert.False(AccessQrToken.TryRead(parts[0] + "." + WebEncoders.Base64UrlEncode(signature), Key, out _));
        var changed = AccessQrToken.Create(Payload with { UserId = 99 }, Key).Split('.')[0];
        Assert.False(AccessQrToken.TryRead(changed + "." + parts[1], Key, out _));
    }

    [Fact]
    public void JwtSigningKeyIsRejected() =>
        Assert.False(AccessQrToken.TryRead(AccessQrToken.Create(Payload, "jwt-signing-key-at-least-32-characters"), Key, out _));

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(4499, true)]
    [InlineData(4500, false)]
    public void WindowHasInclusiveStartAndExclusiveEnd(int seconds, bool expected)
    {
        Assert.True(AccessQrToken.TryRead(AccessQrToken.Create(Payload, Key), Key, out var parsed));
        Assert.Equal(expected, parsed.IsCurrent(DateTimeOffset.FromUnixTimeSeconds(Payload.NotBefore + seconds)));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{}""")]
    [InlineData("null")]
    [InlineData("""{"v":3,"k":"R","rid":12,"uid":34,"nbf":100,"exp":200}""")]
    [InlineData("""{"v":2,"k":"G","rid":12,"uid":34,"nbf":100,"exp":200}""")]
    [InlineData("""{"v":2,"k":"R","rid":12,"uid":34,"exp":200}""")]
    public void InvalidSignedJsonIsRejected(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var token = WebEncoders.Base64UrlEncode(bytes) + "." + WebEncoders.Base64UrlEncode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key), bytes));
        Assert.False(AccessQrToken.TryRead(token, Key, out _));
    }

    [Fact]
    public void GroupTokenCannotBeUsedAsReservation()
    {
        var group = Payload with { Kind = "G", ReservationId = null, GroupBookingId = 12 };
        var parts = AccessQrToken.Create(group, Key).Split('.');
        var json = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(parts[0]));
        var changed = json.Replace("\"k\":\"G\"", "\"k\":\"R\"", StringComparison.Ordinal);
        Assert.False(AccessQrToken.TryRead(WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(changed)) + "." + parts[1], Key, out _));
        Assert.False(AccessQrToken.TryRead(AccessQrToken.Create(group with { Kind = "R" }, Key), Key, out _));
    }

    [Theory]
    [InlineData("v")]
    [InlineData("k")]
    [InlineData("rid")]
    [InlineData("uid")]
    [InlineData("nbf")]
    [InlineData("exp")]
    public void EveryPayloadFieldIsRequired(string field)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(Payload))!.AsObject();
        Assert.True(json.Remove(field));
        InvalidSignedJsonIsRejected(json.ToJsonString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("%%%.abc")]
    [InlineData("abc")]
    public void MalformedTokensAreRejected(string? token) => Assert.False(AccessQrToken.TryRead(token, Key, out _));
}
