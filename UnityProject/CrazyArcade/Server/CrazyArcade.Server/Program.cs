using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        TcpListener server = new TcpListener(IPAddress.Any, 12345);
        server.Start();
        Console.WriteLine("서버 시작! 포트 12345에서 대기 중...");

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            Console.WriteLine("클라이언트 접속!");

            _ = Task.Run(() => HandleClient(client));
        }
    }

    static async Task HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();

        // 환영 메시지 전송
        byte[] message = Encoding.UTF8.GetBytes("Hello from Server!");
        await stream.WriteAsync(message, 0, message.Length);
        Console.WriteLine("환영 메시지 전송 완료");

        // 클라이언트 메시지 받기
        byte[] buffer = new byte[1024];
        while (client.Connected)
        {
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;

            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"클라이언트로부터 받음: {receivedMessage}");

            // 에코 응답
            await stream.WriteAsync(buffer, 0, bytesRead);
        }

        client.Close();
        Console.WriteLine("클라이언트 연결 종료");
    }
}