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
        private static readonly HttpClient Client;
        private const float MAX_INTERACTION_DISTANCE = 8.0f;
        
        private readonly string BrainUrl;
        private readonly VMEntity MySim;
        private readonly VM MyVM;

        private readonly SemaphoreSlim TickLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _inflightCts;

        static LLMBridge()
        {
            Client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public LLMBridge(VMEntity sim, VM vm, string brainUrl = "http://127.0.0.1:5000/tick")
        {
            MySim = sim;
            MyVM = vm;
            BrainUrl = brainUrl;
        }

        public Task TryTickAsync() => TryTickAsync(CancellationToken.None);

        public async Task TryTickAsync(CancellationToken externalToken)
        {
            if (!await TickLock.WaitAsync(0).ConfigureAwait(false)) return;

            try
            {
                // Dispose and cancel previous in-flight tick
                _inflightCts?.Cancel();
                _inflightCts?.Dispose();
                _inflightCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

                var state = BuildStateDTO();
                var json = Serialize(state);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var resp = await Client.PostAsync(BrainUrl, content, _inflightCts.Token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return;

                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var decision = Deserialize<LLMAgentResponse>(body);
                if (decision == null) return;

                ExecuteDecision(decision);
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation, ignore
            }
            catch (Exception ex)
            {
                // Log error in debug builds
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"LLMBridge error: {ex.Message}");
#endif
                // Swallow to protect the game loop
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
                return (T)serializer.ReadObject(stream);
            }
        }

        private LLMSimState BuildStateDTO()
        {
            var s = new LLMSimState();
            s.SimName = MySim.Name ?? "Sim";
            s.CurrentAction = "IDLE";

            // Motives: map from VMMotive enum to string keys
            if (MySim is VMAvatar avatar)
            {
                s.Motives["Hunger"] = avatar.GetMotiveData(VMMotive.Hunger);
                s.Motives["Energy"] = avatar.GetMotiveData(VMMotive.Energy);
                s.Motives["Comfort"] = avatar.GetMotiveData(VMMotive.Comfort);
                s.Motives["Hygiene"] = avatar.GetMotiveData(VMMotive.Hygiene);
                s.Motives["Bladder"] = avatar.GetMotiveData(VMMotive.Bladder);
                s.Motives["Room"] = avatar.GetMotiveData(VMMotive.Room);
                s.Motives["Social"] = avatar.GetMotiveData(VMMotive.Social);
                s.Motives["Fun"] = avatar.GetMotiveData(VMMotive.Fun);
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

                if (dist > MAX_INTERACTION_DISTANCE) continue;

                var info = new LLMObjectInfo
                {
                    Guid = obj.ObjectID.ToString(),
                    Name = obj.Name ?? obj.ToString(),
                    Distance = dist
                };

                // Get pie menu interactions for this object
                var pieMenu = obj.GetPieMenu(MyVM, MySim, false);
                foreach (var interaction in pieMenu)
                {
                    info.Interactions.Add(new LLMInteractionInfo
                    {
                        Id = interaction.ID,
                        Name = interaction.Name ?? "Unknown"
                    });
                }

                s.NearbyObjects.Add(info);
            }

            return s;
        }

        private void ExecuteDecision(LLMAgentResponse decision)
        {
            var type = (decision.ActionType ?? "IDLE").ToUpperInvariant();

            switch (type)
            {
                case "CHAT":
                    if (!string.IsNullOrWhiteSpace(decision.SpeechText))
                    {
                        MyVM.SendCommand(new VMNetChatCmd
                        {
                            ActorUID = MySim.PersistID,
                            Message = decision.SpeechText
                        });
                    }
                    break;

                case "MOVE":
                    // For now, MOVE without coordinates is not implemented
                    // Could be extended to move near target_guid
                    break;

                case "INTERACT":
                    if (string.IsNullOrWhiteSpace(decision.TargetGuid) || decision.InteractionId == null) return;

                    if (!short.TryParse(decision.TargetGuid, out var targetObjId)) return;
                    var interactionId = decision.InteractionId.Value;

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
        
        public void Dispose()
        {
            _inflightCts?.Cancel();
            _inflightCts?.Dispose();
            TickLock?.Dispose();
        }
    }
}
