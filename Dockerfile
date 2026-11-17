FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/GymBackend/GymBackend.csproj src/GymBackend/
RUN dotnet restore src/GymBackend/GymBackend.csproj
COPY . .
RUN dotnet publish src/GymBackend/GymBackend.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
ENV TZ=America/Santiago
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "GymBackend.dll"]
