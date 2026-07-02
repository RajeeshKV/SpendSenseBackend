FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SpendSense.sln ./
COPY src/SpendSense.Api/SpendSense.Api.csproj src/SpendSense.Api/
COPY src/SpendSense.Application/SpendSense.Application.csproj src/SpendSense.Application/
COPY src/SpendSense.Domain/SpendSense.Domain.csproj src/SpendSense.Domain/
COPY src/SpendSense.Infrastructure/SpendSense.Infrastructure.csproj src/SpendSense.Infrastructure/
COPY src/SpendSense.Shared/SpendSense.Shared.csproj src/SpendSense.Shared/
RUN dotnet restore src/SpendSense.Api/SpendSense.Api.csproj
COPY . .
RUN dotnet publish src/SpendSense.Api/SpendSense.Api.csproj -c Release -o /app/publish --no-restore
RUN dotnet tool install --global dotnet-ef --version 8.*
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle -r linux-x64 --configuration Release --project src/SpendSense.Infrastructure/SpendSense.Infrastructure.csproj --startup-project src/SpendSense.Api/SpendSense.Api.csproj -o /app/publish/migrate

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY scripts/render-start.sh ./render-start.sh
RUN sed -i 's/\r$//' ./render-start.sh && chmod +x ./render-start.sh ./migrate
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["./render-start.sh"]
