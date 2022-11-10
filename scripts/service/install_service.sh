#!/bin/bash
set -e 

#add nethermind user
sudo useradd -m -s /bin/bash nethermind

#download new /usr/bin/nethermind
sudo wget -q https://raw.githubusercontent.com/NethermindEth/nethermind/feature/nethermind_service/scripts/service/execution.sh -O /usr/bin/nethermind
sudo chmod +x /usr/bin/nethermind
sudo chown -R nethermind /usr/share/nethermind

#download env file
sudo wget -q https://raw.githubusercontent.com/NethermindEth/nethermind/feature/nethermind_service/scripts/service/env -O /home/nethermind/.env

#download service file
sudo wget -q https://raw.githubusercontent.com/NethermindEth/nethermind/feature/nethermind_service/scripts/service/nethermind.service -O /etc/systemd/system/nethermind.service

sudo systemctl daemon-reload
sudo systemctl enable nethermind

echo "Nethermind Service added succesfully"