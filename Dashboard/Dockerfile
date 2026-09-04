FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Dashboard.csproj shells out to npm for the Tailwind/Vite build, so the SDK
# image needs node. ponytail: copied from the node image beats an apt/nodesource
# dance and pins the version in one place.
COPY --from=node:22-bookworm-slim /usr/local/bin/node /usr/local/bin/node
COPY --from=node:22-bookworm-slim /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm

WORKDIR /src
COPY Dashboard.csproj package.json package-lock.json ./
# npm ci here so the layer caches; the NpmInstall target sees node_modules
# already up to date and skips.
RUN dotnet restore && npm ci
COPY . .
RUN dotnet publish -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Dashboard.dll"]
