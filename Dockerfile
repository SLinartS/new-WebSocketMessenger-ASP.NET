FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SimpleMessenger.csproj ./
RUN HUSKY=0 dotnet restore ./SimpleMessenger.csproj

COPY . ./
RUN HUSKY=0 dotnet publish ./SimpleMessenger.csproj -c Release -o /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

RUN addgroup -S app || true \
    && adduser -S -G app app || true

ENV ASPNETCORE_URLS=http://0.0.0.0:5237 \
    ASPNETCORE_ENVIRONMENT=Production \
    DATA_DIR=/app/data

RUN mkdir -p /app/data && chown -R app:app /app

COPY --from=build /app/publish ./

USER app
EXPOSE 5237

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://127.0.0.1:5237/ >/dev/null 2>&1 || exit 1

ENTRYPOINT ["dotnet", "SimpleMessenger.dll"]

