FROM mono:5.4

ADD ./bin/Release/net461/ /bin/mthc/

ENV MONO_THREADS_PER_CPU 2000

CMD ["mono", "/bin/mthc/UsingHttpClientConcurrentNet.exe", "0"]