using UnityEngine;
using System.Net.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using UnityEngine.Tilemaps;
using static NetworkPacket;

public class NetworkClient : MonoBehaviour
{
    public string myNickname = "Player";
    public static NetworkClient Instance;
    private TcpClient client;
    private NetworkStream stream;
    public bool isConnected = false;
    private ulong myPlayerId;
    private Dictionary<ulong, GameObject> allPlayers = new Dictionary<ulong, GameObject>();

    public GameObject playerPrefab;
    public Tilemap groundTilemap;
    public TextMeshProUGUI timerText;
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
        
        DontDestroyOnLoad(gameObject);
        myNickname = PlayerPrefs.GetString("NICKNAME", "Player");
        Debug.Log("My Nickname: " + myNickname);

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

            // ⭐ 여기서 닉네임 전송
            var joinPacket = new JoinPacket
            {
                Nickname = myNickname
            };
            byte[] joinData = PacketSerializer.Serialize(joinPacket);
            await stream.WriteAsync(joinData, 0, joinData.Length);
            Debug.Log("[Connect] JoinPacket sent: " + myNickname);

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
        string leftover = "";  // ★ 남은 데이터 저장
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
                // ★ 받은 데이터를 문자열로 변환
                string received = leftover + System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                leftover = "";
                // ★ 여러 JSON 패킷 분리
                List<string> packets = new List<string>();
                int depth = 0;
                int start = 0;
                for (int i = 0; i < received.Length; i++)
                {
                    if (received[i] == '{') depth++;
                    else if (received[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            packets.Add(received.Substring(start, i - start + 1));
                            start = i + 1;
                        }
                    }
                }

                // 남은 불완전한 데이터 저장
                if (start < received.Length)
                {
                    leftover = received.Substring(start);
                }

