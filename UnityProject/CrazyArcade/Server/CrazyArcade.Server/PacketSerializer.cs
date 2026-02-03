using System;
using System.Text;
using System.Text.Json;

public static class PacketSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        WriteIndented = false
    };

    // 패킷 → JSON → byte[]
    public static byte[] Serialize<T>(T packet) where T : NetworkPacket
    {
        string json = JsonSerializer.Serialize(packet, JsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    // byte[] → JSON → 패킷
    public static T Deserialize<T>(byte[] data) where T : NetworkPacket
    {
        string json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    // PacketType 먼저 읽기
    public static PacketType GetPacketType(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("Type", out JsonElement typeElement))
        {
            return (PacketType)typeElement.GetInt32();
        }

        throw new Exception("Invalid packet: no Type field");
    }

    // 동적 역직렬화
    public static NetworkPacket DeserializeAny(byte[] data)
    {
        PacketType type = GetPacketType(data);
        string json = Encoding.UTF8.GetString(data);

        return type switch
        {
            PacketType.Connect => JsonSerializer.Deserialize<ConnectPacket>(json, JsonOptions),
            PacketType.Disconnect => JsonSerializer.Deserialize<DisconnectPacket>(json, JsonOptions),
            PacketType.PlayerMove => JsonSerializer.Deserialize<PlayerMovePacket>(json, JsonOptions),
            PacketType.PlayerState => JsonSerializer.Deserialize<PlayerStatePacket>(json, JsonOptions),
            PacketType.GameState => JsonSerializer.Deserialize<GameStatePacket>(json, JsonOptions),
            _ => throw new Exception($"Unknown packet type: {type}")
        };
    }
}