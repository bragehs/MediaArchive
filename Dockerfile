# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy the project file and restore dependencies
COPY ["MediaArchive.API.csproj", "./"]
RUN dotnet restore "MediaArchive.API.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "MediaArchive.API.csproj" -c Release -o /out

# Build the final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .

# Railway provides a PORT environment variable
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MediaArchive.API.dll"]