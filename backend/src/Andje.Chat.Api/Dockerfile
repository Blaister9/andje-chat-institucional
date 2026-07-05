# Contexto de build: el directorio backend/ (ver docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Andje.Chat.Api/Andje.Chat.Api.csproj src/Andje.Chat.Api/
RUN dotnet restore src/Andje.Chat.Api/Andje.Chat.Api.csproj

COPY src/ src/
RUN dotnet publish src/Andje.Chat.Api/Andje.Chat.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# La imagen aspnet:8.0 escucha en el puerto 8080 por defecto (ASPNETCORE_HTTP_PORTS).
EXPOSE 8080
ENTRYPOINT ["dotnet", "Andje.Chat.Api.dll"]
