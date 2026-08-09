FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY server/RagKnowledgeService.csproj server/
RUN dotnet restore server/RagKnowledgeService.csproj
COPY server/ server/
RUN dotnet publish server/RagKnowledgeService.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
RUN addgroup -S gerald && adduser -S gerald -G gerald \
    && mkdir -p /data /app/docs \
    && chown -R gerald:gerald /data /app
COPY --from=build --chown=gerald:gerald /app/publish ./
COPY --chown=gerald:gerald docs/ ./docs/
USER gerald
EXPOSE 8080
ENTRYPOINT ["dotnet", "RagKnowledgeService.dll"]
