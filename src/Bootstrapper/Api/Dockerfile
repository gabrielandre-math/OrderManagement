FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080

# Install ICU libs for globalization support (i18n: en-US, pt-BR, es)
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first for better layer caching
COPY src/Bootstrapper/Api/Api.csproj src/Bootstrapper/Api/
COPY src/Modules/Catalog/Catalog/Catalog.csproj src/Modules/Catalog/Catalog/
COPY src/Modules/Basket/Basket/Basket.csproj src/Modules/Basket/Basket/
COPY src/Modules/Orders/Orders/Orders.csproj src/Modules/Orders/Orders/
COPY src/Shared/Shared/Shared.csproj src/Shared/Shared/
COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
COPY src/Shared/Shared.Messaging/Shared.Messaging.csproj src/Shared/Shared.Messaging/

RUN dotnet restore src/Bootstrapper/Api/Api.csproj /p:BuildingInsideDocker=true

# Copy everything else and build
COPY src/ src/
WORKDIR /src/src/Bootstrapper/Api
RUN dotnet build Api.csproj -c $BUILD_CONFIGURATION -o /app/build --no-restore /p:BuildingInsideDocker=true

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish Api.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore /p:BuildingInsideDocker=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
