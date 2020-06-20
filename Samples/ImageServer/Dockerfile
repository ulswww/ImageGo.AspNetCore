FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
ENV LANG C.UTF-8

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["ImageServer.csproj", "./"]
COPY ["NuGet.Config", "./"]
RUN dotnet restore --configfile NuGet.Config "./ImageServer.csproj" 
COPY . .
WORKDIR "/src/."
RUN dotnet build "ImageServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ImageServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ImageServer.dll"]
