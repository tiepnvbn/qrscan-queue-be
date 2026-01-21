# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["QueueQr.Api/QueueQr.Api.csproj", "QueueQr.Api/"]
RUN dotnet restore "QueueQr.Api/QueueQr.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/QueueQr.Api"
RUN dotnet build "QueueQr.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "QueueQr.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install PostgreSQL client for running migrations
RUN apt-get update && apt-get install -y postgresql-client && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Copy migrations and build script
COPY migrations /app/migrations
COPY build.sh /app/build.sh
RUN chmod +x /app/build.sh

# Expose port (Render will override with $PORT)
EXPOSE 8080

# Run migrations then start app
CMD ["/bin/bash", "-c", "./build.sh && dotnet QueueQr.Api.dll"]
