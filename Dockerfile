FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LeaveDesk.Api/LeaveDesk.Api.csproj src/LeaveDesk.Api/
RUN dotnet restore src/LeaveDesk.Api/LeaveDesk.Api.csproj

COPY src/LeaveDesk.Api/ src/LeaveDesk.Api/
RUN dotnet publish src/LeaveDesk.Api/LeaveDesk.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app .

ENTRYPOINT ["dotnet", "LeaveDesk.Api.dll"]
