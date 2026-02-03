using UnityEngine;
using System.Net.Sockets;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine.Tilemaps;
using static NetworkPacket;

public class NetworkClient : MonoBehaviour
{
    public static NetworkClient Instance;
    private TcpClient client;
    private NetworkStream stream;
    public bool isConnected = false;
    private ulong myPlayerId;
    private Dictionary<ulong, GameObject> allPlayers = new Dictionary<ulong, GameObject>();

    public GameObject playerPrefab;
    public Tilemap groundTilemap;

    void Awake()
    {
        Debug.Log("[NetworkClient] Awake");
        if (Instance == null) Instance = this;

        if (FindObjectOfType<UnityMainThreadDispatcher>() == null)
        {
            GameObject dispatcher = new GameObject("MainThreadDispatcher");
            dispatcher.AddComponent<UnityMainThreadDispatcher>();
            Debug.Log("[NetworkClient] Created MainThreadDispatcher");
        }

        if (groundTilemap == null)
            groundTilemap = GameObject.Find("GroudTilemap")?.GetComponent<Tilemap>();
    }

    async void Start()
    {
        Debug.Log("[NetworkClient] Start");
        await ConnectToServer();
    }

    async Task ConnectToServer()
    {
        try
        {
            Debug.Log("[Connect] Trying to connect...");
            client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 12345);
            stream = client.GetStream();
            isConnected = true;
            Debug.Log("[Connect] Success!");
            _ = Task.Run(ReceivePackets);
        }
        catch (Exception e)
        {
            Debug.LogError("[Connect] Failed: " + e.Message);
            isConnected = false;
        }
    }

    async Task ReceivePackets()
    {
        byte[] buffer = new byte[8192];
        Debug.Log("[Receive] Waiting for packets...");

        while (isConnected && client != null && client.Connected)
        {
            try
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Debug.Log("[Receive] Server closed connection");
                    break;
                }

                byte[] data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);

                Debug.Log("[Receive] Packet received: " + bytesRead + " bytes");

                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    Debug.Log("[Receive] Processing on main thread");
                    ProcessPacket(data);
                });
            }
            catch (Exception e)
            {
                Debug.LogError("[Receive] Error: " + e.Message);
                break;
            }
        }

        isConnected = false;
        Debug.Log("[Receive] Loop ended");
    }

    void ProcessPacket(byte[] data)
    {
        try
        {
            Debug.Log("[Process] ProcessPacket started");
            string json = System.Text.Encoding.UTF8.GetString(data);
            Debug.Log("[Process] Received JSON: " + json);

            PacketType type = PacketSerializer.GetPacketType(data);
            Debug.Log("[Process] Packet type: " + type);

            if (type == PacketType.GameState)
            {
                Debug.Log("[Process] GameState packet");
                var packet = PacketSerializer.Deserialize<GameStatePacket>(data);

                Debug.Log("[Process] Deserialized - MyPlayerId: " + packet.MyPlayerId);
                Debug.Log("[Process] Deserialized - Players null? " + (packet.Players == null));

                if (packet.Players != null)
                {
                    Debug.Log("[Process] Players length: " + packet.Players.Length);
                }

                myPlayerId = packet.MyPlayerId;
                Debug.Log("[Process] My Player ID: " + myPlayerId);

                if (packet.Players == null)
                {
                    Debug.LogError("[Process] packet.Players is NULL!");
                    return;
                }

                Debug.Log("[Process] Total players: " + packet.Players.Length);

                foreach (var p in packet.Players)
                {
                    if (p == null)
                    {
                        Debug.LogError("[Process] Player in array is NULL!");
                        continue;
                    }

                    if (p.GridPos == null)
                    {
                        Debug.LogError("[Process] Player " + p.PlayerId + " has NULL GridPos!");
                        continue;
                    }

                    Debug.Log("[Process] Player info - ID: " + p.PlayerId + ", Pos: (" + p.GridPos.X + ", " + p.GridPos.Y + ")");
                    SpawnPlayer(p);
                }
            }
            else if (type == PacketType.PlayerState)
            {
                Debug.Log("[Process] PlayerState packet");
                var packet = PacketSerializer.Deserialize<PlayerStatePacket>(data);

                if (allPlayers.TryGetValue(packet.Player.PlayerId, out GameObject go))
                {
                    if (packet.Player.PlayerId != myPlayerId)
                    {
                        Vector3Int gridPos = new Vector3Int(packet.Player.GridPos.X, packet.Player.GridPos.Y, 0);
                        if (groundTilemap != null)
                        {
                            go.transform.position = groundTilemap.GetCellCenterWorld(gridPos);
                        }
                        else
                        {
                            go.transform.position = new Vector3(packet.Player.GridPos.X, packet.Player.GridPos.Y, 0);
                        }
                        Debug.Log("[Process] Updated player " + packet.Player.PlayerId + " position");
                    }
                }
                else
                {
                    Debug.Log("[Process] New player needs to be spawned: " + packet.Player.PlayerId);
                    SpawnPlayer(packet.Player);
                }
            }
            else if (type == PacketType.Disconnect)
            {
                var packet = PacketSerializer.Deserialize<DisconnectPacket>(data);
                if (allPlayers.TryGetValue(packet.PlayerId, out GameObject go))
                {
                    Destroy(go);
                    allPlayers.Remove(packet.PlayerId);
                    Debug.Log("[Process] Player disconnected: " + packet.PlayerId);
                }

            }
            else if (type == PacketType.PlaceBalloon)
            {
                var packet = PacketSerializer.Deserialize<PlaceBalloonPacket>(data);
                Debug.Log($"[Balloon] Received PlaceBalloon - Player: {packet.PlayerId}, Pos: ({packet.GridPos.X}, {packet.GridPos.Y}), Range: {packet.Range}");

                // 다른 플레이어의 물풍선만 생성 (내 것은 이미 로컬에서 생성했음)
                if (packet.PlayerId != myPlayerId)
                {
                    Vector3Int gridPos = new Vector3Int(packet.GridPos.X, packet.GridPos.Y, 0);
                    WaterBalloonManager balloonMgr = FindObjectOfType<WaterBalloonManager>();

                    if (balloonMgr != null)
                    {
                        // 다른 플레이어 물풍선이므로 PlayerMove는 null
                        balloonMgr.PlaceBalloon(gridPos, packet.Range, null);
                        Debug.Log($"[Balloon] Other player's balloon placed at ({packet.GridPos.X}, {packet.GridPos.Y})");
                    }
                    else
                    {
                        Debug.LogError("[Balloon] WaterBalloonManager not found!");
                    }
                }
                else
                {
                    Debug.Log($"[Balloon] My balloon, skip (already placed locally)");
                }
            } //물풍선 폭팔
            else if (type == PacketType.BalloonExplode)
            {
                var packet = PacketSerializer.Deserialize<BalloonExplodePacket>(data);
                Debug.Log($"[Explode] Balloon exploded at ({packet.GridPos.X}, {packet.GridPos.Y})");

                // 폭발 영향 받은 셀들 로그
                if (packet.AffectedCells != null)
                {
                    Debug.Log($"[Explode] Affected cells: {packet.AffectedCells.Length}");
                    foreach (var cell in packet.AffectedCells)
                    {
                        Debug.Log($"[Explode] Cell: ({cell.X}, {cell.Y})");
                    }
                }

                // TODO: 여기에 실제 폭발 처리 추가 (타일 파괴 등)
                // 지금은 서버에서 관리하고 있으니 로그만 출력
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[Process] Error: " + e.Message + "\n" + e.StackTrace);
        }
    }

    void SpawnPlayer(PlayerState state)
    {
        try
        {
            Debug.Log("[Spawn] SpawnPlayer called for ID: " + state.PlayerId);

            if (allPlayers.ContainsKey(state.PlayerId))
            {
                Debug.Log("[Spawn] Player already exists: " + state.PlayerId);
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[Spawn] playerPrefab is null!");
                return;
            }

            // 플레이어별 강제 월드 좌표
            Vector3 worldPos;
            if (state.PlayerId == 1)
            {
                worldPos = new Vector3(-128, 6, 0);  // 플레이어 1: 왼쪽 하단
                Debug.Log("[Spawn] Player 1 at: " + worldPos);
            }
            else if (state.PlayerId == 2)
            {
                worldPos = new Vector3(643, 350, 0);  // 플레이어 2: 오른쪽 상단
                Debug.Log("[Spawn] Player 2 at: " + worldPos);
            }
            else if (state.PlayerId == 3)
            {
                worldPos = new Vector3(-128, 350, 0);  // 플레이어 3: 왼쪽 상단
                Debug.Log("[Spawn] Player 3 at: " + worldPos);
            }
            else if (state.PlayerId == 4)
            {
                worldPos = new Vector3(643, 6, 0);  // 플레이어 4: 오른쪽 하단
                Debug.Log("[Spawn] Player 4 at: " + worldPos);
            }
            else
            {
                Vector3Int gridPos = new Vector3Int(state.GridPos.X, state.GridPos.Y, 0);
                if (groundTilemap != null)
                {
                    worldPos = groundTilemap.GetCellCenterWorld(gridPos);
                }
                else
                {
                    worldPos = new Vector3(state.GridPos.X, state.GridPos.Y, 0);
                }
            }

            GameObject go = Instantiate(playerPrefab, worldPos, Quaternion.identity);
            Debug.Log("[Spawn] GameObject created: " + go.name);

            PlayerMove move = go.GetComponent<PlayerMove>();
            if (move == null)
            {
                move = go.AddComponent<PlayerMove>();
                Debug.Log("[Spawn] Added PlayerMove component");
            }

            move.Initialize(state.PlayerId, state.PlayerId == myPlayerId);
            Debug.Log("[Spawn] PlayerMove initialized - ID: " + state.PlayerId + ", Local: " + (state.PlayerId == myPlayerId));

            // 플레이어 ID별 고정 색상
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (state.PlayerId == 1)
                {
                    sr.color = Color.red;    // 플레이어 1: 빨강
                    Debug.Log("[Spawn] Player 1 (Red)");
                }
                else if (state.PlayerId == 2)
                {
                    sr.color = Color.yellow; // 플레이어 2: 노랑
                    Debug.Log("[Spawn] Player 2 (Yellow)");
                }
                else if (state.PlayerId == 3)
                {
                    sr.color = Color.blue;   // 플레이어 3: 파랑
                    Debug.Log("[Spawn] Player 3 (Blue)");
                }
                else if (state.PlayerId == 4)
                {
                    sr.color = Color.green;  // 플레이어 4: 초록
                    Debug.Log("[Spawn] Player 4 (Green)");
                }
                else
                {
                    sr.color = Color.white;  // 5번째 이후: 하양
                }
            }

            allPlayers[state.PlayerId] = go;
            Debug.Log("[Spawn] Total players: " + allPlayers.Count);
        }
        catch (Exception e)
        {
            Debug.LogError("[Spawn] Error: " + e.Message + "\n" + e.StackTrace);
        }
    }

    public async void SendMyMove(Int2 pos)
    {
        if (!isConnected || stream == null)
        {
            Debug.LogWarning("[Send] Not connected");
            return;
        }

        try
        {
            var packet = new PlayerMovePacket { PlayerId = myPlayerId, TargetGridPos = pos };
            byte[] data = PacketSerializer.Serialize(packet);
            await stream.WriteAsync(data, 0, data.Length);
            Debug.Log("[Send] Move packet sent: (" + pos.X + ", " + pos.Y + ")");
        }
        catch (Exception e)
        {
            Debug.LogError("[Send] Failed: " + e.Message);
            isConnected = false;
        }
    }
    public async void SendPlaceBalloon(Vector3Int gridPos, int range)
    {
        if (!isConnected || stream == null)
        {
            Debug.LogWarning("[Send] Not connected");
            return;
        }

        try
        {
            var packet = new PlaceBalloonPacket
            {
                PlayerId = myPlayerId,
                GridPos = new Int2(gridPos.x, gridPos.y),
                Range = range
            };

            byte[] data = PacketSerializer.Serialize(packet);
            await stream.WriteAsync(data, 0, data.Length);
            Debug.Log($"[Send] PlaceBalloon sent: ({gridPos.x}, {gridPos.y}), Range: {range}");
        }
        catch (Exception e)
        {
            Debug.LogError("[Send] PlaceBalloon failed: " + e.Message);
            isConnected = false;
        }
    }
    void OnApplicationQuit()
    {
        isConnected = false;
        stream?.Close();
        client?.Close();
        Debug.Log("[App] Quit");
    }
}

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher _instance;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
                _executionQueue.Dequeue().Invoke();
        }
    }

    public static void Enqueue(Action action)
    {
        lock (_executionQueue)
            _executionQueue.Enqueue(action);
    }
}