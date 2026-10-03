# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TishtryaCMS.slnx ./
COPY src/BuildingBlocks/TishtryaCMS.SharedKernel/TishtryaCMS.SharedKernel.csproj \
    src/BuildingBlocks/TishtryaCMS.SharedKernel/
COPY src/Host/TishtryaCMS.Api/TishtryaCMS.Api.csproj \
    src/Host/TishtryaCMS.Api/
COPY src/Modules/Identity/TishtryaCMS.Modules.Identity/TishtryaCMS.Modules.Identity.csproj \
    src/Modules/Identity/TishtryaCMS.Modules.Identity/
COPY src/Modules/ContentModules/TishtryaCMS.Modules.ContentModules/TishtryaCMS.Modules.ContentModules.csproj \
    src/Modules/ContentModules/TishtryaCMS.Modules.ContentModules/
COPY src/Modules/FileManager/TishtryaCMS.Modules.FileManager/TishtryaCMS.Modules.FileManager.csproj \
    src/Modules/FileManager/TishtryaCMS.Modules.FileManager/
COPY src/Modules/EditorTrya/TishtryaCMS.Modules.EditorTrya/TishtryaCMS.Modules.EditorTrya.csproj \
    src/Modules/EditorTrya/TishtryaCMS.Modules.EditorTrya/
COPY src/Modules/Qa/TishtryaCMS.Modules.Qa/TishtryaCMS.Modules.Qa.csproj \
    src/Modules/Qa/TishtryaCMS.Modules.Qa/
COPY src/Modules/Comments/TishtryaCMS.Modules.Comments/TishtryaCMS.Modules.Comments.csproj \
    src/Modules/Comments/TishtryaCMS.Modules.Comments/
COPY src/Modules/Settings/TishtryaCMS.Modules.Settings/TishtryaCMS.Modules.Settings.csproj \
    src/Modules/Settings/TishtryaCMS.Modules.Settings/
COPY src/Modules/Forms/TishtryaCMS.Modules.Forms/TishtryaCMS.Modules.Forms.csproj \
    src/Modules/Forms/TishtryaCMS.Modules.Forms/
COPY src/Modules/Tickets/TishtryaCMS.Modules.Tickets/TishtryaCMS.Modules.Tickets.csproj \
    src/Modules/Tickets/TishtryaCMS.Modules.Tickets/

RUN dotnet restore src/Host/TishtryaCMS.Api/TishtryaCMS.Api.csproj

COPY src ./src

RUN dotnet publish src/Host/TishtryaCMS.Api/TishtryaCMS.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/wwwroot/uploads

COPY --from=build /app/publish .

VOLUME ["/app/wwwroot/uploads"]

ENTRYPOINT ["dotnet", "TishtryaCMS.Api.dll"]
