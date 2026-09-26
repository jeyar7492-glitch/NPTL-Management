using Microsoft.AspNetCore.Mvc;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Storage;

namespace NPTELManagement.Api.Controllers;

[ApiController]
[Route("api/v1/certificates")]
public class CertificatesController : ControllerBase
{
    [HttpGet("mock-download")]
    public IActionResult MockDownload(
        [FromQuery] string token,
        [FromServices] IPrivateCloudStorageService storageService,
        [FromServices] IHostEnvironment environment)
    {
        // This endpoint exists only for local Development + in-memory demo storage.
        if (!environment.IsDevelopment() || storageService is not OfflineMockStorageService mock)
        {
            return NotFound();
        }

        if (!mock.TryGetObjectForToken(token, out var bytes))
        {
            return NotFound();
        }

        return File(bytes, "application/pdf", "NPTEL-Certificate.pdf");
    }
}
