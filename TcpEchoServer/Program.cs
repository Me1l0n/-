using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TcpEchoServer;

internal static class Program
{
    private const int DefaultPort = 5000;

    private static void Main(string[] args)
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

        var listener = new TcpListener(IPAddress.Any, port);
        bool isRunning = true;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            isRunning = false;
            Console.WriteLine("\n[Сервер] Получен сигнал остановки (Ctrl+C). Завершение работы...");
            listener.Stop();
        };

        try
        {
            listener.Start();
            Console.WriteLine("==================================================");
            Console.WriteLine("   СИНХРОННЫЙ TCP ECHO-СЕРВЕР (БЛОКИРУЮЩИЙ РЕЖИМ)");
            Console.WriteLine("==================================================");
            Console.WriteLine($"[Сервер] Запущен на 0.0.0.0:{port}");
            Console.WriteLine("[Сервер] Режим работы: обработка одного клиента за раз.");
            Console.WriteLine("[Сервер] Для остановки нажмите Ctrl+C.\n");

            int clientCounter = 0;

            while (isRunning)
            {
                Console.WriteLine("[Сервер] Ожидание подключения нового клиента...");

                TcpClient client;
                try
                {
                    // Блокирующий вызов: поток останавливается до подключения клиента
                    client = listener.AcceptTcpClient();
                }
                catch (SocketException) when (!isRunning)
                {
                    break;
                }

                clientCounter++;
                HandleClient(client, clientCounter);
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"[Ошибка сокета] Не удалось запустить сервер на порту {port}: {ex.Message}");
        }
        finally
        {
            listener.Stop();
            Console.WriteLine("[Сервер] Остановлен.");
        }
    }

    /// <summary>
    /// Синхронная обработка одного подключённого клиента до его отключения.
    /// Пока выполняется этот метод, сервер не принимает других клиентов.
    /// </summary>
    private static void HandleClient(TcpClient client, int clientId)
    {
        string remoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "неизвестный адрес";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Клиент #{clientId} подключился ({remoteEndPoint}).");

        using (client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();
                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                using var reader = new StreamReader(stream, utf8NoBom, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                using var writer = new StreamWriter(stream, utf8NoBom, leaveOpen: true) { AutoFlush = true };

                string? receivedLine;
                // Блокирующее чтение строк до закрытия соединения клиентом
                while ((receivedLine = reader.ReadLine()) != null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Получено: \"{receivedLine}\"");

                    string response = receivedLine.ToUpper();
                    writer.WriteLine(response);

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Отправлено: \"{response}\"");
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Соединение прервано: {ex.Message}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Клиент #{clientId}] Ошибка сети: {ex.Message}");
            }
        }

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Клиент #{clientId} отключился ({remoteEndPoint}).\n");
    }
}
