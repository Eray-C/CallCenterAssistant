# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["CallCenterAssistant/CallCenterAssistant.csproj", "CallCenterAssistant/"]
RUN dotnet restore "CallCenterAssistant/CallCenterAssistant.csproj"

# Copy remaining files and build
COPY . .
WORKDIR "/src/CallCenterAssistant"
RUN dotnet build "CallCenterAssistant.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CallCenterAssistant.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy published files
COPY --from=publish /app/publish .

# Install curl/tar to download Linux Piper engine and libgomp1 for Whisper.net
RUN apt-get update && apt-get install -y curl tar gzip libgomp1 && rm -rf /var/lib/apt/lists/*

# Create piper directory and download Linux Piper binaries + Turkish Voice Models
RUN mkdir -p /app/piper && \
    curl -L "https://github.com/rhasspy/piper/releases/download/v1.2.0/piper_amd64.tar.gz" -o /app/piper/piper.tar.gz && \
    tar -xf /app/piper/piper.tar.gz -C /app/piper --strip-components=1 && \
    rm /app/piper/piper.tar.gz && \
    curl -L "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/tr/tr_TR/fahrettin/medium/tr_TR-fahrettin-medium.onnx" -o /app/piper/tr_TR-fahrettin-medium.onnx && \
    curl -L "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/tr/tr_TR/fahrettin/medium/tr_TR-fahrettin-medium.onnx.json" -o /app/piper/tr_TR-fahrettin-medium.onnx.json && \
    chmod +x /app/piper/piper

# Set ASPNETCORE environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CallCenterAssistant.dll"]
