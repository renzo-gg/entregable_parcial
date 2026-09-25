# ---------------------------------------------------------------------------
# Etapa 1: build/restore/publish
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar solo con el csproj para aprovechar la cache de capas de Docker.
COPY ["PlataformaCreditos.csproj", "."]
RUN dotnet restore "PlataformaCreditos.csproj"

# Copiar el codigo restante y publicar en Release.
COPY . .
RUN dotnet publish "PlataformaCreditos.csproj" -c Release -o /app/publish --no-restore

# ---------------------------------------------------------------------------
# Etapa 2: imagen final de runtime (optimizada para produccion)
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Puerto por defecto para ejecuciones locales con docker (Render lo sobreescribe
# con su variable de entorno PORT / ASPNETCORE_URLS=http://0.0.0.0:${PORT}).
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

# Carpeta persistente para la base de datos SQLite (monte un volumen aqui).
RUN mkdir -p /var/data && chmod 777 /var/data
VOLUME /var/data

COPY --from=build /app/publish .

EXPOSE 8080

# Usa el puerto dinamico $PORT que inyecta Render; si no existe (docker local),
# cae a 8080.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet PlataformaCreditos.dll"]