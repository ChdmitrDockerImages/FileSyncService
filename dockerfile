# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY src/*.csproj .
RUN dotnet restore

COPY src .

RUN dotnet publish -c Release -o /app --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

RUN groupadd --system appgroup && \
    useradd --system --groups appgroup appuser
USER appuser

COPY --from=build --chown=appuser:appgroup /app ./

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 8080

ENTRYPOINT ["./FileSyncServer"]
