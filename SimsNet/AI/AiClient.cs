using System;
using System.Net.Http;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace SimsNet.AI
{
    public sealed class AiClient
    {
        private static readonly HttpClient SharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private readonly string _baseUrl;

        public AiClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<Decision> ThinkAsync(ThinkRequest req)
        {
            var json = Serialize(req);
            var resp = await SharedHttp.PostAsync(
                _baseUrl + "/agent/think",
                new StringContent(json, Encoding.UTF8, "application/json")
            ).ConfigureAwait(false);

            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new Exception("AI server error: " + Truncate(body, 500));

            return Deserialize<Decision>(body);
        }

        private static string Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static T Deserialize<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "")))
            {
                var value = serializer.ReadObject(stream);
                if (value == null) throw new SerializationException("Empty response from AI server.");
                return (T)value;
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? "";
            return value.Substring(0, maxLength) + "...";
        }
    }

    [DataContract]
    public sealed class ThinkRequest
    {
        [DataMember(Name = "agent_id")] public string AgentId { get; set; }
        [DataMember(Name = "sim_state")] public SimState SimState { get; set; }
        [DataMember(Name = "world_state")] public WorldState WorldState { get; set; }
        [DataMember(Name = "last_heard")] public string LastHeard { get; set; }
    }

    [DataContract]
    public sealed class Decision
    {
        [DataMember(Name = "say")] public string Say { get; set; }
        [DataMember(Name = "move_to")] public MoveTo MoveTo { get; set; }
        [DataMember(Name = "memory_add")] public string MemoryAdd { get; set; }
        [DataMember(Name = "debug")] public string Debug { get; set; }
    }

    [DataContract]
    public sealed class MoveTo
    {
        [DataMember(Name = "x")] public int X { get; set; }
        [DataMember(Name = "y")] public int Y { get; set; }
        [DataMember(Name = "reason")] public string Reason { get; set; }
    }

    [DataContract]
    public sealed class SimState
    {
        [DataMember(Name = "sim_id")] public uint SimId { get; set; }
        [DataMember(Name = "name")] public string Name { get; set; }
    }

    [DataContract]
    public sealed class WorldState
    {
        [DataMember(Name = "tile")] public TileInfo Tile { get; set; }
        [DataMember(Name = "time")] public string Time { get; set; }
    }

    [DataContract]
    public sealed class TileInfo
    {
        [DataMember(Name = "x")] public int X { get; set; }
        [DataMember(Name = "y")] public int Y { get; set; }
    }
}
