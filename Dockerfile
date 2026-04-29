FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/AgendaInstitucional.Api/AgendaInstitucional.Api.csproj", "src/AgendaInstitucional.Api/"]
COPY ["src/AgendaInstitucional.Application/AgendaInstitucional.Application.csproj", "src/AgendaInstitucional.Application/"]
COPY ["src/AgendaInstitucional.Domain/AgendaInstitucional.Domain.csproj", "src/AgendaInstitucional.Domain/"]
COPY ["src/AgendaInstitucional.Infrastructure/AgendaInstitucional.Infrastructure.csproj", "src/AgendaInstitucional.Infrastructure/"]

RUN dotnet restore "src/AgendaInstitucional.Api/AgendaInstitucional.Api.csproj"

COPY . .
WORKDIR "/src/src/AgendaInstitucional.Api"
RUN dotnet publish "AgendaInstitucional.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AgendaInstitucional.Api.dll"]