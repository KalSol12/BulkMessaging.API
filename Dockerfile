FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["BulkMessaging.API.csproj", "./"]
RUN dotnet restore "BulkMessaging.API.csproj"
COPY . .
RUN dotnet publish "BulkMessaging.API.csproj" --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 10000
ENTRYPOINT ["sh", "-c", "dotnet BulkMessaging.API.dll --urls http://0.0.0.0:${PORT:-10000}"]
