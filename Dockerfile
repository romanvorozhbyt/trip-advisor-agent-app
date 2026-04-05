FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/TripAdvisorAgent.Core/TripAdvisorAgent.Core.csproj src/TripAdvisorAgent.Core/
COPY src/TripAdvisorAgent.Infrastructure/TripAdvisorAgent.Infrastructure.csproj src/TripAdvisorAgent.Infrastructure/
COPY src/TripAdvisorAgent.WebApi/TripAdvisorAgent.WebApi.csproj src/TripAdvisorAgent.WebApi/
RUN dotnet restore src/TripAdvisorAgent.WebApi/TripAdvisorAgent.WebApi.csproj

COPY src/ src/
RUN dotnet publish src/TripAdvisorAgent.WebApi/TripAdvisorAgent.WebApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TripAdvisorAgent.WebApi.dll"]
