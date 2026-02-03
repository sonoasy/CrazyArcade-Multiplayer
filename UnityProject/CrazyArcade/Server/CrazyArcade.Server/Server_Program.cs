using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class Program
{
    // 연결된 클라이언트 관리
    private static ConcurrentDictionary<ulong, ClientConnection> clients = new();
    private static ulong nextPlayerId = 1;

    static async Task Main(string[] args)
    {
        TcpListener server = new TcpListener(IPAddress.Any, 12345);
        server.Start();
        Console.WriteLine("크레이지 아케이드 서버 시작! 포트 12345");

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            ulong playerId = nextPlayerId++;
            Console.WriteLine($"[접속] 플레이어 {playerId}");

            var connection = new ClientConnection(playerId, client);
            clients.TryAdd(playerId, connection);

            _ = Task.Run(() => HandleClient(connection));
        }
    }

    static async Task HandleClient(ClientConnection connection)
    {
        try
        {
            // 접속 환영 패킷 전송
            await SendGameState(connection);

            // 패킷 처리 루프
            byte[] buffer = new byte[4096];
            while (connection.IsConnected)
            {
                int bytesRead = await connection.Stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                byte[] packetData = new byte[bytesRead];
                Array.Copy(buffer, packetData, bytesRead);

                await ProcessPacket(connection, packetData);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[오류] 플레이어 {connection.PlayerId}: {ex.Message}");
        }
        finally
        {
            Console.WriteLine($"[퇴장] 플레이어 {connection.PlayerId}");

            // 연결 종료 처리
            clients.TryRemove(connection.PlayerId, out _);
            connection.Disconnect();

            // 다른 플레이어들에게 알림
            var disconnectPacket = new DisconnectPacket
            {
                PlayerId = connection.PlayerId
            };
            await BroadcastPacket(disconnectPacket, connection.PlayerId);
        }
    }

    static async Task ProcessPacket(ClientConnection connection, byte[] data)
    {
        try
        {
            PacketType type = PacketSerializer.GetPacketType(data);

            switch (type)
            {
                case PacketType.PlayerMove:
                    await HandlePlayerMove(connection, data);
                    break;

                default:
                    Console.WriteLine($"[경고] 알 수 없는 패킷 타입: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[오류] 패킷 처리 실패: {ex.Message}");
        }
    }

    static async Task HandlePlayerMove(ClientConnection connection, byte[] data)
    {
        // JSON 원본 출력
        string json = Encoding.UTF8.GetString(data);
        Console.WriteLine($"[디버그] 받은 JSON: {json}");

        var movePacket = PacketSerializer.Deserialize<PlayerMovePacket>(data);

        Console.WriteLine($"[디버그] PlayerId: {movePacket.PlayerId}");
        Console.WriteLine($"[디버그] TargetGridPos.X: {movePacket.TargetGridPos.X}");
        Console.WriteLine($"[디버그] TargetGridPos.Y: {movePacket.TargetGridPos.Y}");

        Console.WriteLine($"[이동] 플레이어 {movePacket.PlayerId}: ({movePacket.TargetGridPos.X}, {movePacket.TargetGridPos.Y})");

        // 플레이어 상태 업데이트
        connection.PlayerState.GridPos = movePacket.TargetGridPos;
        connection.PlayerState.TargetGridPos = null;
        connection.PlayerState.MoveState = PlayerMoveState.Idle;

        // 모든 클라이언트에게 브로드캐스트
        var statePacket = new PlayerStatePacket
        {
            Player = connection.PlayerState
        };

        await BroadcastPacket(statePacket);
    }

    static async Task SendGameState(ClientConnection connection)
    {
        // 새 플레이어 상태 초기화
        connection.PlayerState = new PlayerState
        {
            PlayerId = connection.PlayerId,
            GridPos = new Int2(1, 1),
            MoveState = PlayerMoveState.Idle,
            BaseState = BaseState.Normal,
            Stats = new PlayerStats()
        };

        Console.WriteLine($"[상태] 플레이어 {connection.PlayerId}의 현재 위치: ({connection.PlayerState.GridPos.X}, {connection.PlayerState.GridPos.Y})");

        // ★ 중요: 여기서 Players 배열 확인!
        var allPlayers = clients.Values.Select(c => c.PlayerState).ToArray();
        Console.WriteLine($"[디버그] 전송할 플레이어 수: {allPlayers.Length}");

        foreach (var p in allPlayers)
        {
            if (p == null)
            {
                Console.WriteLine($"[경고] PlayerState가 null인 클라이언트 발견!");
            }
            else
            {
                Console.WriteLine($"[디버그] 플레이어 {p.PlayerId} 포함 - 위치: ({p.GridPos.X}, {p.GridPos.Y})");
            }
        }

        // 현재 게임 상태 전송
        var gameStatePacket = new GameStatePacket
        {
            MyPlayerId = connection.PlayerId,
            Players = allPlayers
        };

        byte[] data = PacketSerializer.Serialize(gameStatePacket);
        Console.WriteLine($"[디버그] 직렬화된 데이터 크기: {data.Length} bytes");

        await connection.Stream.WriteAsync(data, 0, data.Length);

        Console.WriteLine($"[전송] 플레이어 {connection.PlayerId}에게 게임 상태 전송");

        // 기존 플레이어들에게 새 플레이어 알림
        var newPlayerPacket = new PlayerStatePacket
        {
            Player = connection.PlayerState
        };
        await BroadcastPacket(newPlayerPacket, connection.PlayerId);
    }
    static async Task BroadcastPacket<T>(T packet, ulong? excludePlayerId = null) where T : NetworkPacket
    {
        byte[] data = PacketSerializer.Serialize(packet);

        var tasks = new List<Task>();
        foreach (var client in clients.Values)
        {
            if (excludePlayerId.HasValue && client.PlayerId == excludePlayerId.Value)
                continue;

            tasks.Add(client.SendPacketAsync(data));
        }

        await Task.WhenAll(tasks);
    }
}

// 클라이언트 연결 정보
class ClientConnection
{
    public ulong PlayerId { get; }
    public TcpClient Client { get; }
    public NetworkStream Stream { get; }
    public PlayerState PlayerState { get; set; }
    public bool IsConnected => Client.Connected;

    public ClientConnection(ulong playerId, TcpClient client)
    {
        PlayerId = playerId;
        Client = client;
        Stream = client.GetStream();
    }

    public async Task SendPacketAsync(byte[] data)
    {
        try
        {
            if (IsConnected)
            {
                await Stream.WriteAsync(data, 0, data.Length);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[오류] 플레이어 {PlayerId} 전송 실패: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        try
        {
            Stream?.Close();
            Client?.Close();
        }
        catch { }
    }
}