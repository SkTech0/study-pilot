FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/StudyPilot.API/StudyPilot.API.csproj", "src/StudyPilot.API/"]
COPY ["src/StudyPilot.Application/StudyPilot.Application.csproj", "src/StudyPilot.Application/"]
COPY ["src/StudyPilot.Domain/StudyPilot.Domain.csproj", "src/StudyPilot.Domain/"]
COPY ["src/StudyPilot.Infrastructure/StudyPilot.Infrastructure.csproj", "src/StudyPilot.Infrastructure/"]
RUN dotnet restore "src/StudyPilot.API/StudyPilot.API.csproj"
COPY src src
RUN dotnet publish "src/StudyPilot.API/StudyPilot.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "StudyPilot.API.dll"]
