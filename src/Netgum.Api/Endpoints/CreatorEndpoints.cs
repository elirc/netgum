using Netgum.Application.Creators;

namespace Netgum.Api.Endpoints;

public static class CreatorEndpoints
{
    public static IEndpointRouteBuilder MapCreatorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/creators").WithTags("Creators");

        group.MapPost("/", async (
            RegisterCreatorRequest request,
            CreatorService service,
            CancellationToken cancellationToken) =>
        {
            var creator = await service.RegisterAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Created($"/api/creators/{creator.Id}", creator);
        })
        .WithName("RegisterCreator")
        .WithOpenApi();

        return app;
    }
}
