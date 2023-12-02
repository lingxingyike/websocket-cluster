#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["WebSocketApplication1/WebSocketApplication1.csproj", "WebSocketApplication1/"]
COPY ["RedisHelper/RedisHelper.csproj", "RedisHelper/"]
RUN dotnet restore "WebSocketApplication1/WebSocketApplication1.csproj"
COPY . .
WORKDIR "/src/WebSocketApplication1"
RUN dotnet build "WebSocketApplication1.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebSocketApplication1.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebSocketApplication1.dll"]