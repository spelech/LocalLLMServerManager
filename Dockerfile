FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["LocalLLMServerManager.slnx", "./"]
COPY ["LocalLLMServerManager.csproj", "./"]
COPY ["LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj", "LocalLLMServerManager.Shared/"]
COPY ["LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj", "LocalLLMServerManager.Web/"]

# Restore dependencies
RUN dotnet restore "LocalLLMServerManager.slnx"

# Copy source code
COPY . .

# Build WASM UI and copy output to wwwroot
RUN dotnet publish "LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj" -c Release -o /app/wwwroot_wasm --nologo \
    && cp -r /app/wwwroot_wasm/wwwroot/* wwwroot/

# Publish Main Server App
RUN dotnet publish "LocalLLMServerManager.csproj" -c Release -o /app/publish --nologo /p:PublishSingleFile=false

# Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5246
EXPOSE 5246

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LocalLLMServerManager.dll", "--service"]
