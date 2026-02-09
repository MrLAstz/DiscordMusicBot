# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DiscordMusicBot.csproj ./
RUN dotnet restore --no-cache

COPY . .
RUN dotnet publish -c Release -o /app

# ---------- RUNTIME ---------- (แก้เริ่มจากตรงนี้ครับ)
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# ติดตั้ง libopus และสร้างทางลัด (Symbolic Link) เพื่อให้บอทหาไฟล์เจอแน่นอน
RUN apt-get update && apt-get install -y \
    ffmpeg \
    libopus0 \
    libsodium23 \
    && ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 /usr/lib/libopus.so \
    && ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 /usr/lib/libsodium.so \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "DiscordMusicBot.dll"]