                // ★ 각 패킷 처리
                foreach (string packetJson in packets)
                {
                    byte[] data = System.Text.Encoding.UTF8.GetBytes(packetJson);
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        ProcessPacket(data);
                    });
                }
                // byte[] data = new byte[bytesRead];
                //Array.Copy(buffer, data, bytesRead);

                //   Debug.Log("[Receive] Packet received: " + bytesRead + " bytes");

                //  UnityMainThreadDispatcher.Enqueue(() =>
                //  {
                //      Debug.Log("[Receive] Processing on main thread");
                //      ProcessPacket(data);
                //  });
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
                    if (PlayerListUI.Instance != null)
                    {
                        Debug.Log($"[UI] AddPlayer call: {p.PlayerId}, {p.Nickname}");
                        PlayerListUI.Instance.AddPlayer(p.PlayerId, p.Nickname);
                    }
                }
            }
            else if (type == PacketType.PlayerState)
            {
                Debug.Log("[Process] PlayerState packet");
                var packet = PacketSerializer.Deserialize<PlayerStatePacket>(data);
                var p = packet.Player;
                if (allPlayers.TryGetValue(packet.Player.PlayerId, out GameObject go))
                {
                   // if (packet.Player.PlayerId != myPlayerId)
                   // {

                        // ★ 내 플레이어면 스탯 업데이트
                        if (packet.Player.PlayerId == myPlayerId)
                        {
                        

                        PlayerMove move = go.GetComponent<PlayerMove>();
                            if (move != null && packet.Player.Stats != null)
                            {
                                move.UpdateStats(packet.Player.Stats);
                            }

                        }


                        Vector3Int gridPos = new Vector3Int(packet.Player.GridPos.X, packet.Player.GridPos.Y, 0);
                        Vector3 targetPos;  // ★ 추가

                        if (groundTilemap != null)
                        {
                            //go.transform.position
                            targetPos = groundTilemap.GetCellCenterWorld(gridPos);
                        }
                        else
                        {
                            //go.transform.position =
                            targetPos = new Vector3(packet.Player.GridPos.X, packet.Player.GridPos.Y, 0);
                        }
                        // Debug.Log("[Process] Updated player " + packet.Player.PlayerId + " position");
                        RemotePlayerController remoteController = go.GetComponent<RemotePlayerController>();
                        if (remoteController != null)
                        {
                            remoteController.SetTargetPosition(targetPos);
                            Debug.Log($"[Process] Smooth move to {targetPos}");
                        }
                        else
                        {
                            go.transform.position = targetPos;
                            Debug.LogWarning("[Process] No RemotePlayerController, instant move");
                        }
                    //}
                }
                else
                {
                    Debug.Log("[Process] New player needs to be spawned: " + packet.Player.PlayerId);
                    SpawnPlayer(packet.Player);
                    if (PlayerListUI.Instance != null)
                    {
                        PlayerListUI.Instance.AddPlayer(p.PlayerId, p.Nickname);
                    }
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
            }// ★ 블록 파괴
            else if (type == PacketType.BlockDestroy)
            {
                var packet = PacketSerializer.Deserialize<BlockDestroyPacket>(data);
                Debug.Log($"[Block] Destroyed at ({packet.GridPos.X}, {packet.GridPos.Y})");

                // 타일 지우기
                WaterBalloonManager balloonMgr = FindObjectOfType<WaterBalloonManager>();
                if (balloonMgr != null && balloonMgr.objectTilemap != null)
                {
                    Vector3Int pos = new Vector3Int(packet.GridPos.X, packet.GridPos.Y, 0);
                    balloonMgr.objectTilemap.SetTile(pos, null);

                    // ★ 이펙트 추가
                    if (balloonMgr.waterEffectPrefab != null && balloonMgr.groundTilemap != null)
                    {
                        Vector3 worldPos = balloonMgr.groundTilemap.GetCellCenterWorld(pos);
                        GameObject water = Instantiate(balloonMgr.waterEffectPrefab, worldPos, Quaternion.identity);
                        water.transform.position = new Vector3(worldPos.x, worldPos.y, -0.5f);
                        Destroy(water, 0.5f);
                    }

                }
            }// ★ 아이템 스폰
            else if (type == PacketType.ItemSpawn)
            {
                var packet = PacketSerializer.Deserialize<ItemSpawnPacket>(data);
                Debug.Log($"[Item] Spawned {packet.ItemType} at ({packet.GridPos.X}, {packet.GridPos.Y})");

                // TODO: 아이템 오브젝트 생성
                // 아이템 오브젝트 생성
                if (ItemManager.Instance != null)
                {
                    ItemManager.Instance.SpawnItem(packet.ItemId, packet.ItemType, packet.GridPos);
                }
            }    // ★ 아이템 획득
            else if (type == PacketType.ItemPickup)
            {
                var packet = PacketSerializer.Deserialize<ItemPickupPacket>(data);
                Debug.Log($"[Item] Player {packet.PlayerId} picked up {packet.ItemType}");

                // TODO: 아이템 오브젝트 제거
                // 아이템 오브젝트 제거
                if (ItemManager.Instance != null)
                {
                    ItemManager.Instance.RemoveItem(packet.ItemId);
                }
                // ★ 내가 먹은 거면 스탯 업데이트
                if (packet.PlayerId == myPlayerId)
                {
                    if (allPlayers.TryGetValue(myPlayerId, out GameObject go))
                    {
                        PlayerMove move = go.GetComponent<PlayerMove>();
                        if (move != null)
                        {
                            // 아이템 타입에 따라 스탯 직접 증가
                            if (packet.ItemType == ItemType.Potion)
                            {
                                move.balloonRange++;
                                Debug.Log($"[Item] 물줄기 증가! 현재: {move.balloonRange}");
                            }
                            else if (packet.ItemType == ItemType.Balloon)
                            {
                                move.maxBalloons++;
                                Debug.Log($"[Item] 물풍선 개수 증가! 현재: {move.maxBalloons}");
                            }
                            else if (packet.ItemType == ItemType.Roller)
                            {
                                move.moveSpeed += 50f;
                                Debug.Log($"[Item] 속도 증가! 현재: {move.moveSpeed}");
                            }
                            else if (packet.ItemType == ItemType.Needle)
                            {
                                move.needleCount++;

                                if (NeedleUI.Instance != null)
                                {
                                    NeedleUI.Instance.SetCount(move.needleCount);
                                }

                                Debug.Log($"[Item] Needle 획득! 현재: {move.needleCount}");
                            }
                        }
                    }

                }
            }
            else if (type == PacketType.GameTimer)
            {
                Debug.Log("[Timer] GameTimer packet received!");  // ★ 추가

                var packet = PacketSerializer.Deserialize<GameTimerPacket>(data);

                Debug.Log($"[Timer] RemainingTime: {packet.RemainingTime}");  // ★ 추가

                int minutes = (int)packet.RemainingTime / 60;
                int seconds = (int)packet.RemainingTime % 60;

                Debug.Log($"[Timer] Formatted: {minutes:D2}:{seconds:D2}");  // ★ 추가

                if (timerText != null)
                {
                    Debug.Log("[Timer] Updating UI...");  // ★ 추가
                    timerText.text = $"TIMER:{minutes:D2}:{seconds:D2}";
                    Debug.Log($"[Timer] UI updated to: {timerText.text}");  // ★ 추가
                }
                else
                {
                    Debug.LogError("[Timer] timerText is NULL!");  // ★ 추가
                }
            }
            // ★ 플레이어 갇힘
            else if (type == PacketType.PlayerTrapped)
            {
                var packet = PacketSerializer.Deserialize<PlayerTrappedPacket>(data);

                if (allPlayers.TryGetValue(packet.PlayerId, out GameObject go))
                {
                    SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = Color.cyan;  // 청록색!
                    }

                    PlayerMove move = go.GetComponent<PlayerMove>();
                    if (move != null)
                    {
                        move.GetTrapped();
                    }

                    Debug.Log($"[Trapped] Player {packet.PlayerId} is trapped!");
                }
            }
            // ★ 플레이어 죽음
            else if (type == PacketType.PlayerDie)
            {
                var packet = PacketSerializer.Deserialize<PlayerDiePacket>(data);

                if (allPlayers.TryGetValue(packet.PlayerId, out GameObject go))
                {
                    SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = Color.gray;  // 회색으로 변경
                    }

                    PlayerMove move = go.GetComponent<PlayerMove>();
                    if (move != null)
                    {
                        move.Die();  // 움직임 차단
                    }

                    Debug.Log($"[Die] Player {packet.PlayerId} died!");
                }
            }
            // ★ 플레이어 구출 (바늘 사용)
            else if (type == PacketType.PlayerRescued)
            {
                var packet = PacketSerializer.Deserialize<PlayerRescuedPacket>(data);

                if (allPlayers.TryGetValue(packet.PlayerId, out GameObject go))
                {
                    PlayerMove move = go.GetComponent<PlayerMove>();
                    if (move != null)
                    {
                        move.Rescue();
                    }
                }

                // ★ 내가 구출된 경우 → UI 갱신
                if (packet.PlayerId == myPlayerId)
                {
                    if (allPlayers.TryGetValue(myPlayerId, out GameObject me))
                    {
                        PlayerMove move = me.GetComponent<PlayerMove>();
                        if (move != null && NeedleUI.Instance != null)
                        {
                            NeedleUI.Instance.SetCount(move.needleCount);
                        }
                    }
                }

                Debug.Log($"[Rescue] Player {packet.PlayerId} rescued by {packet.RescuerId}");
            }

            // ★ 게임 종료
            else if (type == PacketType.GameOver)
            {
                var packet = PacketSerializer.Deserialize<GameOverPacket>(data);

                if (packet.WinnerPlayerId == 0)
                {
                    Debug.Log("[Game Over] Draw!");
                    // TODO: 무승부 UI
                }
                else
                {
                    Debug.Log($"[Game Over] Player {packet.WinnerPlayerId} wins! ({packet.Reason})");
                    // TODO: 승리 UI
                }
            }// ★ 타이머 업데이트
            else if (type == PacketType.GameTimer)
            {
                var packet = PacketSerializer.Deserialize<GameTimerPacket>(data);

                int minutes = (int)packet.RemainingTime / 60;
                int seconds = (int)packet.RemainingTime % 60;

                // ★ UI 텍스트 업데이트
                if (timerText != null)
                {
                    timerText.text = $"TIMER:{minutes:D2}:{seconds:D2}";
                }

                Debug.Log($"[Timer] {minutes:D2}:{seconds:D2}");
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

            // ★ 추가: 원격 플레이어면 RemotePlayerController 추가
            if (state.PlayerId != myPlayerId)
            {
                RemotePlayerController remoteController = go.GetComponent<RemotePlayerController>();
                if (remoteController == null)
                {
                    remoteController = go.AddComponent<RemotePlayerController>();
                    Debug.Log("[Spawn] Added RemotePlayerController for remote player");
                }
                remoteController.SetTargetPosition(worldPos);
            }

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
    public async void SendUseNeedle()
    {
        if (!isConnected || stream == null)
        {
            Debug.LogWarning("[Send] Not connected");
            return;
        }

        try
        {
            var packet = new UseNeedlePacket
            {
                PlayerId = myPlayerId
            };

            byte[] data = PacketSerializer.Serialize(packet);
            await stream.WriteAsync(data, 0, data.Length);

            Debug.Log($"[Send] UseNeedle sent: PlayerId={myPlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError("[Send] UseNeedle failed: " + e.Message);
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