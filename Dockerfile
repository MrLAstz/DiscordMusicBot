# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# เพิ่มบรรทัดนี้: ล้าง Cache ของระบบ NuGet ในเครื่อง Build ให้เกลี้ยง
RUN dotnet nuget locals all --clear 

# 1. ดึง Library ใหม่ 100%
COPY DiscordMusicBot.csproj ./
RUN dotnet restore --no-cache

COPY . .

# 2. คอมไพล์ใหม่โดยไม่ใช้ของเก่าค้างคา
RUN dotnet publish -c Release -o /app

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# 3. ติดตั้ง Library พื้นฐานสำหรับเสียง (คงเดิมไว้เพราะถูกต้องแล้ว)
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