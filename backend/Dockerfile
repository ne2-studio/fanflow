FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["FanFlow.Api/FanFlow.Api.csproj", "FanFlow.Api/"]
COPY ["FanFlow/FanFlow.csproj", "FanFlow/"]
COPY ["FanFlow.Infra/FanFlow.Infra.csproj", "FanFlow.Infra/"]
RUN dotnet restore "FanFlow.Api/FanFlow.Api.csproj"

# Copy the rest of the code
COPY . .

# Build and publish
RUN dotnet build "FanFlow.Api/FanFlow.Api.csproj" -c Release -o /app/build
RUN dotnet publish "FanFlow.Api/FanFlow.Api.csproj" -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set the entry point
ENTRYPOINT ["dotnet", "FanFlow.Api.dll"]
