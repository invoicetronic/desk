FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

COPY src/*.csproj ./src/
RUN dotnet restore src/

COPY src/. ./src/
RUN dotnet publish src/ -c release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1
ENTRYPOINT ["dotnet", "Invoicetronic.Desk.dll"]
