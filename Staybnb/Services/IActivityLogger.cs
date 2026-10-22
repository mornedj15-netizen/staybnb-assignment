using Staybnb.Models;

namespace Staybnb.Services;

public interface IActivityLogger
{
    Task LogAsync(string? userId, string action, ActivityType type);
}