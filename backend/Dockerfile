# Context: backend/ (compose: context ../backend). Targets: dev (dotnet watch), build, prod.
FROM mcr.microsoft.com/dotnet/sdk:9.0.320@sha256:d7cfd59385751fc87897a138ba173fb1bbc2bf946ea78aecc91a119993676997 AS dev

RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

WORKDIR /app

# Without nuget.config the restore sees nuget.org only and fails NU1101 for Innovayse.Auth.
COPY nuget.config ./nuget.config

COPY Innovayse.Hostpanel.sln ./Innovayse.Hostpanel.sln
COPY Directory.Build.props ./Directory.Build.props
COPY Directory.Packages.props ./Directory.Packages.props
COPY src/Directory.Build.props ./src/Directory.Build.props
COPY src/SharedKernel/Innovayse.Hostpanel.SharedKernel.csproj ./src/SharedKernel/
COPY src/Shared/Innovayse.Hostpanel.Shared.csproj ./src/Shared/
COPY src/Innovayse.Hostpanel.Domain/Innovayse.Hostpanel.Domain.csproj ./src/Innovayse.Hostpanel.Domain/
COPY src/Innovayse.Hostpanel.Application/Innovayse.Hostpanel.Application.csproj ./src/Innovayse.Hostpanel.Application/
COPY src/Innovayse.Hostpanel.Infrastructure/Innovayse.Hostpanel.Infrastructure.csproj ./src/Innovayse.Hostpanel.Infrastructure/
COPY src/Innovayse.Hostpanel.API/Innovayse.Hostpanel.API.csproj ./src/Innovayse.Hostpanel.API/
COPY src/Innovayse.SDK/Innovayse.SDK.csproj ./src/Innovayse.SDK/
COPY src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj ./src/Innovayse.Providers.CWP/
COPY src/Innovayse.Providers.CWP7/Innovayse.Providers.CWP7.csproj ./src/Innovayse.Providers.CWP7/
COPY src/Innovayse.Providers.Inecobank/Innovayse.Providers.Inecobank.csproj ./src/Innovayse.Providers.Inecobank/
COPY tests/Innovayse.Hostpanel.Domain.Tests/Innovayse.Hostpanel.Domain.Tests.csproj ./tests/Innovayse.Hostpanel.Domain.Tests/
COPY tests/Innovayse.Hostpanel.Architecture.Tests/Innovayse.Hostpanel.Architecture.Tests.csproj ./tests/Innovayse.Hostpanel.Architecture.Tests/
COPY tests/Innovayse.Hostpanel.Integration.Tests/Innovayse.Hostpanel.Integration.Tests.csproj ./tests/Innovayse.Hostpanel.Integration.Tests/
COPY tests/Innovayse.CWP.Tests/Innovayse.CWP.Tests.csproj ./tests/Innovayse.CWP.Tests/
COPY tests/Innovayse.Inecobank.Tests/Innovayse.Inecobank.Tests.csproj ./tests/Innovayse.Inecobank.Tests/
# AUTH_MODE=sso builds with -p:InnovayseSso=true so a missing registry credential fails here, not at boot.
ARG AUTH_MODE

# The private feed credentials arrive as the BuildKit secret nuget_credentials (docker build --secret
# id=nuget_credentials,env=...): a build argument would stay readable in the image history. NuGet reads
# the credentials from the environment variable of this exact name; it exists for this one command only.
RUN --mount=type=secret,id=nuget_credentials,required=false \
    SSO=$([ "$AUTH_MODE" = "sso" ] && echo "-p:InnovayseSso=true"); \
    if [ -s /run/secrets/nuget_credentials ]; then export NuGetPackageSourceCredentials_innovayse="$(cat /run/secrets/nuget_credentials)"; fi; \
    dotnet restore Innovayse.Hostpanel.sln $SSO

# Documentation only; the listening port is ASPNETCORE_HTTP_PORTS (API_PORT) from compose.
EXPOSE 5148

ENTRYPOINT ["dotnet", "watch", "run", \
    "--project", "src/Innovayse.Hostpanel.API/Innovayse.Hostpanel.API.csproj", \
    "--no-launch-profile", \
    "--"]

FROM mcr.microsoft.com/dotnet/sdk:9.0.320@sha256:d7cfd59385751fc87897a138ba173fb1bbc2bf946ea78aecc91a119993676997 AS build

WORKDIR /src

COPY nuget.config ./nuget.config

