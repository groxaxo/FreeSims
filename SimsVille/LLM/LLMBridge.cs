using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FSO.SimAntics;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using Microsoft.Xna.Framework;

namespace FSO.Client.LLM
{
    public sealed class LLMBridge
    {
        private static readonly HttpClient Client = new HttpClient();
        private readonly string BrainUrl;
        private readonly VMEntity MySim;
        private readonly VM MyVM;

        private readonly SemaphoreSlim TickLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource InflightCts;

        public LLMBridge(VMEntity sim, VM vm, string brainUrl = "http://127.0.0.1:5000/tick")
        {
            MySim = sim;
            MyVM = vm;
            BrainUrl = brainUrl;
            Client.Timeout = TimeSpan.FromSeconds(10);
        }

        public Task TryTickAsync() => TryTickAsync(CancellationToken.None);

        public async Task TryTickAsync(CancellationToken externalToken)
        {
            if (!await TickLock.WaitAsync(0).ConfigureAwait(false)) return;

            try
            {
                // cancel previous in-flight tick (optional)
                InflightCts?.Cancel();
                InflightCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

                var state = BuildStateDTO();
                var json = Serialize(state);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var resp = await Client.PostAsync(BrainUrl, content, InflightCts.Token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return;

                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var decision = Deserialize<LLMAgentResponse>(body);
                if (decision == null) return;

                ExecuteDecision(decision);
            }
            catch
            {
                // swallow to protect the game loop; log if you have a logger
            }
            finally
            {
                TickLock.Release();
            }
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

        private LLMSimState BuildStateDTO()
        {
            var s = new LLMSimState();
            s.sim_name = MySim.Name ?? "Sim";
            s.current_action = "IDLE";

            // Motives: map from VMMotive enum to string keys
            if (MySim is VMAvatar avatar)
            {
                s.motives["Hunger"] = avatar.GetMotiveData(VMMotive.Hunger);
                s.motives["Energy"] = avatar.GetMotiveData(VMMotive.Energy);
                s.motives["Comfort"] = avatar.GetMotiveData(VMMotive.Comfort);
                s.motives["Hygiene"] = avatar.GetMotiveData(VMMotive.Hygiene);
                s.motives["Bladder"] = avatar.GetMotiveData(VMMotive.Bladder);
                s.motives["Room"] = avatar.GetMotiveData(VMMotive.Room);
                s.motives["Social"] = avatar.GetMotiveData(VMMotive.Social);
                s.motives["Fun"] = avatar.GetMotiveData(VMMotive.Fun);
            }

            var myPos = MySim.Position;

            // Enumerate nearby objects
            foreach (var obj in MyVM.Entities)
            {
                if (obj == null || obj == MySim) continue;

                var dist = Vector2.Distance(
                    new Vector2(obj.Position.x, obj.Position.y),
                    new Vector2(myPos.x, myPos.y)
                );

                if (dist > 8.0f) continue;

                var info = new LLMObjectInfo
                {
                    guid = obj.ObjectID.ToString(),
                    name = obj.Name ?? obj.ToString(),
                    distance = dist
                };

                // Get pie menu interactions for this object
                var pieMenu = obj.GetPieMenu(MyVM, MySim, false);
                foreach (var interaction in pieMenu)
                {
                    info.interactions.Add(new LLMInteractionInfo
                    {
                        id = interaction.ID,
                        name = interaction.Name ?? "Unknown"
                    });
                }

                s.nearby_objects.Add(info);
            }

            // recent_chat: wire this to your chat log buffer if available
            // s.recent_chat = ChatLog.TakeLast(10).ToList();

            return s;
        }

        private void ExecuteDecision(LLMAgentResponse decision)
        {
            var type = (decision.action_type ?? "IDLE").ToUpperInvariant();

            switch (type)
            {
                case "CHAT":
                    if (!string.IsNullOrWhiteSpace(decision.speech_text))
                    {
                        MyVM.SendCommand(new VMNetChatCmd
                        {
                            ActorUID = MySim.PersistID,
                            Message = decision.speech_text
                        });
                    }
                    break;

                case "MOVE":
                    // For now, MOVE without coordinates is not implemented
                    // Could be extended to move near target_guid
                    break;

                case "INTERACT":
                    if (string.IsNullOrWhiteSpace(decision.target_guid) || decision.interaction_id == null) return;

                    if (!short.TryParse(decision.target_guid, out var targetObjId)) return;
                    var interactionId = decision.interaction_id.Value;

                    MyVM.SendCommand(new VMNetInteractionCmd
                    {
                        ActorUID = MySim.PersistID,
                        CalleeID = targetObjId,
                        Interaction = (ushort)interactionId,
                        Param0 = 0
                    });
                    break;

                default:
                    break;
            }
        }
    }
}
