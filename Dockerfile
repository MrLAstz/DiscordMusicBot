# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish -c Release -o /app

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0

RUN apt-get update && apt-get install -y \
    ffmpeg \
    libopus0 \
    libsodium23 \
    && ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 /usr/lib/libsodium.so \
    && ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 /usr/lib/libopus.so \
    && rm -rf /var/lib/apt/lists/*

ENV LD_LIBRARY_PATH=/usr/lib

WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "DiscordMusicBot.dll"]
