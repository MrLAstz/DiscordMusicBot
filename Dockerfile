# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. ล้าง Cache NuGet
RUN dotnet nuget locals all --clear 

# 2. ก๊อปปี้ไฟล์โปรเจกต์และ Restore
COPY DiscordMusicBot.csproj ./
RUN dotnet restore --no-cache

# 3. ก๊อปปี้ไฟล์ที่เหลือ "หลังจาก" Restore เสร็จแล้ว
COPY . .

# 4. คอมไพล์ (ห้ามใส่ --no-restore เพราะเราลบ bin/obj ทิ้ง)
RUN rm -rf bin obj && \
    dotnet publish -c Release -o /app /p:UseAppHost=false

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0

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