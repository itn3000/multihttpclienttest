#!/bin/bash

cat <(echo fw,method,loop,concurrent,limit,time) \
	<(sed -e 's/.*net,elapsed,/net,/' mthc-net-result-win.txt) \
	<(sed -e 's/.*core,elapsed,/core,/' mthc-core-result-win.txt) \
	> mthc-result-win.csv
cat <(echo fw,method,loop,concurrent,limit,time) \
	<(sed -e 's/.*net,elapsed,/net,/' mthc-net-result-linux.txt) \
	<(sed -e 's/.*core,elapsed,/core,/' mthc-core-result-linux.txt) \
	> mthc-result-linux.csv
