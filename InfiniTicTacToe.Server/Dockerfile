FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5154

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["InfiniTicTacToe.Server/InfiniTicTacToe.Server.csproj", "InfiniTicTacToe.Server/"]
RUN dotnet restore "InfiniTicTacToe.Server/InfiniTicTacToe.Server.csproj"
COPY . .
WORKDIR "/src/InfiniTicTacToe.Server"
RUN dotnet build "InfiniTicTacToe.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "InfiniTicTacToe.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "InfiniTicTacToe.Server.dll"]
