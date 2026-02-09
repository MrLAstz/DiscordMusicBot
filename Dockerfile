# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DiscordMusicBot.csproj ./
# บังคับ Restore ใหม่ทุครั้งที่ Build
RUN dotnet restore --no-cache

COPY . .
# ลบ --no-restore ออกเพื่อให้มั่นใจว่า publish จะใช้ของที่ดึงมาใหม่ล่าสุด
RUN dotnet publish -c Release -o /app