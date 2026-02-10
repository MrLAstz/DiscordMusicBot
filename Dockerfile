# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# ล้าง Cache ของระบบ NuGet ให้เกลี้ยงก่อนเริ่ม
RUN dotnet nuget locals all --clear 

# 1. ดึงไฟล์โปรเจกต์มา Restore แบบไม่ใช้ Cache
COPY DiscordMusicBot.csproj ./
RUN dotnet restore --no-cache

# 2. ก๊อปปี้ไฟล์ทั้งหมดเข้าเครื่อง Build
COPY . .

# 3. ลบโฟลเดอร์ที่อาจจะค้างมาจากการรันในเครื่องตัวเอง (bin/obj) 
# แล้วค่อย Publish ใหม่
RUN rm -rf bin obj && \
    dotnet publish -c Release -o /app /p:UseAppHost=false --no-restore

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# 3. ติดตั้ง Library พื้นฐานสำหรับเสียง (ffmpeg, opus, sodium)
RUN apt-get update && apt-get install -y \
    ffmpeg \
    libopus0 \
    libsodium23 \
    && ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 /usr/lib/libopus.so \
    && ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 /usr/lib/libsodium.so \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# ตั้งค่า URL สำหรับ Railway
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "DiscordMusicBot.dll"]