#FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
#USER $APP_UID
#WORKDIR /app
#EXPOSE 8080
#EXPOSE 8081
#
#FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
#ARG BUILD_CONFIGURATION=Release
#WORKDIR /src
#COPY ["ApiAlertaDengue/ApiAlertaDengue.csproj", "ApiAlertaDengue/"]
#RUN dotnet restore "ApiAlertaDengue/ApiAlertaDengue.csproj"
#COPY . .
#WORKDIR "/src/ApiAlertaDengue"
#RUN dotnet build "ApiAlertaDengue.csproj" -c $BUILD_CONFIGURATION -o /app/build
#
#FROM build AS publish
#ARG BUILD_CONFIGURATION=Release
#RUN dotnet publish "ApiAlertaDengue.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
#
#FROM base AS final
#WORKDIR /app
#COPY --from=publish /app/publish .
#ENTRYPOINT ["dotnet", "ApiAlertaDengue.dll"]
#


# ===== build =====
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# copie apenas os csproj primeiro (cache de restore)
COPY ApiAlertaDengue/ApiAlertaDengue.csproj ApiAlertaDengue/
RUN dotnet restore ApiAlertaDengue/ApiAlertaDengue.csproj

# agora o restante do código
COPY . .
WORKDIR /src/ApiAlertaDengue
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish ApiAlertaDengue.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ===== runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# importante: bind na porta injetada pela plataforma
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

# (opcional) se quiser logar no console em JSON:
# ENV Logging__Console__FormatterName=Json

COPY --from=build /app/publish .
# EXPOSE é opcional; a plataforma usa $PORT
# EXPOSE 8080

ENTRYPOINT ["dotnet", "ApiAlertaDengue.dll"]
