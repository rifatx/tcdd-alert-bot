FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "TCDDAlertBot.csproj"
RUN dotnet build "TCDDAlertBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TCDDAlertBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir /app/data
ENTRYPOINT ["dotnet", "TCDDAlertBot.dll"]
