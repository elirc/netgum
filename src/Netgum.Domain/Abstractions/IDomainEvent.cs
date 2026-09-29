namespace Netgum.Domain.Abstractions;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