COPY Innovayse.Hostpanel.sln ./Innovayse.Hostpanel.sln
COPY Directory.Build.props ./Directory.Build.props
COPY Directory.Packages.props ./Directory.Packages.props
COPY src/Directory.Build.props ./src/Directory.Build.props
COPY src/SharedKernel/Innovayse.Hostpanel.SharedKernel.csproj ./src/SharedKernel/
COPY src/Shared/Innovayse.Hostpanel.Shared.csproj ./src/Shared/
COPY src/Innovayse.Hostpanel.Domain/Innovayse.Hostpanel.Domain.csproj ./src/Innovayse.Hostpanel.Domain/
COPY src/Innovayse.Hostpanel.Application/Innovayse.Hostpanel.Application.csproj ./src/Innovayse.Hostpanel.Application/
COPY src/Innovayse.Hostpanel.Infrastructure/Innovayse.Hostpanel.Infrastructure.csproj ./src/Innovayse.Hostpanel.Infrastructure/
COPY src/Innovayse.Hostpanel.API/Innovayse.Hostpanel.API.csproj ./src/Innovayse.Hostpanel.API/
COPY src/Innovayse.SDK/Innovayse.SDK.csproj ./src/Innovayse.SDK/
COPY src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj ./src/Innovayse.Providers.CWP/
COPY src/Innovayse.Providers.CWP7/Innovayse.Providers.CWP7.csproj ./src/Innovayse.Providers.CWP7/
COPY src/Innovayse.Providers.Inecobank/Innovayse.Providers.Inecobank.csproj ./src/Innovayse.Providers.Inecobank/
COPY tests/Innovayse.Hostpanel.Domain.Tests/Innovayse.Hostpanel.Domain.Tests.csproj ./tests/Innovayse.Hostpanel.Domain.Tests/
COPY tests/Innovayse.Hostpanel.Architecture.Tests/Innovayse.Hostpanel.Architecture.Tests.csproj ./tests/Innovayse.Hostpanel.Architecture.Tests/
COPY tests/Innovayse.Hostpanel.Integration.Tests/Innovayse.Hostpanel.Integration.Tests.csproj ./tests/Innovayse.Hostpanel.Integration.Tests/
COPY tests/Innovayse.CWP.Tests/Innovayse.CWP.Tests.csproj ./tests/Innovayse.CWP.Tests/
COPY tests/Innovayse.Inecobank.Tests/Innovayse.Inecobank.Tests.csproj ./tests/Innovayse.Inecobank.Tests/
ARG AUTH_MODE

# The private feed credentials arrive as the BuildKit secret nuget_credentials (docker build --secret
# id=nuget_credentials,env=...): a build argument would stay readable in the image history. NuGet reads
# the credentials from the environment variable of this exact name; it exists for this one command only.
RUN --mount=type=secret,id=nuget_credentials,required=false \
    SSO=$([ "$AUTH_MODE" = "sso" ] && echo "-p:InnovayseSso=true"); \
    if [ -s /run/secrets/nuget_credentials ]; then export NuGetPackageSourceCredentials_innovayse="$(cat /run/secrets/nuget_credentials)"; fi; \
    dotnet restore Innovayse.Hostpanel.sln $SSO

COPY . .

# Wolverine generates the code of every message handler. Left to do it at runtime it compiles each handler
# on its first use, which costs tens of megabytes that stay resident, so the code is generated here and
# compiled into the published API, and a production host (TypeLoadMode.Static) loads it. The Testing
# environment is used only because its settings let the host be built with no database or secrets: the
# command builds the host and writes the code, it never starts it. Writing it fresh on every build is
# also what keeps it from going stale. The host is built in the auth mode the image runs in: the two
# modes register different account adapters, and code generated for one cannot resolve the other's
# (a sign-on image whose handlers were generated for local mode asks for Identity, which sso never
# registers). Sign-on mode refuses to build the host without its sign-on settings (this product's and
# the shared sign-on package's); the placeholders below only let it be built here, nothing is contacted
# because the host is never started, and the running container takes its own from the environment.
RUN SSO=$([ "$AUTH_MODE" = "sso" ] && echo "-p:InnovayseSso=true"); \
    cd src/Innovayse.Hostpanel.API && \
    ASPNETCORE_ENVIRONMENT=Testing Auth__Mode="${AUTH_MODE:-local}" \
    Sso__Authority=https://sign-on.invalid Sso__ClientId=codegen \
    Auth__AppName=codegen Auth__Authority=https://sign-on.invalid Auth__PublicAuthority=https://sign-on.invalid \
    Auth__ClientId=codegen Auth__ClientSecret=codegen Auth__RedisConnection=localhost:6379 \
    dotnet run -c Release --no-restore --no-launch-profile $SSO -- codegen write

ARG VERSION=0.0.0-dev

RUN SSO=$([ "$AUTH_MODE" = "sso" ] && echo "-p:InnovayseSso=true"); \
    dotnet publish src/Innovayse.Hostpanel.API/Innovayse.Hostpanel.API.csproj -c Release -o /out --no-restore \
        /p:ErrorOnDuplicatePublishOutputFiles=false \
        /p:Version=$VERSION \
        /p:InformationalVersion=$VERSION \
        $SSO

FROM mcr.microsoft.com/dotnet/aspnet:9.0.22-alpine3.24@sha256:c8e9a3262af499a08fd3911542af57a35c7a11819ba5db2a0a1658e47417d11b AS prod

# icu-data-full: the Alpine image is culture-invariant by default, and the API starts with en, ru and hy.
RUN apk add --no-cache icu-libs icu-data-full
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

WORKDIR /app
# The publish output carries migration-plugin/modules, the plugin for the source billing system that the
# admin download zips (MigrationPlugin:Path); the API project copies it there, so no stage copies it by hand.
COPY --from=build /out .

# The folders the API writes to belong to the unprivileged user. A named volume mounted on one of them
# starts with this ownership; a volume created by an earlier root image is chowned once by the operator.
RUN mkdir -p /app/wwwroot/uploads/branding /app/plugins-installed /app/logs \
    && chown -R $APP_UID /app/wwwroot/uploads /app/plugins-installed /app/logs

USER $APP_UID

# Documentation only; the listening port is ASPNETCORE_HTTP_PORTS (API_PORT) from compose.
EXPOSE 5148

# `localhost`, not 127.0.0.1: AllowedHosts lists names, so the host filter answers 400 to a literal loopback
# IP. A GET to /dev/null, not --spider: that sends HEAD, which the GET-only /health route refuses.
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD wget -q -O /dev/null "http://localhost:${ASPNETCORE_HTTP_PORTS}/health" || exit 1

ENTRYPOINT ["dotnet", "Innovayse.Hostpanel.API.dll"]
