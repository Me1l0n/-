using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TcpEchoServer;

internal static class Program
{
    private const int DefaultPort = 5000;
    private static int _totalClientCounter = 0;
    private static int _activeClientsCount = 0;

    private static async Task Main(string[] args)
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.InputEncoding = utf8NoBom;
        Console.OutputEncoding = utf8NoBom;

        int port = DefaultPort;
        if (args.Length > 0 && (!int.TryParse(args[0], out port) || port is < 1 or > 65535))
        {
            Console.WriteLine($"Некорректный порт '{args[0]}'. Используется порт по умолчанию: {DefaultPort}.");
            port = DefaultPort;
        }

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[Сервер] Получен сигнал остановки (Ctrl+C). Завершение работы...");
            cts.Cancel();
        };

        // Создание асинхронного сокета TCP (IPv4, потоковый, протокол TCP)
        using var listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            listenerSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            listenerSocket.Listen(backlog: 100);

            Console.WriteLine("==================================================");
            Console.WriteLine("     АСИНХРОННЫЙ TCP ECHO-СЕРВЕР (.NET 9)         ");
            Console.WriteLine("==================================================");
            Console.WriteLine($"[Сервер] Запущен на 0.0.0.0:{port}");
            Console.WriteLine("[Сервер] Режим: асинхронные сокеты, параллельная обработка клиентов.");
            Console.WriteLine("[Сервер] Для остановки нажмите Ctrl+C.\n");

            // Основной асинхронный цикл принятия входящих подключений
            while (!cts.IsCancellationRequested)
            {
                Console.WriteLine($"[Сервер] Ожидание подключений... (Активных клиентов: {_activeClientsCount})");

                Socket clientSocket;
                try
                {
                    // Асинхронное ожидание подключения без блокировки потока
                    clientSocket = await listenerSocket.AcceptAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException ex) when (cts.IsCancellationRequested || ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    break;
                }

                int clientId = Interlocked.Increment(ref _totalClientCounter);
                int activeCount = Interlocked.Increment(ref _activeClientsCount);

                // Запуск обработки клиента в независимой асинхронной задаче
                // Основной поток/цикл НЕ блокируется и сразу возвращается к AcceptAsync
                _ = Task.Run(() => HandleClientAsync(clientSocket, clientId, cts.Token), CancellationToken.None);
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"[Критическая ошибка сокета] Не удалось запустить сервер на порту {port}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ошибка] Непредвиденное исключение в сервере: {ex.Message}");
        }
        finally
        {
            try
            {
                listenerSocket.Close();
            }
            catch { }
            Console.WriteLine("[Сервер] Остановлен. Все ресурсы освобождены.");
        }
    }

    /// <summary>
    /// Асинхронная обработка индивидуального клиента.
    /// Выполняется параллельно для каждого подключённого клиента.
    /// Изолирует сетевые ошибки и неожиданные отключения клиента, предотвращая падение сервера.
    /// </summary>
    private static async Task HandleClientAsync(Socket socket, int clientId, CancellationToken serverCancellationToken)
    {
        string remoteEndPoint = socket.RemoteEndPoint?.ToString() ?? "неизвестный адрес";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Клиент #{clientId} подключился ({remoteEndPoint}). Активных клиентов: {_activeClientsCount}.");

        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        try
        {
            // Сетевой поток поверх асинхронного сокета
            using var stream = new NetworkStream(socket, ownsSocket: false);
            using var reader = new StreamReader(stream, utf8NoBom, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            using var writer = new StreamWriter(stream, utf8NoBom, leaveOpen: true) { AutoFlush = true };

            while (!serverCancellationToken.IsCancellationRequested)
            {
                // Асинхронное неблокирующее чтение строки от клиента
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(serverCancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                // Если line == null, клиент штатно закрыл соединение (EOF)
                if (line is null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Отправил сигнал закрытия соединения (EOF).");
                    break;
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Получено: \"{line}\"");

                // Преобразование строки в верхний регистр
                string response = line.ToUpper();

                // Асинхронная неблокирующая отправка ответа клиенту
                await writer.WriteLineAsync(response.AsMemory(), serverCancellationToken);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Отправлено: \"{response}\"");
            }
        }
        catch (IOException ex) when (ex.InnerException is SocketException sex)
        {
            // Клиент оборвал связь (например, принудительно закрыл окно процесса / Reset connection)
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Сетевое соединение сброшено ({sex.SocketErrorCode}).");
        }
        catch (SocketException ex)
        {
            // Прямая ошибка сокета клиента
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Ошибка сокета ({ex.SocketErrorCode}): {ex.Message}");
        }
        catch (IOException ex)
        {
            // Ошибка потока данных при внезапном отключении клиента
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Соединение разорвано: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Сеанс отменён из-за остановки сервера.");
        }
        catch (Exception ex)
        {
            // Любая другая ошибка изолируется в рамках данного клиента
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Ошибка при обработке: {ex.Message}");
        }
        finally
        {
            // Гарантированное освобождение ресурсов сокета клиента
            try
            {
                if (socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch { }

            try
            {
                socket.Close();
                socket.Dispose();
            }
            catch { }

            int remaining = Interlocked.Decrement(ref _activeClientsCount);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Клиент #{clientId} отключился ({remoteEndPoint}). Активных клиентов: {remaining}.");

            // Если все клиенты отключились, сервер сообщает о готовности принять новых
            if (remaining == 0)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Сервер] Все клиенты отключились. Сервер ожидает новых подключений...\n");
            }
        }
    }
}
