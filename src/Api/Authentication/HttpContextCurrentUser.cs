using SnG.Control.Application.Identity;

namespace SnG.Control.Api.Authentication;

public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string Subject
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var subject = principal?.FindFirst("sub")?.Value;
            if (principal?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(subject))
                throw new InvalidOperationException("An authenticated identity with a subject is required.");
            return subject;
        }
    }
    public string? Email => accessor.HttpContext?.User.FindFirst("email")?.Value;
    public string? Name => accessor.HttpContext?.User.FindFirst("name")?.Value;
}
