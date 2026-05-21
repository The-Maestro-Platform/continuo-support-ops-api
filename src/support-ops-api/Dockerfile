# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["services/support-ops-api/support-ops-api.csproj", "services/support-ops-api/"]
RUN dotnet restore "services/support-ops-api/support-ops-api.csproj"
COPY . ./
WORKDIR "/src/services/support-ops-api"
RUN dotnet publish "support-ops-api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "support-ops-api.dll"]
