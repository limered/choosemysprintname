# --- Stage 1: build the Vue frontend ---
FROM node:24-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# --- Stage 2: build & publish the .NET backend ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend
WORKDIR /src
COPY backend/Backend.csproj backend/
RUN dotnet restore backend/Backend.csproj
COPY backend/ backend/
RUN dotnet publish backend/Backend.csproj -c Release -o /app/publish /p:UseAppHost=false

# --- Stage 3: runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=backend /app/publish ./
COPY --from=frontend /src/frontend/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend.dll"]
