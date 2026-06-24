FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY InsecureMongoApp.API/InsecureMongoApp.csproj .
RUN dotnet restore

COPY . .

RUN dotnet publish -c Release -o /app/publish

FROM ubuntu:20.04

RUN apt-get update && \
    apt-get install -y wget curl libicu66 libssl1.1 zlib1g libc6 libgcc1 libgssapi-krb5-2 ca-certificates && \
    rm -rf /var/lib/apt/lists/*

ENV DOTNET_VERSION=8.0.0
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH=$PATH:/usr/share/dotnet

RUN wget https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.100/dotnet-sdk-8.0.100-linux-x64.tar.gz && \
    mkdir -p "$DOTNET_ROOT" && \
    tar -zxf dotnet-sdk-8.0.100-linux-x64.tar.gz -C "$DOTNET_ROOT" && \
    ln -s "$DOTNET_ROOT/dotnet" /usr/bin/dotnet && \
    rm dotnet-sdk-8.0.100-linux-x64.tar.gz

ENV ASPNETCORE_URLS=http://+:5000

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
ENTRYPOINT ["dotnet", "InsecureMongoApp.dll"]
