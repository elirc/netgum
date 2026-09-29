namespace Netgum.Application.Creators;

public sealed record RegisterCreatorRequest(string DisplayName, string Slug, string SupportEmail);

public sealed record CreatorResponse(Guid Id, string DisplayName, string Slug, string SupportEmail, string? Bio, string? AvatarUrl);
