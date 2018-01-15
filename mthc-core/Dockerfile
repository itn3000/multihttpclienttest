FROM microsoft/dotnet:2.0-sdk

ARG HTTP_PROXY
ENv HTTP_PROXY ${HTTP_PROXY}

ADD . /src

WORKDIR /src

RUN dotnet publish -c Release -o /bin/mthc

CMD ["dotnet","/bin/mthc/UsingHttpClientConcurrentCore.dll","0"]