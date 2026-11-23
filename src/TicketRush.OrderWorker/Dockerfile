# Збирається з кореня репозиторію: docker build -f src/TicketRush.OrderWorker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/TicketRush.Shared/TicketRush.Shared.csproj src/TicketRush.Shared/
COPY src/TicketRush.OrderWorker/TicketRush.OrderWorker.csproj src/TicketRush.OrderWorker/
RUN dotnet restore src/TicketRush.OrderWorker/TicketRush.OrderWorker.csproj
COPY src/ src/
RUN dotnet publish src/TicketRush.OrderWorker/TicketRush.OrderWorker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "TicketRush.OrderWorker.dll"]
