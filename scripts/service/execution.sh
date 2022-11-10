#!/bin/bash
opts=$@
if [ ${#opts} -gt 0 ]
then
  echo 'Executing Nethermind Runner'
  /usr/share/nethermind/Nethermind.Runner $@
else
  echo 'Executing Nethermind Launcher'
  cd /usr/share/nethermind
  /usr/share/nethermind/Nethermind.Launcher
fi
