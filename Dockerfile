# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# ล้าง Cache ของ NuGet และดึง Library ใหม่ 100%
COPY DiscordMusicBot.csproj ./
RUN dotnet restore --no-cache

COPY . .
# สั่ง Publish โดยบังคับให้ตรวจสอบการอ้างอิงใหม่ทั้งหมด
RUN dotnet publish -c Release -o /app --no-restore

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# ติดตั้ง libopus และสร้าง Symbolic Link (ทางลัด) ให้ระบบมองเห็นไฟล์
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