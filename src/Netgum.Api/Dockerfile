FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["src/Netgum.Domain/Netgum.Domain.csproj", "src/Netgum.Domain/"]
COPY ["src/Netgum.Application/Netgum.Application.csproj", "src/Netgum.Application/"]
COPY ["src/Netgum.Infrastructure/Netgum.Infrastructure.csproj", "src/Netgum.Infrastructure/"]
COPY ["src/Netgum.Api/Netgum.Api.csproj", "src/Netgum.Api/"]
RUN dotnet restore "src/Netgum.Api/Netgum.Api.csproj"
COPY . .
RUN dotnet publish "src/Netgum.Api/Netgum.Api.csproj" -c Release -o /app/publish --no-restore

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Netgum.Api.dll"]
