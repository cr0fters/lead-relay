FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update \
  && apt-get install -y --no-install-recommends default-mysql-client \
  && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet tool install --global dotnet-ef --version 8.*
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet restore src/LeadRelay.Web/LeadRelay.Web.csproj
RUN dotnet publish src/LeadRelay.Web/LeadRelay.Web.csproj -c Release -o /out
RUN dotnet ef migrations script --idempotent --project src/LeadRelay.Infrastructure --startup-project src/LeadRelay.Infrastructure --configuration Release --output /out/migrations.sql --no-build

FROM base AS final
WORKDIR /app
COPY --from=build /out .
COPY build/apply-migrations.sh /app/apply-migrations.sh
RUN chmod +x /app/apply-migrations.sh
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet","LeadRelay.Web.dll"]
