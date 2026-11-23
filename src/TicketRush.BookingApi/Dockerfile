# Збирається з кореня репозиторію: docker build -f src/TicketRush.BookingApi/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/TicketRush.Shared/TicketRush.Shared.csproj src/TicketRush.Shared/
COPY src/TicketRush.BookingApi/TicketRush.BookingApi.csproj src/TicketRush.BookingApi/
RUN dotnet restore src/TicketRush.BookingApi/TicketRush.BookingApi.csproj
COPY src/ src/
RUN dotnet publish src/TicketRush.BookingApi/TicketRush.BookingApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TicketRush.BookingApi.dll"]
