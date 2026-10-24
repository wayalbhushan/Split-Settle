# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files to leverage Docker layer caching for restore
COPY SplitSettle.sln ./
COPY src/SplitSettle.Core/SplitSettle.Core.csproj src/SplitSettle.Core/
COPY src/SplitSettle.Data/SplitSettle.Data.csproj src/SplitSettle.Data/
COPY src/SplitSettle.Api/SplitSettle.Api.csproj src/SplitSettle.Api/

RUN dotnet restore src/SplitSettle.Api/SplitSettle.Api.csproj

# Copy application source
COPY src/ src/

# Publish release build
WORKDIR /src/src/SplitSettle.Api
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .

# Run container as non-root user
USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "SplitSettle.Api.dll"]
