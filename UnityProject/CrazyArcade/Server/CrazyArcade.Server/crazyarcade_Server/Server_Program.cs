using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using static NetworkPacket;

class Server_Program
{
    static bool gameStarted = false;      // 실제 게임 시작 여부
    static bool countdownRunning = false; // 카운트다운 중인지
    private static ConcurrentDictionary<ulong, ClientConnection> clients = new();
    private static ulong nextPlayerId = 1;
    
    private static int maxPlayers = 2;
    private static ConcurrentDictionary<string, BalloonInfo> activeBalloons = new();

    // ★ 맵 데이터
    private static HashSet<string> wallTiles = new();
    private static HashSet<string> blockTiles = new();
    private static Random random = new Random();

    // ★ 게임 타이머 (180초 = 3분)
    private static float gameTime = 180f;
    private static bool gameEnded = false;
    static async Task HandlePlayerMove(ClientConnection connection, byte[] data)
    {
        var packet = PacketSerializer.Deserialize<PlayerMovePacket>(data);

        // 죽었거나 갇히면 이동 무시
        if (connection.IsDead || connection.IsTrapped)
            return;

        // ⭐ 서버 상태 즉시 갱신 (제일 중요)
        connection.PlayerState.GridPos = packet.TargetGridPos;

        // ⭐ 이동 직후 아이템 판정 (같은 타이밍!)
        await CheckItemPickup(connection);

        // ⭐ 상태 브로드캐스트
        var statePacket = new PlayerStatePacket
        {
            Player = connection.PlayerState
        };

        await BroadcastPacket(statePacket);
    }

