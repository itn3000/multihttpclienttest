#!/bin/bash

export Logging:LogLevel:Microsoft="None"
export MONO_THREADS_PER_CPU=100

for climit in 1 2 5 10 50 100;do
    export MTHC_CONNECTION_LIMIT=$climit
    for method in 0 1 2;do
        mono "/bin/mthc/UsingHttpClientConcurrentNet.exe" $method
    done
done