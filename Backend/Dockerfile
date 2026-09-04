FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Backend.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# ponytail: no migration step — run `dotnet ef database update` against the
# mounted volume, or add db.Database.Migrate() at startup if that gets old.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend.dll"]
