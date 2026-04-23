FROM node:20-alpine AS frontend-build
WORKDIR /src/frontend

COPY frontend/package.json frontend/pnpm-lock.yaml ./
RUN corepack enable && pnpm install --frozen-lockfile

COPY frontend/ ./
RUN pnpm build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-publish
WORKDIR /src

COPY backend/ImageRelay.sln ./backend/ImageRelay.sln
COPY backend/ImageRelay.Api/ImageRelay.Api.csproj ./backend/ImageRelay.Api/ImageRelay.Api.csproj
RUN dotnet restore ./backend/ImageRelay.sln

COPY backend/ ./backend/
RUN dotnet publish ./backend/ImageRelay.Api/ImageRelay.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=backend-publish /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot/

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "ImageRelay.Api.dll"]
