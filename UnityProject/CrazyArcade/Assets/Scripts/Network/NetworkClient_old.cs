using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class NetworkClient_old : MonoBehaviour
{
    private TcpClient client;
    private NetworkStream stream;
    private bool isConnected = false;

    async void Start()
    {
        await ConnectToServer();
    }

    async Task ConnectToServer()
    {
        try
        {
            client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 12345);
            stream = client.GetStream();
            isConnected = true;

            Debug.Log("서버 연결 성공!");

            _ = Task.Run(ReceiveMessages);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"서버 연결 실패: {e.Message}");
        }
    }

    async Task ReceiveMessages()
    {
        byte[] buffer = new byte[1024];

        while (isConnected && client.Connected)
        {
            try
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Debug.Log($"서버에서 받음: {message}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"수신 오류: {e.Message}");
                break;
            }
        }
    }

    public async void SendMessage(string message)
    {
        if (!isConnected || stream == null) return;

        byte[] data = Encoding.UTF8.GetBytes(message);
        await stream.WriteAsync(data, 0, data.Length);
        Debug.Log($"서버로 전송: {message}");
    }

    void OnDestroy()
    {
        isConnected = false;
        stream?.Close();
        client?.Close();
    }
}