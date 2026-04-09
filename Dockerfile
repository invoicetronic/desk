FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

COPY src/*.csproj ./src/
RUN dotnet restore src/

COPY src/. ./src/
RUN dotnet publish src/ -c release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
USER root
WORKDIR /app
COPY --from=build /app ./
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN mkdir -p /app/logs /app/data \
    && chown -R app:app /app/logs /app/data \
    && chmod +x /usr/local/bin/docker-entrypoint.sh
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
CMD ["dotnet", "Invoicetronic.Desk.dll"]
