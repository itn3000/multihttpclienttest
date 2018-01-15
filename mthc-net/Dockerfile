FROM mono:5.4

ARG HTTP_PROXY

ENv HTTP_PROXY ${HTTP_PROXY}

ADD . /src
WORKDIR /src

RUN msbuild /t:Restore \
    && msbuild /t:Build /p:Configuration=Release \
    && msbuild /t:Publish /p:OutputPath=/bin/mthc/

ENV MONO_THREADS_PER_CPU 2000

CMD ["bash", "test.sh"]