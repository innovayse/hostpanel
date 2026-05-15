# ── Dev target ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dev

RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

WORKDIR /app

# Restore dependencies (cached layer)
COPY backend/Innovayse.Backend.sln ./Innovayse.Backend.sln
COPY backend/Directory.Build.props ./Directory.Build.props
COPY backend/src/Directory.Build.props ./src/Directory.Build.props
COPY backend/src/Innovayse.Domain/Innovayse.Domain.csproj ./src/Innovayse.Domain/
COPY backend/src/Innovayse.Application/Innovayse.Application.csproj ./src/Innovayse.Application/
COPY backend/src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj ./src/Innovayse.Infrastructure/
COPY backend/src/Innovayse.API/Innovayse.API.csproj ./src/Innovayse.API/
COPY backend/src/Innovayse.SDK/Innovayse.SDK.csproj ./src/Innovayse.SDK/
COPY backend/src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj ./src/Innovayse.Providers.CWP/
COPY backend/tests/Innovayse.Domain.Tests/Innovayse.Domain.Tests.csproj ./tests/Innovayse.Domain.Tests/
COPY backend/tests/Innovayse.Application.Tests/Innovayse.Application.Tests.csproj ./tests/Innovayse.Application.Tests/
COPY backend/tests/Innovayse.Integration.Tests/Innovayse.Integration.Tests.csproj ./tests/Innovayse.Integration.Tests/
COPY backend/tests/Innovayse.CWP.Tests/Innovayse.CWP.Tests.csproj ./tests/Innovayse.CWP.Tests/
RUN dotnet restore Innovayse.Backend.sln

ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 5148

ENTRYPOINT ["dotnet", "watch", "run", \
    "--project", "src/Innovayse.API/Innovayse.API.csproj", \
    "--no-launch-profile", \
    "--", "--urls", "http://0.0.0.0:5148"]

# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY backend/Innovayse.Backend.sln ./Innovayse.Backend.sln
COPY backend/Directory.Build.props ./Directory.Build.props
COPY backend/src/Directory.Build.props ./src/Directory.Build.props
COPY backend/src/Innovayse.Domain/Innovayse.Domain.csproj ./src/Innovayse.Domain/
COPY backend/src/Innovayse.Application/Innovayse.Application.csproj ./src/Innovayse.Application/
COPY backend/src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj ./src/Innovayse.Infrastructure/
COPY backend/src/Innovayse.API/Innovayse.API.csproj ./src/Innovayse.API/
COPY backend/src/Innovayse.SDK/Innovayse.SDK.csproj ./src/Innovayse.SDK/
COPY backend/src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj ./src/Innovayse.Providers.CWP/
COPY backend/tests/Innovayse.Domain.Tests/Innovayse.Domain.Tests.csproj ./tests/Innovayse.Domain.Tests/
COPY backend/tests/Innovayse.Application.Tests/Innovayse.Application.Tests.csproj ./tests/Innovayse.Application.Tests/
COPY backend/tests/Innovayse.Integration.Tests/Innovayse.Integration.Tests.csproj ./tests/Innovayse.Integration.Tests/
COPY backend/tests/Innovayse.CWP.Tests/Innovayse.CWP.Tests.csproj ./tests/Innovayse.CWP.Tests/
RUN dotnet restore Innovayse.Backend.sln

COPY backend/ .
RUN dotnet publish src/Innovayse.API/Innovayse.API.csproj -c Release -o /out --no-restore

# ── Prod target ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS prod

WORKDIR /app
COPY --from=build /out .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5148

ENTRYPOINT ["dotnet", "Innovayse.API.dll", "--urls", "http://0.0.0.0:5148"]
