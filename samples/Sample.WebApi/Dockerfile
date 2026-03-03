FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for layer-cached restore
COPY ["HybridOutbox.sln", "./"]
COPY ["src/HybridOutbox/HybridOutbox.csproj", "src/HybridOutbox/"]
COPY ["src/HybridOutbox.DynamoDb/HybridOutbox.DynamoDb.csproj", "src/HybridOutbox.DynamoDb/"]
COPY ["src/HybridOutbox.MassTransit/HybridOutbox.MassTransit.csproj", "src/HybridOutbox.MassTransit/"]
COPY ["samples/Sample.WebApi/Sample.WebApi.csproj", "samples/Sample.WebApi/"]

# Restore dependencies
RUN dotnet restore "samples/Sample.WebApi/Sample.WebApi.csproj"

# Copy all source files
COPY . .

# Build the project
WORKDIR "/src/samples/Sample.WebApi"
RUN dotnet build "Sample.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sample.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sample.WebApi.dll"]
