@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo [1/3] Сборка решения SyncTcpEcho...
dotnet build SyncTcpEcho.sln -c Release --nologo -v minimal
if errorlevel 1 (
    echo.
    echo [Ошибка] Не удалось собрать проект. Убедитесь, что установлен .NET 9 SDK.
    pause
    exit /b 1
)

echo [2/3] Запуск TCP Echo-сервера в отдельном окне...
start "TCP Echo Server" cmd /k "chcp 65001 >nul && TcpEchoServer\bin\Release\net9.0\TcpEchoServer.exe"

timeout /t 1 /nobreak >nul

echo [3/3] Запуск TCP Echo-клиента...
echo.
TcpEchoClient\bin\Release\net9.0\TcpEchoClient.exe

echo.
pause
