# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["MechanicApp.Server.csproj", "."]
RUN dotnet restore "./MechanicApp.Server.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "MechanicApp.Server.csproj" -c Release -o /app/build
RUN dotnet publish "MechanicApp.Server.csproj" -c Release -o /app/publish

# Serve Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Install Kerberos library required by Npgsql for GSSAPI auth support
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 80
COPY --from=build /app/publish .

# Ensure wwwroot/uploads exists and is writable by the app
RUN mkdir -p /app/wwwroot/uploads && chown -R app:app /app/wwwroot

USER app
ENTRYPOINT ["dotnet", "MechanicApp.Server.dll"]
