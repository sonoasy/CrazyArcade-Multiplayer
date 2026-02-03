using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static NetworkPacket;

class Server_Program
{
    private static ConcurrentDictionary<ulong, ClientConnection> clients = new();
    private static ulong nextPlayerId = 1;

    // ★ 추가: 게임 상태 관리
    private static bool gameStarted = false;
    private static int maxPlayers = 2;  // 최대 2명

    private static ConcurrentDictionary<string, BalloonInfo> activeBalloons = new();

    static async Task Main(string[] args)
    {
        TcpListener server = new TcpListener(IPAddress.Any, 12345);
        server.Start();
        Console.WriteLine("크레이지 아케이드 서버 시작! 포트 12345");
        Console.WriteLine($"최대 플레이어: {maxPlayers}명");

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();

            // ★ 추가: 게임 진행 중이거나 인원 꽉 찼으면 거부
            if (gameStarted || clients.Count >= maxPlayers)
            {
                Console.WriteLine($"[거부] 게임 진행 중이거나 인원 초과 (현재: {clients.Count}/{maxPlayers})");
                client.Close();
                continue;
            }

            ulong playerId = nextPlayerId++;
            Console.WriteLine($"[접속] 플레이어 {playerId} (현재 인원: {clients.Count + 1}/{maxPlayers})");

            var connection = new ClientConnection(playerId, client);
            clients.TryAdd(playerId, connection);

            // ★ 추가: 2명 모이면 게임 시작
            if (clients.Count == maxPlayers)
            {
                gameStarted = true;
                Console.WriteLine("[게임 시작!] 모든 플레이어 접속 완료");
            }

            _ = Task.Run(() => HandleClient(connection));
        }
    }

    static async Task HandleClient(ClientConnection connection)
    {
        try
        {
            await SendGameState(connection);

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

            clients.TryRemove(connection.PlayerId, out _);
            connection.Disconnect();

            var disconnectPacket = new DisconnectPacket
            {
                PlayerId = connection.PlayerId
            };
            await BroadcastPacket(disconnectPacket, connection.PlayerId);

            // ★ 추가: 모든 플레이어 퇴장 시 게임 리셋
            if (clients.Count == 0)
            {
                Console.WriteLine("[초기화] 모든 플레이어 퇴장 - 게임 리셋, ID 초기화");
                gameStarted = false;
                nextPlayerId = 1;  // ID를 1로 초기화!
            }
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
                case PacketType.PlaceBalloon:
                    await HandlePlaceBalloon(connection, data);
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
        string json = Encoding.UTF8.GetString(data);
        Console.WriteLine($"[디버그] 받은 JSON: {json}");

        var movePacket = PacketSerializer.Deserialize<PlayerMovePacket>(data);

        Console.WriteLine($"[디버그] PlayerId: {movePacket.PlayerId}");
        Console.WriteLine($"[디버그] TargetGridPos.X: {movePacket.TargetGridPos.X}");
        Console.WriteLine($"[디버그] TargetGridPos.Y: {movePacket.TargetGridPos.Y}");

        Console.WriteLine($"[이동] 플레이어 {movePacket.PlayerId}: ({movePacket.TargetGridPos.X}, {movePacket.TargetGridPos.Y})");

        connection.PlayerState.GridPos = movePacket.TargetGridPos;
        connection.PlayerState.TargetGridPos = null;
        connection.PlayerState.MoveState = PlayerMoveState.Idle;

        var statePacket = new PlayerStatePacket
        {
            Player = connection.PlayerState
        };

        await BroadcastPacket(statePacket);
    }

    static async Task SendGameState(ClientConnection connection)
    {
        Int2[] spawnPositions = new Int2[]
        {
            new Int2(-6, -5),   // 플레이어 1
            new Int2(-4, -5),   // 플레이어 2
            new Int2(-6, -3),   // 플레이어 3
            new Int2(-4, -3)    // 플레이어 4
        };

        int spawnIndex = ((int)(connection.PlayerId - 1)) % spawnPositions.Length;

        connection.PlayerState = new PlayerState
        {
            PlayerId = connection.PlayerId,
            GridPos = spawnPositions[spawnIndex],
            MoveState = PlayerMoveState.Idle,
            BaseState = BaseState.Normal,
            Stats = new PlayerStats()
        };

        Console.WriteLine($"[상태] 플레이어 {connection.PlayerId}의 시작 위치: ({connection.PlayerState.GridPos.X}, {connection.PlayerState.GridPos.Y})");

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

        var gameStatePacket = new GameStatePacket
        {
            MyPlayerId = connection.PlayerId,
            Players = allPlayers
        };

        byte[] data = PacketSerializer.Serialize(gameStatePacket);
        Console.WriteLine($"[디버그] 직렬화된 데이터 크기: {data.Length} bytes");

        await connection.Stream.WriteAsync(data, 0, data.Length);

        Console.WriteLine($"[전송] 플레이어 {connection.PlayerId}에게 게임 상태 전송");

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
    // ★ 물풍선 설치 처리
    static async Task HandlePlaceBalloon(ClientConnection connection, byte[] data)
    {
        var packet = PacketSerializer.Deserialize<PlaceBalloonPacket>(data);

        string key = $"{packet.GridPos.X},{packet.GridPos.Y}";

        // 이미 물풍선이 있으면 무시
        if (activeBalloons.ContainsKey(key))
        {
            Console.WriteLine($"[물풍선] 위치 ({packet.GridPos.X}, {packet.GridPos.Y})에 이미 존재");
            return;
        }

        Console.WriteLine($"[물풍선] 플레이어 {packet.PlayerId}가 ({packet.GridPos.X}, {packet.GridPos.Y})에 설치, 범위: {packet.Range}");

        // 물풍선 정보 저장
        var balloonInfo = new BalloonInfo
        {
            GridPos = packet.GridPos,
            Range = packet.Range,
            PlayerId = packet.PlayerId
        };

        activeBalloons.TryAdd(key, balloonInfo);

        // 모든 클라이언트에게 브로드캐스트
        await BroadcastPacket(packet);

        // 3초 후 폭발 예약
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            await ExplodeBalloon(packet.GridPos, packet.Range);
        });
    }

    // ★ 물풍선 폭발 처리
    static async Task ExplodeBalloon(Int2 gridPos, int range)
    {
        string key = $"{gridPos.X},{gridPos.Y}";

        if (!activeBalloons.TryRemove(key, out _))
        {
            Console.WriteLine($"[폭발] 물풍선이 이미 제거됨: ({gridPos.X}, {gridPos.Y})");
            return;
        }

        Console.WriteLine($"[폭발] 물풍선 폭발: ({gridPos.X}, {gridPos.Y}), 범위: {range}");

        // 폭발 범위 계산 (4방향)
        var affectedCells = new List<Int2>();
        affectedCells.Add(gridPos); // 중심

        // 상하좌우로 range만큼
        for (int i = 1; i <= range; i++)
        {
            affectedCells.Add(new Int2(gridPos.X + i, gridPos.Y)); // 우
            affectedCells.Add(new Int2(gridPos.X - i, gridPos.Y)); // 좌
            affectedCells.Add(new Int2(gridPos.X, gridPos.Y + i)); // 상
            affectedCells.Add(new Int2(gridPos.X, gridPos.Y - i)); // 하
        }

        // 폭발 패킷 전송
        var explodePacket = new BalloonExplodePacket
        {
            GridPos = gridPos,
            AffectedCells = affectedCells.ToArray()
        };

        await BroadcastPacket(explodePacket);
    }

    // ★ 물풍선 정보 클래스
    class BalloonInfo
    {
        public Int2 GridPos { get; set; }
        public int Range { get; set; }
        public ulong PlayerId { get; set; }
    }
}

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