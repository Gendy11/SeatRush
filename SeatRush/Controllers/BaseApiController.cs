using Microsoft.AspNetCore.Mvc;

namespace SeatRush.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    // TEMPORARY identity: the client sends a GUID in X-User-Id.
    // Replaced by a minimal JWT / anonymous session in the auth step; callers only use TryGetUserId.
    private const string UserIdHeader = "X-User-Id";

    protected bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        return Request.Headers.TryGetValue(UserIdHeader, out var value)
               && Guid.TryParse(value, out userId)
               && userId != Guid.Empty;
    }

    protected ActionResult MissingUser() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: $"Missing or invalid {UserIdHeader} header.");
}
