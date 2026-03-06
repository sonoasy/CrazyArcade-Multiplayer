using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;  // ← 이거 추가!

public static class PacketSerializer
{
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Include,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    // 패킷 → JSON → byte[]
    public static byte[] Serialize<T>(T packet) where T : NetworkPacket
    {
        string json = JsonConvert.SerializeObject(packet, JsonSettings);
        //길이 추가 -> json 연이은 문제 해결해야함 
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);


        byte[] result = new byte[4 + jsonBytes.Length];
        BitConverter.GetBytes(jsonBytes.Length).CopyTo(result, 0); // 앞 4바이트 길이
        jsonBytes.CopyTo(result, 4); // 나머지 JSON
        return result;

        //return Encoding.UTF8.GetBytes(json);
    }

    // byte[] → JSON → 패킷
    public static T Deserialize<T>(byte[] data) where T : NetworkPacket
    {
        string json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<T>(json, JsonSettings);
    }

    // PacketType 먼저 읽기 (dynamic 대신 JObject 사용!)
    public static PacketType GetPacketType(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        JObject obj = JObject.Parse(json);

        if (obj["Type"] != null)
        {
            return (PacketType)obj["Type"].Value<int>();
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
            PacketType.Connect => JsonConvert.DeserializeObject<ConnectPacket>(json, JsonSettings),
            PacketType.Disconnect => JsonConvert.DeserializeObject<DisconnectPacket>(json, JsonSettings),
            PacketType.PlayerMove => JsonConvert.DeserializeObject<PlayerMovePacket>(json, JsonSettings),
            PacketType.PlayerState => JsonConvert.DeserializeObject<PlayerStatePacket>(json, JsonSettings),
            PacketType.GameState => JsonConvert.DeserializeObject<GameStatePacket>(json, JsonSettings),
            _ => throw new Exception($"Unknown packet type: {type}")
        };
    }
}