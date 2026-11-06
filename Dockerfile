# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
ARG TARGETARCH
WORKDIR /src

COPY --link global.json Directory.Build.props Directory.Packages.props ./
COPY --link src/Choco.SharedKernel/Choco.SharedKernel.csproj src/Choco.SharedKernel/
COPY --link src/Choco.Domain/Choco.Domain.csproj src/Choco.Domain/
COPY --link src/Choco.Contract/Choco.Contract.csproj src/Choco.Contract/
COPY --link src/Choco.Application/Choco.Application.csproj src/Choco.Application/
COPY --link src/Choco.Infrastructure/Choco.Infrastructure.csproj src/Choco.Infrastructure/
COPY --link src/Choco.Presentation/Choco.Presentation.csproj src/Choco.Presentation/
# Keep packages in the restore layer so --no-restore works even when a builder's cache mounts are empty.
RUN dotnet restore src/Choco.Presentation/Choco.Presentation.csproj -a $TARGETARCH

COPY --link . .
RUN dotnet publish src/Choco.Presentation/Choco.Presentation.csproj \
        -c Release -a $TARGETARCH --no-restore -o /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble-chiseled@sha256:c89a6a61f494f20a5ae302990e814627a8d91b332fd4a8e7c1707c2d4da9555d AS final
WORKDIR /app
COPY --link --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Choco.Presentation.dll"]
