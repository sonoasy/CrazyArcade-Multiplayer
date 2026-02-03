using UnityEngine;
using System.Net.Sockets;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine.Tilemaps;

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

        // 이 부분이 있나
        if (FindObjectOfType<UnityMainThreadDispatcher>() == null)
        {
            GameObject dispatcher = new GameObject("MainThreadDispatcher");
            dispatcher.AddComponent<UnityMainThreadDispatcher>();
            Debug.Log("[NetworkClient] Created MainThreadDispatcher");
        }

        if (groundTilemap == null)
            groundTilemap = GameObject.Find("Ground")?.GetComponent<Tilemap>();
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

                // ★ 이 부분이 빠져있었어요!
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

            Vector3Int gridPos = new Vector3Int(state.GridPos.X, state.GridPos.Y, 0);
            Vector3 worldPos;

            if (groundTilemap != null)
            {
                worldPos = groundTilemap.GetCellCenterWorld(gridPos);
                Debug.Log("[Spawn] Grid " + gridPos + " to World " + worldPos);
            }
            else
            {
                Debug.LogWarning("[Spawn] groundTilemap is null!");
                worldPos = new Vector3(state.GridPos.X, state.GridPos.Y, 0);
            }

            worldPos = new Vector3(-123, 18, 0);  // 파란색 위치로 강제 이동!

            GameObject go = Instantiate(playerPrefab, worldPos, Quaternion.identity);
            Debug.Log("[Spawn] GameObject created: " + go.name);

            // ★ PlayerMove 가져오거나 추가
            PlayerMove move = go.GetComponent<PlayerMove>();
            if (move == null)
            {
                move = go.AddComponent<PlayerMove>();
                Debug.Log("[Spawn] Added PlayerMove component");
            }

            // 초기화
            move.Initialize(state.PlayerId, state.PlayerId == myPlayerId);
            Debug.Log("[Spawn] PlayerMove initialized - ID: " + state.PlayerId + ", Local: " + (state.PlayerId == myPlayerId));

            allPlayers[state.PlayerId] = go;
            Debug.Log("[Spawn] Added to allPlayers. Total count: " + allPlayers.Count);
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