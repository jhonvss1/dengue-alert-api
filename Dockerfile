# ===== build =====
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# restaura primeiro só com o csproj para aproveitar o cache de camadas
COPY ApiAlertaDengue.csproj ./
RUN dotnet restore ApiAlertaDengue.csproj

COPY . .
RUN dotnet publish ApiAlertaDengue.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

# ===== runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# a imagem do aspnet já escuta em 8080 (ASPNETCORE_HTTP_PORTS); o TLS termina na Cloudflare
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ApiAlertaDengue.dll"]
