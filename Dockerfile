# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# ล้าง Cache ของระบบ NuGet ในเครื่อง Build ให้เกลี้ยง
RUN dotnet nuget locals all --clear 

# 1. ดึง Library ใหม่ 100%
COPY DiscordMusicBot.csproj ./
RUN dotnet restore --no-cache

COPY . .

# 2. แก้ไขตรงนี้: สั่งลบโฟลเดอร์ที่อาจค้างมาจากเครื่องเรา และสั่งคอมไพล์ใหม่แบบ Clean
RUN rm -rf bin obj && \
    dotnet publish -c Release -o /app /p:UseAppHost=false

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# 3. ติดตั้ง Library พื้นฐานสำหรับเสียง
RUN apt-get update && apt-get install -y \
    ffmpeg \
    libopus0 \
    libsodium23 \
    && ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 /usr/lib/libopus.so \
    && ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 /usr/lib/libsodium.so \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .
ะ
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "DiscordMusicBot.dll"]