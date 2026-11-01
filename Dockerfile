# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
ARG TARGETARCH
WORKDIR /src

COPY --link global.json Directory.Build.props Directory.Packages.props ./
COPY --link Choco.SharedKernel/Choco.SharedKernel.csproj Choco.SharedKernel/
COPY --link Choco.Domain/Choco.Domain.csproj Choco.Domain/
COPY --link Choco.Contract/Choco.Contract.csproj Choco.Contract/
COPY --link Choco.Application/Choco.Application.csproj Choco.Application/
COPY --link Choco.Infrastructure/Choco.Infrastructure.csproj Choco.Infrastructure/
COPY --link Choco.Presentation/Choco.Presentation.csproj Choco.Presentation/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore Choco.Presentation/Choco.Presentation.csproj -a $TARGETARCH

COPY --link . .
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish Choco.Presentation/Choco.Presentation.csproj \
        -c Release -a $TARGETARCH --no-restore -o /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble-chiseled@sha256:c89a6a61f494f20a5ae302990e814627a8d91b332fd4a8e7c1707c2d4da9555d AS final
WORKDIR /app
COPY --link --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Choco.Presentation.dll"]
