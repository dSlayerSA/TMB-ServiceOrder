FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY OrderManagement.sln .
COPY OrderManagement.Domain/OrderManagement.Domain.csproj OrderManagement.Domain/
COPY OrderManagement.Application/OrderManagement.Application.csproj OrderManagement.Application/
COPY OrderManagement.Infrastructure/OrderManagement.Infrastructure.csproj OrderManagement.Infrastructure/
COPY OrderManagement.Api/OrderManagement.Api.csproj OrderManagement.Api/
COPY OrderManagement.Worker/OrderManagement.Worker.csproj OrderManagement.Worker/
COPY OrderManagement.Tests/OrderManagement.Tests.csproj OrderManagement.Tests/
RUN dotnet restore OrderManagement.Worker/OrderManagement.Worker.csproj

COPY . .
RUN dotnet publish OrderManagement.Worker/OrderManagement.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OrderManagement.Worker.dll"]
