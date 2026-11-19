using GymBackend.Application.Excel;
using GymBackend.Domain;
using GymBackend.Extensions;
using GymBackend.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[RequestValidation]
public sealed class UserImportsController(UserImportService service) : ControllerBase
{
    [HttpPost("api/v1/admin/user-imports")]
    [RequestSizeLimit(UserImportService.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = UserImportService.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Preview([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > UserImportService.MaxBytes || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Se requiere un archivo .xlsx de hasta 2 MB." });
        using var stream = new MemoryStream();
        await using var source = file.OpenReadStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await source.ReadAsync(buffer, ct)) > 0)
        {
            if (stream.Length + count > UserImportService.MaxBytes) return BadRequest(new { message = "El archivo supera los 2 MB." });
            await stream.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        // ZIP signature plus successful ClosedXML parsing; extension/content-type alone are insufficient.
        if (stream.Length < 4 || stream.GetBuffer()[0] != 'P' || stream.GetBuffer()[1] != 'K')
            return BadRequest(new { message = "El archivo no es un .xlsx válido." });
        stream.Position = 0;
        try { return Ok(await service.PreviewAsync(stream, User.GetUserId()!.Value, ct)); }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or System.Xml.XmlException
            or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException or FormatException)
        { return BadRequest(new { message = "El archivo no es válido o supera los límites de importación." }); }
    }

    [HttpPost("api/v1/admin/user-imports/{id:int}/confirm")]
    [Idempotent(false)]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct) =>
        this.ToActionResult(await service.ConfirmAsync(id, User.GetUserId()!.Value, ct));
}
