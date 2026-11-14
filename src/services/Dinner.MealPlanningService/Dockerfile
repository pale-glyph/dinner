ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY global.json Dinner.sln ./
COPY src/Dinner.ServiceDefaults/Dinner.ServiceDefaults.csproj src/Dinner.ServiceDefaults/
COPY src/services/Dinner.MealPlanningService/Dinner.MealPlanningService.csproj src/services/Dinner.MealPlanningService/
RUN dotnet restore src/services/Dinner.MealPlanningService/Dinner.MealPlanningService.csproj
COPY src/ src/
RUN dotnet publish src/services/Dinner.MealPlanningService/Dinner.MealPlanningService.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
RUN apt-get update && apt-get install --yes --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Dinner.MealPlanningService.dll"]
