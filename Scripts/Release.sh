function service {
    sudo systemctl $1 fumo_bot.service
    sudo systemctl $1 fumo_web.service
}

git pull

service stop

dotnet build -c Release

service start