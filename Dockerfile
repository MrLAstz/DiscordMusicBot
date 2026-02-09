# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DiscordMusicBot.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# ปรับส่วน RUN ใน Dockerfile เป็นแบบนี้
RUN apt-get update && apt-get install -y \
    ffmpeg \
    libopus-dev \
    libsodium-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "DiscordMusicBot.dll"]
