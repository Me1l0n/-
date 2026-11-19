@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo ======================================================================
echo    Сборка и запуск асинхронного TCP Echo-сервера и клиентов (.NET 9)
echo ======================================================================
echo.

echo [1/4] Сборка решения SyncTcpEcho.sln...
dotnet build SyncTcpEcho.sln -c Release --nologo -v minimal
if errorlevel 1 (
    echo.
    echo [Ошибка] Не удалось собрать проект. Убедитесь, что установлен .NET 9 SDK.
    pause
    exit /b 1
)

echo.
echo [2/4] Запуск асинхронного TCP-сервера в отдельном окне...
start "TCP Echo Server (Async)" cmd /k "chcp 65001 >nul && TcpEchoServer\bin\Release\net9.0\TcpEchoServer.exe"

timeout /t 1 /nobreak >nul

echo [3/4] Запуск первого клиента (Клиент #1) в отдельном окне...
start "TCP Echo Client #1" cmd /k "chcp 65001 >nul && TcpEchoClient\bin\Release\net9.0\TcpEchoClient.exe 127.0.0.1 5000 Клиент-1"

timeout /t 1 /nobreak >nul

echo [4/4] Запуск второго клиента (Клиент #2) в этом окне для одновременной работы...
echo       (Оба клиента могут общаться с сервером параллельно)
echo.
TcpEchoClient\bin\Release\net9.0\TcpEchoClient.exe 127.0.0.1 5000 Клиент-2

echo.
pause
