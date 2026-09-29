namespace Netgum.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
