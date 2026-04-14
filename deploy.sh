#!/bin/bash
set -euo pipefail

SERVER="botsvm"
REMOTE_DIR="/opt/uvbstealer"
SERVICE="uvbstealer"
PROJECT="UVBStealer"

echo "=== Building $PROJECT ==="
rm -rf ./publish
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish

echo "=== Merging config ==="
jq -s '.[0] * .[1]' appsettings.json appsettings.Production.json > ./publish/appsettings.json
rm -f ./publish/appsettings.*.json

echo "=== Deploying to $SERVER:$REMOTE_DIR ==="
ssh "$SERVER" "sudo mkdir -p $REMOTE_DIR $REMOTE_DIR/data/media && sudo chown -R \$(whoami) $REMOTE_DIR"
rsync -avz --delete \
    --exclude 'data/' \
    --exclude 'memes/' \
    --exclude 'memes_sent.txt' \
    --exclude '.DS_Store' \
    ./publish/ "$SERVER:$REMOTE_DIR/"

echo "=== Setting up systemd service ==="
ssh "$SERVER" "sudo tee /etc/systemd/system/$SERVICE.service > /dev/null" <<EOF
[Unit]
Description=UVB-76 Telegram Stealer
After=network.target

[Service]
Type=simple
WorkingDirectory=$REMOTE_DIR
Environment=DOTNET_ENVIRONMENT=Production
ExecStart=$REMOTE_DIR/$PROJECT
Restart=always
RestartSec=10
SyslogIdentifier=$SERVICE

[Install]
WantedBy=multi-user.target
EOF

ssh "$SERVER" "sudo systemctl daemon-reload && sudo systemctl enable $SERVICE && sudo systemctl restart $SERVICE"

echo "=== Checking status ==="
ssh "$SERVER" "sudo systemctl status $SERVICE --no-pager" || true

echo "=== Done ==="
