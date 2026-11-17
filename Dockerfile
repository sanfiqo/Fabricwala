# TextileConnect API image. Build from the repository root:  docker build -t textileconnect-api .
# It has not been built on the machine where it was written (no Docker there): the first build is its first test.

# ---- build and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# copy the project files first so that the package restore is cached until a package changes
COPY TextileConnect.slnx ./
COPY src/Sanfiqo.Identity/Sanfiqo.Identity.csproj src/Sanfiqo.Identity/
COPY src/TextileConnect.Domain/TextileConnect.Domain.csproj src/TextileConnect.Domain/
COPY src/TextileConnect.Application/TextileConnect.Application.csproj src/TextileConnect.Application/
COPY src/TextileConnect.Infrastructure/TextileConnect.Infrastructure.csproj src/TextileConnect.Infrastructure/
COPY src/TextileConnect.Api/TextileConnect.Api.csproj src/TextileConnect.Api/
RUN dotnet restore src/TextileConnect.Api/TextileConnect.Api.csproj
COPY src/ src/
RUN dotnet publish src/TextileConnect.Api/TextileConnect.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---- run
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# The image runs as an ordinary user, not root. Files that the app writes (only the Development file storage does) go to /data.
RUN mkdir -p /data && chown -R app:app /data /app
USER app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
EXPOSE 8080
# No HEALTHCHECK here: the image has no curl. Point the orchestrator at /api/v1/health/live (restart) and /api/v1/health/ready (traffic), see docs/DEPLOY.md.
ENTRYPOINT ["dotnet", "TextileConnect.Api.dll"]
