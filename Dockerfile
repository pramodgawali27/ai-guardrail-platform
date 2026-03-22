# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore — copy project files first for layer caching
COPY EnterpriseAiGuardrailPlatform.sln ./
COPY src/Guardrail.Core/Guardrail.Core.csproj                   src/Guardrail.Core/
COPY src/Guardrail.Application/Guardrail.Application.csproj     src/Guardrail.Application/
COPY src/Guardrail.Infrastructure/Guardrail.Infrastructure.csproj src/Guardrail.Infrastructure/
COPY src/Guardrail.API/Guardrail.API.csproj                     src/Guardrail.API/

RUN dotnet restore src/Guardrail.API/Guardrail.API.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish src/Guardrail.API/Guardrail.API.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Copy seed data so the app can seed policies and eval datasets on startup
COPY policies /app/policies
COPY evaluations /app/evaluations

# Hugging Face Spaces requires port 7860
# Default: no external database → SQLite demo mode.
# Override ConnectionStrings__PostgreSql in HF Secrets to use a real PostgreSQL.
ENV ConnectionStrings__PostgreSql=""
ENV ConnectionStrings__Redis=""
ENV ASPNETCORE_URLS=http://+:7860
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 7860

ENTRYPOINT ["dotnet", "Guardrail.API.dll"]
