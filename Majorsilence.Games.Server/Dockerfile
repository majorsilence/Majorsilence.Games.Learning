# Build context must be the repo root (this project references ../Majorsilence.Games.Shared):
#   docker build -f Majorsilence.Games.Server/Dockerfile -t titanic-server .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Majorsilence.Games.Shared/Majorsilence.Games.Shared.csproj Majorsilence.Games.Shared/
COPY Majorsilence.Games.Server/Majorsilence.Games.Server.csproj Majorsilence.Games.Server/
COPY Majorsilence.Games.Shared/ Majorsilence.Games.Shared/
COPY Majorsilence.Games.Server/ Majorsilence.Games.Server/
RUN dotnet publish Majorsilence.Games.Server/Majorsilence.Games.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# /data holds titanic.db - mount a volume here for persistence across container recreation.
ENV TITANIC_DB_PATH=/data/titanic.db
ENV ASPNETCORE_URLS=http://+:8080
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "Majorsilence.Games.Server.dll"]
