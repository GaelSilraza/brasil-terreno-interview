FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN --mount=type=cache,id=finance-nuget,target=/root/.nuget/packages \
    dotnet restore src/Hosts/Finance.Api/Finance.Api.csproj \
    && dotnet restore src/Hosts/Finance.Worker/Finance.Worker.csproj \
    && dotnet restore src/Hosts/Finance.Migrator/Finance.Migrator.csproj
RUN --mount=type=cache,id=finance-nuget,target=/root/.nuget/packages \
    dotnet publish src/Hosts/Finance.Api/Finance.Api.csproj -c Release -o /app/api --no-restore \
    && dotnet publish src/Hosts/Finance.Worker/Finance.Worker.csproj -c Release -o /app/worker --no-restore \
    && dotnet publish src/Hosts/Finance.Migrator/Finance.Migrator.csproj -c Release -o /app/migrator --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

FROM runtime AS api
WORKDIR /app
COPY --from=build /app/api .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Finance.Api.dll"]

FROM runtime AS worker
WORKDIR /app
COPY --from=build /app/worker .
USER $APP_UID
ENTRYPOINT ["dotnet", "Finance.Worker.dll"]

FROM runtime AS migrator
WORKDIR /app
COPY --from=build /app/migrator .
USER $APP_UID
ENTRYPOINT ["dotnet", "Finance.Migrator.dll"]
