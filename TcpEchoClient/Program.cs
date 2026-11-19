using System.Net.Sockets;
using System.Text;

namespace TcpEchoClient;

internal static class Program
{
    private const string DefaultHost = "127.0.0.1";
    private const int DefaultPort = 5000;

    private static async Task Main(string[] args)
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.InputEncoding = utf8NoBom;
        Console.OutputEncoding = utf8NoBom;

        string host = args.Length > 0 ? args[0] : DefaultHost;
        int port = DefaultPort;

        if (args.Length > 1 && (!int.TryParse(args[1], out port) || port is < 1 or > 65535))
        {
            Console.WriteLine($"Некорректный порт '{args[1]}'. Используется порт по умолчанию: {DefaultPort}.");
            port = DefaultPort;
        }

        string clientName = args.Length > 2 ? args[2] : "Клиент";

        Console.WriteLine("==================================================");
        Console.WriteLine($"      АСИНХРОННЫЙ TCP ECHO-КЛИЕНТ ({clientName})   ");
        Console.WriteLine("==================================================");
        Console.WriteLine($"[Клиент] Подключение к серверу {host}:{port}...");

        using var client = new TcpClient();

        try
        {
            // Асинхронное подключение к серверу
            await client.ConnectAsync(host, port);
            Console.WriteLine("[Клиент] Подключение успешно установлено.");
            Console.WriteLine("[Клиент] Вводите строки для отправки. Для выхода введите 'exit'.\n");

            using NetworkStream stream = client.GetStream();
            using var reader = new StreamReader(stream, utf8NoBom, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            using var writer = new StreamWriter(stream, utf8NoBom, leaveOpen: true) { AutoFlush = true };

            while (true)
            {
                Console.Write($"{clientName} > ");
                string? input = Console.ReadLine();

                if (input is null || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[Клиент] Завершение сеанса...");
                    break;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                // Асинхронная отправка строки
                await writer.WriteLineAsync(input);

                // Асинхронное ожидание ответа от сервера
                string? response = await reader.ReadLineAsync();
                if (response is null)
                {
                    Console.WriteLine("[Клиент] Сервер закрыл соединение.");
                    break;
                }

                Console.WriteLine($"Ответ сервера  < {response}\n");
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"[Ошибка подключения] Не удалось связаться с сервером {host}:{port}: {ex.Message}");
            Console.WriteLine("Убедитесь, что сервер запущен и доступен по указанному адресу.");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[Ошибка обмена данными] Соединение с сервером потеряно: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Непредвиденная ошибка] {ex.Message}");
        }

        Console.WriteLine("[Клиент] Работа завершена.");
    }
}
