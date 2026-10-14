using Microsoft.AspNetCore.Mvc;
using SnG.Control.Application.Identity;

namespace SnG.Control.Api.Controllers;

public sealed record CurrentUserResponse(string Subject, string? Email, string? Name);

[ApiController]
[Route("api/auth")]
public class AuthController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> Me() => Ok(new CurrentUserResponse(currentUser.Subject, currentUser.Email, currentUser.Name));
}
