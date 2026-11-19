using System.Text.Json;
using GymBackend.Security;

namespace GymBackend.UnitTests.Idempotency;

public sealed class RequestHashTests
{
    [Fact]
    public void Hash_NormalizesWhitespaceAndNestedPropertyOrder()
    {
        using var first = JsonDocument.Parse("{\"a\":1,\"nested\":{\"x\":2,\"y\":3}}");
        using var reordered = JsonDocument.Parse("{ \"nested\": {\"y\":3, \"x\":2}, \"a\": 1 }");
        using var changed = JsonDocument.Parse("{\"a\":2,\"nested\":{\"x\":2,\"y\":3}}");
        Assert.Equal(IdempotentAttribute.RequestHash(first.RootElement), IdempotentAttribute.RequestHash(reordered.RootElement));
        Assert.NotEqual(IdempotentAttribute.RequestHash(first.RootElement), IdempotentAttribute.RequestHash(changed.RootElement));
    }
}
