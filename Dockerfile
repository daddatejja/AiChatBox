# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["AiChatBox.Api/AiChatBox.Api.csproj", "AiChatBox.Api/"]
RUN dotnet restore "AiChatBox.Api/AiChatBox.Api.csproj"

# Copy everything else and build
COPY . .

# Copy widget files to wwwroot for serving
RUN mkdir -p AiChatBox.Api/wwwroot
COPY AiChatBox.Widget/ai-chatbox.js AiChatBox.Api/wwwroot/
COPY AiChatBox.Widget/ai-chatbox.css AiChatBox.Api/wwwroot/
COPY AiChatBox.Widget/audio-processor.js AiChatBox.Api/wwwroot/

WORKDIR "/src/AiChatBox.Api"
RUN dotnet build "AiChatBox.Api.csproj" -c Release -o /app/build

# Publish Stage
FROM build AS publish
RUN dotnet publish "AiChatBox.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create directory for SQLite database and logs
RUN mkdir -p /app/data /app/logs
ENV ASPNETCORE_URLS=http://+:5000
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/chat.db"

EXPOSE 5000
ENTRYPOINT ["dotnet", "AiChatBox.Api.dll"]