    static async Task Main(string[] args)
    {

        LoadMap();  // ★ 여기에 추가
        TcpListener server = new TcpListener(IPAddress.Any, 12345);
        server.Start();
        Console.WriteLine("크레이지 아케이드 서버 시작! 포트 12345");
        Console.WriteLine($"최대 플레이어: {maxPlayers}명");

        // ★ 게임 타이머 시작
        _ = Task.Run(GameLoop);

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();

            if (gameStarted || clients.Count >= maxPlayers)
            {
                Console.WriteLine($"[거부] 게임 진행 중이거나 인원 초과");
                client.Close();
                continue;
            }

            ulong playerId = nextPlayerId++;
            Console.WriteLine($"[접속] 플레이어 {playerId}");

            var connection = new ClientConnection(playerId, client);
            //임시
            connection.PlayerState = new PlayerState
            {
                PlayerId = playerId,
                GridPos = new Int2(0, 0), // 임시값
                MoveState = PlayerMoveState.Idle,
                BaseState = BaseState.Normal,
                Stats = new PlayerStats()
            };


            clients.TryAdd(playerId, connection);



            if (clients.Count == maxPlayers && !countdownRunning)
            {
                //gameStarted = true;
                //gameTime = 180f;  // 타이머 리셋
                //gameEnded = false;
                countdownRunning = true;
                Console.WriteLine("[COUNTDOWN] Start");
                _ = Task.Run(StartCountdown);
                Console.WriteLine("[게임 시작!] 모든 플레이어 접속 완료");
            }

            _ = Task.Run(() => HandleClient(connection));
        }
    }
    static async Task StartCountdown()
    {
        int count = 10;

        while (count > 0)
        {
            var packet = new GameStartCountdownPacket
            {
                Remaining = count
            };

            await BroadcastPacket(packet);
            Console.WriteLine($"[COUNTDOWN] {count}");

            await Task.Delay(1000);
            count--;
        }

        // 카운트 끝
        countdownRunning = false;
        gameStarted = true;
        gameTime = 180f;

        await BroadcastPacket(new GameStartPacket());

        Console.WriteLine("[GAME] START!");
    }
    // ★ 게임 루프 (타이머 관리)
    static async Task GameLoop()
    {
        while (true)
        {
            await Task.Delay(1000);  // 1초마다

            if (gameStarted && !gameEnded)
            {
                float deltaTime = 1f;
                gameTime -= 1f;

                // 타이머 동기화 (1초마다)
                var timerPacket = new GameTimerPacket
                {
                    RemainingTime = gameTime
                };
                await BroadcastPacket(timerPacket);

                Console.WriteLine($"[타이머] 남은 시간: {(int)gameTime}초");
                // ★ 추가: 갇힌 플레이어 타이머
                foreach (var client in clients.Values)
                {
                    if (client.IsTrapped && !client.IsDead)
                    {
                        client.TrappedTimer += deltaTime;

                        if (client.TrappedTimer >= client.TrappedDuration)
                        {
                            await KillPlayer(client.PlayerId);
                            Console.WriteLine($"[죽음] 플레이어 {client.PlayerId} 시간 초과!");
                        }
                    }
                }

                // ★ 추가: 충돌 체크
                await CheckCollision();
                // 시간 종료
                if (gameTime <= 0)
                {
                    await HandleTimeOut();
                }
            }
        }
    }
    // ★ 충돌 체크
    static async Task CheckCollision()
    {
        foreach (var trapped in clients.Values.Where(c => c.IsTrapped && !c.IsDead))
        {
            foreach (var other in clients.Values.Where(c => c.PlayerId != trapped.PlayerId && !c.IsDead && !c.IsTrapped))
            {
                // 같은 위치에 있으면
                if (trapped.PlayerState.GridPos.X == other.PlayerState.GridPos.X &&
                    trapped.PlayerState.GridPos.Y == other.PlayerState.GridPos.Y)
                {
                    // 1vs1이므로 무조건 다른 팀 = 죽음
                    await KillPlayer(trapped.PlayerId);
                    Console.WriteLine($"[킬!] 플레이어 {other.PlayerId}가 플레이어 {trapped.PlayerId}를 처치!");
                }
            }
        }
    }
    // ★ 플레이어 죽음
    static async Task KillPlayer(ulong victimId)
    {
        if (!clients.TryGetValue(victimId, out var victim))
            return;

        victim.IsDead = true;
        victim.IsTrapped = false;
        victim.PlayerState.BaseState = BaseState.Dead;

        var diePacket = new PlayerDiePacket
        {
            PlayerId = victimId
        };
        await BroadcastPacket(diePacket);

        await CheckGameOver();
    }
    // ★ 시간 종료 처리
    static async Task HandleTimeOut()
    {
        gameEnded = true;

        // 생존자 확인
        var alivePlayers = clients.Values.Where(c => !c.IsDead).ToList();

        if (alivePlayers.Count == 1)
        {
            var winner = alivePlayers[0];
            var gameOverPacket = new GameOverPacket
            {
                WinnerPlayerId = winner.PlayerId,
                Reason = "timeout"
            };
            await BroadcastPacket(gameOverPacket);
            Console.WriteLine($"[게임 종료] 시간 종료 - 플레이어 {winner.PlayerId} 승리!");
        }
        else if (alivePlayers.Count > 1)
        {
            // 무승부
            var gameOverPacket = new GameOverPacket
            {
                WinnerPlayerId = 0,
                Reason = "draw"
            };
            await BroadcastPacket(gameOverPacket);
            Console.WriteLine($"[게임 종료] 시간 종료 - 무승부!");
        }
    }
    private static ConcurrentDictionary<string, ItemInfo> activeItems = new();
    private static int nextItemId = 1;
    // ★ JSON 구조
    class TilePos
    {
        public int x { get; set; }
        public int y { get; set; }
    }

    class MapData
    {
        public List<TilePos> walls { get; set; }
        public List<TilePos> blocks { get; set; }
    }

    // ★ 맵 로드
    static void LoadMap()
    {
        try
        {
            string json = File.ReadAllText("map.json");
            var mapData = JsonSerializer.Deserialize<MapData>(json);

            wallTiles.Clear();
            blockTiles.Clear();

            foreach (var wall in mapData.walls)
                wallTiles.Add($"{wall.x},{wall.y}");

            foreach (var block in mapData.blocks)
                blockTiles.Add($"{block.x},{block.y}");

            Console.WriteLine($"[맵] 벽 {wallTiles.Count}개, 블록 {blockTiles.Count}개 로드됨");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[오류] 맵 로드 실패: {ex.Message}");
        }
    }

    // ★ 블록 파괴 + 아이템 드롭
    static async Task DestroyBlock(Int2 gridPos)
    {
        string key = $"{gridPos.X},{gridPos.Y}";

        if (blockTiles.Remove(key))
        {
            Console.WriteLine($"[블록] 파괴: ({gridPos.X}, {gridPos.Y})");

            // ★ 클라에 블록 파괴 알림
            var destroyPacket = new BlockDestroyPacket
            {
                GridPos = gridPos
            };
            await BroadcastPacket(destroyPacket);

            // 30% 확률로 아이템 드롭
            if (random.Next(100) < 100)
            {
                ItemType itemType = GetRandomItemType();
                await SpawnItem(gridPos, itemType);
            }
        }
    }

    // ★ 랜덤 아이템 타입
    static ItemType GetRandomItemType()
    {
        int roll = random.Next(100);

        if (roll < 22) return ItemType.Balloon;
        else if (roll < 44) return ItemType.Potion;
        else if (roll < 62) return ItemType.Roller;
        else if (roll < 72) return ItemType.Needle;
        else if (roll < 82) return ItemType.Kick;
        else if (roll < 90) return ItemType.Glove;
        else return ItemType.Shark;
    }
    // ★ 아이템 정보 클래스 (BalloonInfo 근처에)
    class ItemInfo
    {
        public string ItemId { get; set; }
        public ItemType ItemType { get; set; }
        public Int2 GridPos { get; set; }
    }

    // ★ 아이템 스폰 (GameLoop이나 블록 파괴 시 호출)
    static async Task SpawnItem(Int2 gridPos, ItemType itemType)
    {
        string itemId = $"item_{nextItemId++}";

        var itemInfo = new ItemInfo
        {
            ItemId = itemId,
            ItemType = itemType,
            GridPos = gridPos
        };

        activeItems.TryAdd(itemId, itemInfo);

        var spawnPacket = new ItemSpawnPacket
        {
            ItemId = itemId,
            ItemType = itemType,
            GridPos = gridPos
        };

        await BroadcastPacket(spawnPacket);
        Console.WriteLine($"[아이템] {itemType} 스폰: ({gridPos.X}, {gridPos.Y})");
    }

    // ★ 아이템 획득 체크 (플레이어 이동 시 호출)
    static async Task CheckItemPickup(ClientConnection connection)
    {
        var playerPos = connection.PlayerState.GridPos;

        foreach (var item in activeItems.Values.ToList())
        {
            if (item.GridPos.X == playerPos.X && item.GridPos.Y == playerPos.Y)
            {
                // 아이템 제거
                if (!activeItems.TryRemove(item.ItemId, out _)) continue;

                // 효과 적용
                ApplyItemEffect(connection, item.ItemType);

                // 획득 패킷 전송
                var pickupPacket = new ItemPickupPacket
                {
                    ItemId = item.ItemId,
                    PlayerId = connection.PlayerId,
                    ItemType = item.ItemType
                };
                var statePacket = new PlayerStatePacket
                {
                    Player = connection.PlayerState
                };
                await BroadcastPacket(pickupPacket);
                await BroadcastPacket(statePacket);
                Console.WriteLine($"[아이템] 플레이어 {connection.PlayerId}가 {item.ItemType} 획득!");
            }
        }
    }

    // ★ 아이템 효과 적용
    static void ApplyItemEffect(ClientConnection connection, ItemType itemType)
    {
        var stats = connection.PlayerState.Stats;

        switch (itemType)
        {
            case ItemType.Balloon:
                if (stats.BalloonCount < 15)
                    stats.BalloonCount++;
                break;

            case ItemType.Potion:
                if (stats.BalloonRange < 10)  // 맵 크기에 맞게 조절
                    stats.BalloonRange++;
                break;

            case ItemType.Roller:
                if (stats.MoveCostTick > 1)
                    stats.MoveCostTick = Math.Max(1, stats.MoveCostTick - 1);  // 틱 감소 = 속도 증가
                break;

            case ItemType.Needle:
               // connection.PlayerState.Stats.NeedleCount++;
                stats.NeedleCount++;
                break;

            case ItemType.Kick:
                stats.HasKick = true;
                break;

            case ItemType.Glove:
                stats.HasGlove = true;
                break;

            case ItemType.Shark:
                stats.IsRidingShark = true;
                connection.PlayerState.BaseState = BaseState.Riding;
                break;
        }
    }
    // ★ 승패 판정
    static async Task CheckGameOver()
    {
        if (gameEnded) return;

        var alivePlayers = clients.Values.Where(c => !c.IsDead).ToList();

        if (alivePlayers.Count == 1)
        {
            gameEnded = true;
            var winner = alivePlayers[0];

            var gameOverPacket = new GameOverPacket
            {
                WinnerPlayerId = winner.PlayerId,
                Reason = "killed"
            };

            await BroadcastPacket(gameOverPacket);
            Console.WriteLine($"[게임 종료] 플레이어 {winner.PlayerId} 승리!");
        }
    }

    static async Task HandleClient(ClientConnection connection)
    {
        try
        {
            //await SendGameState(connection);

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

            if (clients.Count == 0)
            {
                Console.WriteLine("[초기화] 모든 플레이어 퇴장 - 게임 리셋");
                gameStarted = false;
                gameEnded = false;
                nextPlayerId = 1;
                gameTime = 180f;
                LoadMap();           // ★ 추가
                activeItems.Clear(); // ★ 추가
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
                case PacketType.Join:
                {
                       var packet = PacketSerializer.Deserialize<JoinPacket>(data);
                        connection.Nickname = packet.Nickname;
                        connection.PlayerState.Nickname = packet.Nickname;//  널 조심 
                        Console.WriteLine($"[JOIN] 플레이어 {connection.PlayerId} 닉네임: {packet.Nickname}");
                        await SendGameState(connection);

                        break;
                }
                case PacketType.UseNeedle:
                    Console.WriteLine($"[RECV] UseNeedle packet from {connection.PlayerId}");
                    await HandleUseNeedle(connection, data);
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
    /*
    static async Task HandlePlayerMove(ClientConnection connection, byte[] data)
    {
        var movePacket = PacketSerializer.Deserialize<PlayerMovePacket>(data);

        // ★ 죽은 플레이어는 이동 못함
        // ★ 죽었거나 갇히면 이동 못함
        if (connection.IsDead || connection.IsTrapped) return;

        connection.PlayerState.GridPos = movePacket.TargetGridPos;
        connection.PlayerState.TargetGridPos = null;
        connection.PlayerState.MoveState = PlayerMoveState.Idle;

        // ★ 아이템 획득 체크 추가
        await CheckItemPickup(connection);

        var statePacket = new PlayerStatePacket
        {
            Player = connection.PlayerState
        };

        await BroadcastPacket(statePacket);
    }
    */
    static async Task HandleUseNeedle(ClientConnection connection, byte[] data)
    {
        var packet = PacketSerializer.Deserialize<UseNeedlePacket>(data);

        // 1) 요청자가 본인인지 체크 (치트 방지)
        if (packet.PlayerId != connection.PlayerId)
        {
            Console.WriteLine($"[Needle] Invalid request: packet {packet.PlayerId} != conn {connection.PlayerId}");
            return;
        }

        // 2) 현재 상태 체크
        if (connection.IsDead) return;
        if (!connection.IsTrapped) return;

        // 3) 바늘 보유 체크 (서버 권한)
        var stats = connection.PlayerState?.Stats;
        if (stats == null) return;

        // 너는 Stats에 NeedleCount를 쓰고 있으니까 이거 기준
        if (stats.NeedleCount <= 0)
        {
            Console.WriteLine($"[Needle] Player {connection.PlayerId} has no needle");
            return;
        }

        // 4) 서버에서 감소 + 갇힘 해제
        stats.NeedleCount--;
        connection.IsTrapped = false;
        connection.TrappedTimer = 0f;
        connection.PlayerState.BaseState = BaseState.Normal;

        Console.WriteLine($"[Needle] Player {connection.PlayerId} used needle. remain={stats.NeedleCount}");

        // 5) 브로드캐스트 (모두에게 "누가 풀려났다" 알림)
        var rescuedPacket = new PlayerRescuedPacket
        {
            PlayerId = connection.PlayerId,
            RescuerId = connection.PlayerId
        };
        await BroadcastPacket(rescuedPacket);

        // (선택) 상태 패킷도 보내서 BaseState 동기화 확실히 하고 싶으면
        var statePacket = new PlayerStatePacket
        {
            Player = connection.PlayerState
        };
        await BroadcastPacket(statePacket);
    }


    static async Task SendGameState(ClientConnection connection)
    {
        if (connection.PlayerState == null)
        {
            Console.WriteLine($"[경고] PlayerState가 null이라 GameState 전송 스킵: {connection.PlayerId}");
            return;
        }

        Int2[] spawnPositions = new Int2[]
        {
            new Int2(-6, -5),
            new Int2(-4, -5),
            new Int2(-6, -3),
            new Int2(-4, -3)
        };

        int spawnIndex = ((int)(connection.PlayerId - 1)) % spawnPositions.Length;

        connection.PlayerState = new PlayerState
        {
            PlayerId = connection.PlayerId,
            Nickname = connection.Nickname,
            GridPos = spawnPositions[spawnIndex],
            MoveState = PlayerMoveState.Idle,
            BaseState = BaseState.Normal,
            Stats = new PlayerStats()
        };

        Console.WriteLine($"[상태] 플레이어 {connection.PlayerId}의 시작 위치: ({connection.PlayerState.GridPos.X}, {connection.PlayerState.GridPos.Y})");

        var allPlayers = clients.Values.Select(c => c.PlayerState).ToArray();

        var gameStatePacket = new GameStatePacket
        {
            MyPlayerId = connection.PlayerId,
            Players = allPlayers
        };

        byte[] data = PacketSerializer.Serialize(gameStatePacket);
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

    static async Task HandlePlaceBalloon(ClientConnection connection, byte[] data)
    {
        var packet = PacketSerializer.Deserialize<PlaceBalloonPacket>(data);

        string key = $"{packet.GridPos.X},{packet.GridPos.Y}";

        if (activeBalloons.ContainsKey(key))
        {
            Console.WriteLine($"[물풍선] 위치 ({packet.GridPos.X}, {packet.GridPos.Y})에 이미 존재");
            return;
        }

        Console.WriteLine($"[물풍선] 플레이어 {packet.PlayerId}가 ({packet.GridPos.X}, {packet.GridPos.Y})에 설치, 범위: {packet.Range}");

        var balloonInfo = new BalloonInfo
        {
            GridPos = packet.GridPos,
            Range = packet.Range,
            PlayerId = packet.PlayerId
        };

        activeBalloons.TryAdd(key, balloonInfo);

        await BroadcastPacket(packet);

        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            await ExplodeBalloon(packet.GridPos, packet.Range);
        });
    }

    // ★ 물풍선 폭발 처리 - 플레이어 죽음 체크 추가
    static async Task ExplodeBalloon(Int2 gridPos, int range)
    {
        string key = $"{gridPos.X},{gridPos.Y}";

        if (!activeBalloons.TryRemove(key, out _))
        {
            return;
        }

        Console.WriteLine($"[폭발] 물풍선 폭발: ({gridPos.X}, {gridPos.Y}), 범위: {range}");

        var affectedCells = new List<Int2>();
        affectedCells.Add(gridPos);

        // ★ 4방향 물줄기 (벽/블록에서 멈춤)
        Int2[] directions = { new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) };

        foreach (var dir in directions)
        {
            for (int i = 1; i <= range; i++)
            {
                Int2 cell = new Int2(gridPos.X + dir.X * i, gridPos.Y + dir.Y * i);
                string cellKey = $"{cell.X},{cell.Y}";

                // 벽이면 멈춤
                if (wallTiles.Contains(cellKey))
                    break;

                // 블록이면 파괴하고 멈춤
                if (blockTiles.Contains(cellKey))
                {
                    affectedCells.Add(cell);
                    await DestroyBlock(cell);
                    break;
                }

                affectedCells.Add(cell);
            }
        }

        // ★ 폭발 범위에 있는 플레이어 갇힘 처리
        foreach (var client in clients.Values)
        {
            if (client.IsDead || client.IsTrapped) continue;

            var playerPos = client.PlayerState.GridPos;

            foreach (var cell in affectedCells)
            {
                if (cell.X == playerPos.X && cell.Y == playerPos.Y)
                {
                    client.IsTrapped = true;
                    client.TrappedTimer = 0f;
                    client.PlayerState.BaseState = BaseState.Trapped;

                    await BroadcastPacket(new PlayerTrappedPacket
                    {
                        PlayerId = client.PlayerId
                    });

                    // 2️⃣ 상태 동기화 (⭐ 필수)
                    await BroadcastPacket(new PlayerStatePacket
                    {
                        Player = client.PlayerState
                    });
       
                    Console.WriteLine($"[갇힘] 플레이어 {client.PlayerId} 물풍선에 갇힘!");
                    break;
                }
            }
        }

        var explodePacket = new BalloonExplodePacket
        {
            GridPos = gridPos,
            AffectedCells = affectedCells.ToArray()
        };

        await BroadcastPacket(explodePacket);
    }

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
    public string Nickname { get; set; }   // ⭐ 추가
    public TcpClient Client { get; }
    public NetworkStream Stream { get; }
    public PlayerState PlayerState { get; set; }
    public bool IsConnected => Client.Connected;

    // ★ 추가
    // ★ 상태 필드 추가
    public bool IsDead { get; set; } = false;
    public bool IsTrapped { get; set; } = false;
    public float TrappedTimer { get; set; } = 0f;
    public float TrappedDuration { get; set; } = 30f;

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