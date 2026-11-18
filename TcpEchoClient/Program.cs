using System.Net.Sockets;
using System.Text;

namespace TcpEchoClient;

internal static class Program
{
    private const string DefaultHost = "127.0.0.1";
    private const int DefaultPort = 5000;

    private static void Main(string[] args)
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

        Console.WriteLine("==================================================");
        Console.WriteLine("             СИНХРОННЫЙ TCP ECHO-КЛИЕНТ           ");
        Console.WriteLine("==================================================");
        Console.WriteLine($"[Клиент] Подключение к серверу {host}:{port}...");

        try
        {
            // Синхронное подключение к серверу
            using var client = new TcpClient();
            client.Connect(host, port);
            Console.WriteLine("[Клиент] Подключение установлено.");
            Console.WriteLine("[Клиент] Вводите строки для отправки. Для выхода введите 'exit'.\n");

            using NetworkStream stream = client.GetStream();
            using var reader = new StreamReader(stream, utf8NoBom, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            using var writer = new StreamWriter(stream, utf8NoBom, leaveOpen: true) { AutoFlush = true };

            while (true)
            {
                Console.Write("Введите строку > ");
                string? input = Console.ReadLine();

                if (input is null || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[Клиент] Завершение сеанса...");
                    break;
                }

                // Синхронная отправка строки серверу
                writer.WriteLine(input);

                // Блокирующее ожидание ответа от сервера
                string? response = reader.ReadLine();
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
            Console.WriteLine($"[Ошибка подключения] Не удалось подключиться к {host}:{port}: {ex.Message}");
            Console.WriteLine("Убедитесь, что сервер запущен и слушает указанный порт.");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[Ошибка обмена данными] Соединение с сервером потеряно: {ex.Message}");
        }

        Console.WriteLine("[Клиент] Работа завершена.");
    }
}
