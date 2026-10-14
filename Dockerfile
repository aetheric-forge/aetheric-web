# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN test -f AethericForge.Web/modules/parallel-you/module.json \
    && test -f AethericForge.Web/modules/library/module.json \
    && test -f AethericForge.Web/modules/archive/module.json \
    && test -f AethericForge.Web/modules/decisions/module.json
RUN dotnet restore AethericForge.Web/AethericForge.Web.csproj
RUN dotnet publish AethericForge.Web/AethericForge.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Trust the Aetheric Forge internal root CA so outbound calls to internal-only services (Keycloak,
# MinIO/S3-dev, etc.) don't fail with "PartialChain" - .NET on Linux uses the system OpenSSL trust
# store, so this covers HttpClient/SslStream the same way update-ca-certificates does for curl/etc.
COPY docker/certs/aetheric-forge-ca.crt /usr/local/share/ca-certificates/aetheric-forge-ca.crt
RUN update-ca-certificates

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "AethericForge.Web.dll"]