namespace SnG.Control.Application.Identity;

public interface ICurrentUser
{
    string Subject { get; }
    string? Email { get; }
    string? Name { get; }
}
