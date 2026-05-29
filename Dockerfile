# BUILD STAGE
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy projects and restore dependencies
COPY ["NeoPos.WebAPI/NeoPos.WebAPI.csproj", "NeoPos.WebAPI/"]
COPY ["BusinessLayer/BusinessLayer.csproj", "BusinessLayer/"]
COPY ["DAL.Server/DAL.Server.csproj", "DAL.Server/"]
COPY ["Domain/Domain.csproj", "Domain/"]
RUN dotnet restore "NeoPos.WebAPI/NeoPos.WebAPI.csproj"

# Copy everything and build
COPY . .
WORKDIR "/src/NeoPos.WebAPI"
RUN dotnet build "NeoPos.WebAPI.csproj" -c Release -o /app/build

# PUBLISH STAGE
FROM build AS publish
RUN dotnet publish "NeoPos.WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# RUN STAGE
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Volume for SQLite and Uploads
VOLUME /app/data
VOLUME /app/wwwroot/uploads

# Expose API port
EXPOSE 5050
ENV ASPNETCORE_URLS=http://+:5050

ENTRYPOINT ["dotnet", "NeoPos.WebAPI.dll"]
