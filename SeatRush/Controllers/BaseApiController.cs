using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SeatRush.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        return User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is { } sub
               && Guid.TryParse(sub, out userId)
               && userId != Guid.Empty;
    }

    protected ActionResult MissingUser() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Missing or invalid access token.");
}
