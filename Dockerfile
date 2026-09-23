FROM node:22-alpine AS client-build
WORKDIR /src/Client

COPY Client/package.json Client/package-lock.json ./
RUN npm ci

COPY Client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS server-build
WORKDIR /src

COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Web/Web.csproj", "Web/"]
RUN dotnet restore "Web/Web.csproj"

COPY . .
RUN dotnet publish "Web/Web.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

COPY --from=client-build /src/Client/dist /app/publish/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

COPY --from=server-build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "Web.dll"]
