FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["KeycloakWebService.csproj", "."]
RUN dotnet restore "KeycloakWebService.csproj"
COPY . .
RUN dotnet build "KeycloakWebService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KeycloakWebService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "KeycloakWebService.dll"]
