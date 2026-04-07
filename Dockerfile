FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /workspace
COPY .config .config
RUN dotnet tool restore
COPY .paket .paket
COPY paket.references paket.references
COPY paket.dependencies paket.lock ./

FROM build AS app-build

# Install node
RUN mkdir /usr/local/nvm
ENV NVM_DIR=/usr/local/nvm
ENV NODE_VERSION=22.12.0
RUN curl https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.1/install.sh | bash \
    && . $NVM_DIR/nvm.sh \
    && nvm install $NODE_VERSION \
    && nvm alias default $NODE_VERSION \
    && nvm use default

ENV NODE_PATH=$NVM_DIR/v$NODE_VERSION/lib/node_modules
ENV PATH=$NVM_DIR/versions/node/v$NODE_VERSION/bin:$PATH

ENV HUSKY=0
COPY Build.fsproj .
COPY Build.fs .
COPY Helpers.fs .
COPY src/ src/
RUN dotnet run bundle


FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY --from=app-build /workspace/deploy /app

ENV GENPRES_LOG=0
ENV GENPRES_PROD="1"
ENV GENPRES_DEBUG="0"
ARG GENPRES_URL_ARG
ENV GENPRES_URL_ID=$GENPRES_URL_ARG

WORKDIR /app
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Informedica.GenPRES.Server.dll" ]