FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY src/Domain/PainelEstetica.Domain.csproj ./src/Domain/
COPY src/Application/PainelEstetica.Application.csproj ./src/Application/
COPY src/Infrastructure/PainelEstetica.Infrastructure.csproj ./src/Infrastructure/
COPY src/WebAPI/WebAPI.csproj ./src/WebAPI/
RUN dotnet restore ./src/WebAPI/WebAPI.csproj --configfile nuget.config

COPY src/ ./src/
RUN dotnet publish ./src/WebAPI/WebAPI.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/storage /app/data-protection-keys \
    && chown -R ${APP_UID}:${APP_UID} /app/storage /app/data-protection-keys

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
USER $APP_UID
ENTRYPOINT ["dotnet", "WebAPI.dll"]
