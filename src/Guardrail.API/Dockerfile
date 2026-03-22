FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY EnterpriseAiGuardrailPlatform.sln ./
COPY src/Guardrail.Core/Guardrail.Core.csproj src/Guardrail.Core/
COPY src/Guardrail.Application/Guardrail.Application.csproj src/Guardrail.Application/
COPY src/Guardrail.Infrastructure/Guardrail.Infrastructure.csproj src/Guardrail.Infrastructure/
COPY src/Guardrail.API/Guardrail.API.csproj src/Guardrail.API/

RUN dotnet restore src/Guardrail.API/Guardrail.API.csproj

COPY . .
RUN dotnet publish src/Guardrail.API/Guardrail.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Guardrail.API.dll"]
