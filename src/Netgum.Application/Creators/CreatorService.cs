using Netgum.Application.Abstractions;
using Netgum.Domain.Abstractions;
using Netgum.Domain.Creators;
using Netgum.Domain.ValueObjects;

namespace Netgum.Application.Creators;

public sealed class CreatorService
{
    private readonly ICreatorRepository _creators;
    private readonly IUnitOfWork _unitOfWork;

    public CreatorService(ICreatorRepository creators, IUnitOfWork unitOfWork)
    {
        _creators = creators;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreatorResponse> RegisterAsync(RegisterCreatorRequest request, CancellationToken cancellationToken)
    {
        var slug = Slug.Create(request.Slug);
        if (await _creators.SlugExistsAsync(slug, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainException("Creator slug is already taken.");
        }

        var creator = Creator.Register(request.DisplayName, slug.Value, request.SupportEmail);
        _creators.Add(creator);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatorMapper.ToResponse(creator);
    }
}

internal static class CreatorMapper
{
    public static CreatorResponse ToResponse(Creator creator) =>
        new(
            creator.Id,
            creator.DisplayName,
            creator.Slug.Value,
            creator.SupportEmail.Value,
            creator.Bio,
            creator.AvatarUrl);
}
