# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY *.sln ./
COPY DiscordMusicBot/*.csproj ./DiscordMusicBot/
RUN dotnet restore

COPY . .
WORKDIR /src/DiscordMusicBot
RUN dotnet publish -c Release -f net10.0 -o /app --no-restore

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0

RUN apt-get update && apt-get install -y \
    libopus0 \
    libsodium23 \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# สำคัญสำหรับ Railway
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "DiscordMusicBot.dll"]
