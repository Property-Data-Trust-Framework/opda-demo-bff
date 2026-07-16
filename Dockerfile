FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OpdaDemoBff/OpdaDemoBff.csproj src/OpdaDemoBff/
RUN dotnet restore src/OpdaDemoBff/OpdaDemoBff.csproj -r linux-x64
COPY src/OpdaDemoBff/ src/OpdaDemoBff/
RUN dotnet publish src/OpdaDemoBff/OpdaDemoBff.csproj \
    -c Release -o /app/publish --no-restore \
    -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:InvariantGlobalization=true

FROM public.ecr.aws/lambda/provided:al2023
WORKDIR /var/task
COPY --from=build /app/publish/OpdaDemoBff ./bootstrap
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
ENTRYPOINT ["/var/task/bootstrap"]
