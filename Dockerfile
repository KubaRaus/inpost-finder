FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/InpostTask.Web/InpostTask.Web.csproj", "src/InpostTask.Web/"]
RUN dotnet restore "src/InpostTask.Web/InpostTask.Web.csproj"

COPY . .
RUN dotnet publish "src/InpostTask.Web/InpostTask.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet InpostTask.Web.dll"]
