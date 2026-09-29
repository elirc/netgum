using Netgum.Domain.Abstractions;

namespace Netgum.Domain.Orders;

public sealed record OrderPaidEvent(Guid OrderId, Guid ProductId, string CustomerEmail, DateTimeOffset OccurredAt) : IDomainEvent;
