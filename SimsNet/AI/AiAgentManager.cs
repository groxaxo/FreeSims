using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.NetPlay.Model.Commands;

namespace SimsNet.AI
{
    public sealed class AiAgentManager
    {
        private readonly AiClient _ai;
        private readonly Dictionary<string, AgentRuntime> _agents = new Dictionary<string, AgentRuntime>();
        private readonly Random _rng = new Random();
        private readonly double _thinkEverySeconds;

        private double _accum;
        private string _lastHeardGlobal = "";

        public AiAgentManager(string aiBaseUrl, double thinkEverySeconds = 3.0)
        {
            _ai = new AiClient(aiBaseUrl);
            _thinkEverySeconds = thinkEverySeconds;
        }

        public string LastHeardGlobal => _lastHeardGlobal;

        public void Register(string agentId, uint simId)
        {
            _agents[agentId] = new AgentRuntime { AgentId = agentId, SimId = simId };
        }

        public void RecordChat(string text)
        {
            _lastHeardGlobal = text ?? "";
        }

        public void Update(VM vm, double dtSeconds)
        {
            if (vm == null) return;
            _accum += dtSeconds;
            if (_accum < _thinkEverySeconds) return;
            _accum = 0;

            foreach (var kv in _agents)
            {
                var rt = kv.Value;
                var task = ThinkAndAct(vm, rt);
                task.ContinueWith(t =>
                {
                    if (t.Exception != null)
                        Console.WriteLine("AI agent error: " + t.Exception.GetBaseException().Message);
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        private async Task ThinkAndAct(VM vm, AgentRuntime rt)
        {
            try
            {
                var simState = new SimState
                {
                    SimId = rt.SimId,
                    Name = "Sim#" + rt.SimId
                };

                var pos = GetSimTile(vm, rt.SimId);
                var worldState = new WorldState
                {
                    Tile = new TileInfo { X = pos.X, Y = pos.Y },
                    Time = DateTime.UtcNow.ToString("o")
                };

                var req = new ThinkRequest
                {
                    AgentId = rt.AgentId,
                    SimState = simState,
                    WorldState = worldState,
                    LastHeard = _lastHeardGlobal ?? ""
                };

                var decision = await _ai.ThinkAsync(req).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(decision.Say))
                {
                    BroadcastChat(vm, rt.SimId, decision.Say.Trim());
                }

                if (decision.MoveTo != null)
                {
                    IssueMove(vm, rt.SimId, decision.MoveTo.X, decision.MoveTo.Y, pos.Level);
                }
                else if (_rng.NextDouble() < 0.25)
                {
                    IssueMove(vm, rt.SimId, pos.X + _rng.Next(-2, 3), pos.Y + _rng.Next(-2, 3), pos.Level);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AI agent error: " + ex.Message);
            }
        }

        private TilePos GetSimTile(VM vm, uint simId)
        {
            var sim = vm.Entities.Find(x => x is VMAvatar && x.PersistID == simId) as VMAvatar;
            if (sim == null || sim.Position == LotTilePos.OUT_OF_WORLD)
            {
                return new TilePos { X = 0, Y = 0, Level = 1 };
            }

            return new TilePos { X = sim.Position.x, Y = sim.Position.y, Level = sim.Position.Level };
        }

        private void IssueMove(VM vm, uint simId, int tileX, int tileY, sbyte level)
        {
            vm.SendCommand(new VMNetGotoCmd
            {
                Interaction = 0,
                ActorUID = simId,
                x = (short)tileX,
                y = (short)tileY,
                level = level
            });
        }

        private void BroadcastChat(VM vm, uint simId, string text)
        {
            vm.SendCommand(new VMNetChatCmd
            {
                ActorUID = simId,
                Message = text ?? ""
            });
        }

        private struct TilePos
        {
            public short X;
            public short Y;
            public sbyte Level;
        }

        private sealed class AgentRuntime
        {
            public string AgentId;
            public uint SimId;
        }
    }
}
