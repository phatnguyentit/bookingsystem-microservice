ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG SERVICE_PROJECT_PATH
WORKDIR /src
COPY src/ src/
RUN dotnet restore "${SERVICE_PROJECT_PATH}"
RUN dotnet publish "${SERVICE_PROJECT_PATH}" -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ARG ASSEMBLY_NAME
RUN printf '#!/bin/sh\nexec dotnet /app/%s.dll "$@"\n' "${ASSEMBLY_NAME}" > /entrypoint.sh \
    && chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
