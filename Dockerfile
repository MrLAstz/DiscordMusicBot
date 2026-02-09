# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# ก๊อปปี้ไฟล์โปรเจกต์และสั่ง Restore แบบไม่ใช้ Cache เดิม
COPY DiscordMusicBot.csproj ./
RUN dotnet restore --no-cache

# ก๊อปปี้ไฟล์ที่เหลือทั้งหมด
COPY . .

# สั่ง Publish โดยลบ --no-restore ออก เพื่อให้มันตรวจสอบความถูกต้องของ Library ใหม่
RUN dotnet publish -c Release -o /app

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# ติดตั้ง Library ที่จำเป็นสำหรับการเล่นเพลง
RUN apt-get update && apt-get install -y \
    ffmpeg \
    libopus0 \
    libsodium23 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# ตั้งค่า Port (Railway จะเป็นคนกำหนดค่านี้ให้)
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "DiscordMusicBot.dll"]