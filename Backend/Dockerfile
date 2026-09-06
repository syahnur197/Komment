FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Backend.csproj shells out to npm for the Tailwind build, so the SDK image needs
# node. ponytail: copied from the node image beats an apt/nodesource dance and
# pins the version in one place.
COPY --from=node:22-bookworm-slim /usr/local/bin/node /usr/local/bin/node
COPY --from=node:22-bookworm-slim /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm

WORKDIR /src
COPY global.json Backend.csproj package.json package-lock.json ./
# npm ci here so the layer caches; the NpmInstall target then sees node_modules
# already up to date and skips.
RUN dotnet restore && npm ci
COPY . .
RUN dotnet publish -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Npgsql dlopens GSSAPI. Without it every boot logs a scary but harmless
# "libgssapi_krb5.so.2: cannot open shared object file".
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

# DATAPROTECTION_KEYS mounts a named volume here in prod. Docker copies this
# directory's ownership onto an empty volume, so it must exist and belong to app
# before the switch below — otherwise the keyring is unwritable and every reader
# and admin is signed out on each restart.
RUN mkdir -p /keys && chown app:app /keys

# The image ships an unprivileged `app` user but does not select it. Nothing here
# needs root: the port is 8080, and /app is only ever read.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend.dll"]
