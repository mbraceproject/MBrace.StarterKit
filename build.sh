#!/bin/bash
if [ "X$OS" = "XWindows_NT" ] ; then
# use .Net
  RUN=""
else
# use mono
  RUN="mono"
fi
  
$RUN .paket/paket.bootstrapper.exe
exit_code=$?
if [ $exit_code -ne 0 ]; then
	exit $exit_code
fi

$RUN .paket/paket.exe restore -v
exit_code=$?
if [ $exit_code -ne 0 ]; then
	exit $exit_code
fi
