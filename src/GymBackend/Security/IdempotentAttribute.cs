using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GymBackend.Data;
using GymBackend.DTOs;
using GymBackend.Extensions;
using GymBackend.Models;
using GymBackend.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute(bool required) : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var ct = http.RequestAborted;
        var values = http.Request.Headers["Idempotency-Key"];
        var key = values.ToString();
        if (values.Count == 0 && !required) { await next(); return; }
        if (values.Count != 1 || string.IsNullOrWhiteSpace(key) || key.Length > 200 || key.Any(char.IsControl))
        {
            context.Result = new BadRequestObjectResult(new MessageResponse("Debe proporcionar una cabecera Idempotency-Key válida."));
            return;
        }
        if (http.User.GetUserId() is not { } userId) { context.Result = new UnauthorizedResult(); return; }

        http.Request.EnableBuffering();
        string hash;
        try
        {
            using var document = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: ct);
            hash = RequestHash(document.RootElement);
        }
        catch (JsonException)
        {
            context.Result = new BadRequestObjectResult(new MessageResponse("El cuerpo JSON no es válido."));
            return;
        }
        finally { http.Request.Body.Position = 0; }

        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var clock = http.RequestServices.GetRequiredService<IGymClock>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Stable lock per (operator, key), distinct from the global occupancy lock.
        var lockBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"idempotency:{userId}:{key}"));
        var lockId = BinaryPrimitives.ReadInt64BigEndian(lockBytes);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);
        var cutoff = clock.UtcNow.AddHours(-24);
        // Opportunistic retention; never removes a record less than 24 hours old.
        await db.IdempotencyRecords.Where(r => r.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
        var existing = await db.IdempotencyRecords.SingleOrDefaultAsync(r => r.UserId == userId && r.Key == key, ct);
        if (existing is not null)
        {
            context.Result = existing.RequestHash != hash || existing.Method != http.Request.Method || existing.Path != http.Request.Path.Value
                ? new UnprocessableEntityObjectResult(new MessageResponse("La clave de idempotencia ya se usó con otra solicitud."))
                : new ContentResult { StatusCode = existing.StatusCode, ContentType = "application/json; charset=utf-8", Content = existing.ResponseBody };
            await transaction.CommitAsync(ct);
            return;
        }

        var output = http.Response.Body;
        await using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        try
        {
            var executed = await next();
            if (executed.Exception is not null && !executed.ExceptionHandled) return;
            // Server failures are retryable and must not commit partial business changes.
            if (http.Response.StatusCode >= 500)
            {
                await transaction.RollbackAsync(ct);
                buffer.Position = 0;
                await buffer.CopyToAsync(output, ct);
                return;
            }
            buffer.Position = 0;
            var body = await new StreamReader(buffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync(ct);
            db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = key,
                UserId = userId,
                Method = http.Request.Method,
                Path = http.Request.Path.Value!,
                RequestHash = hash,
                StatusCode = http.Response.StatusCode,
                ResponseBody = body,
                CreatedAt = clock.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            buffer.Position = 0;
            await buffer.CopyToAsync(output, ct);
        }
        finally { http.Response.Body = output; }
    }

    public static string RequestHash(JsonElement body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteNormalized(writer, body);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteNormalized(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteNormalized(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteNormalized(writer, item);
                writer.WriteEndArray();
                break;
            default: value.WriteTo(writer); break;
        }
    }
}
