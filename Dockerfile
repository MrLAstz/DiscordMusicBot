# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj ก่อน เพื่อ cache
COPY DiscordMusicBot.csproj ./
RUN dotnet restore

# copy ทุกอย่าง
COPY . .
RUN dotnet publish -c Release -f net8.0 -o /app --no-restore

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/runtime:8.0

RUN apt-get update && apt-get install -y \
    libopus0 \
    libsodium23 \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# สำคัญมากสำหรับ Railway
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "DiscordMusicBot.dll"]
