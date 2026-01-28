# FreeSims LLM Integration

This module provides AI-powered agent behavior for Sims using a Python LLM backend.

## Architecture (4 Modules)

### Module 1: Python Brain (`LLM_Server/server.py`)
- FastAPI server exposing `/tick` endpoint
- ChromaDB for episodic memory storage
- Structured JSON output from Ollama LLM
- Interaction ID-based decision making

### Module 2: C# DTOs (`SimsVille/LLM/LLMDataStructures.cs`)
- `LLMInteractionInfo`: Interaction with ID and name
- `LLMSimState`: Sim state with motives, nearby objects, chat
- `LLMObjectInfo`: Object with interactions list
- `LLMAgentResponse`: AI decision (CHAT/MOVE/INTERACT/IDLE)

### Module 3: C# Observer (`SimsVille/LLM/LLMBridge.cs`)
- Scans VM world state (motives, objects, interactions)
- HTTP client with 1-in-flight request gating (SemaphoreSlim)
- Uses `Task` (not `async void`) for safe async
- DataContractJsonSerializer for JSON (no external deps)

### Module 4: Actuator + Heartbeat (`SimsVille/Game.cs`)
- Time-based ticking (5 seconds, not frame-based)
- Executes AI decisions via VM commands (VMNetChatCmd, VMNetInteractionCmd)
- Integrated into main game Update loop

## Key Improvements

1. **Module count fixed**: Folded "Hooking the Update Loop" into Module 4 (4 modules total)
2. **Safe async**: Uses `Task` with SemaphoreSlim to gate to 1 in-flight tick
3. **Interaction IDs**: Sends interaction ID + label from C#, Python returns ID (no string matching)
4. **ChromaDB embeddings**: Uses default embedding function (no manual configuration needed)
5. **Structured JSON**: Ollama `format` field enforces JSON schema

## Setup

### Python Server

```bash
cd LLM_Server
pip install -r requirements.txt
python server.py
```

Server listens on `http://127.0.0.1:5000` by default.

### Environment Variables

```bash
# Ollama configuration
export OLLAMA_URL="http://127.0.0.1:11434"
export OLLAMA_MODEL="llama3.2"
export OLLAMA_TIMEOUT_S="20"

# Memory configuration
export MEM_DB_PATH="./sims_memory_db"
export MEM_COLLECTION="sim_episodic_memory"
export TOP_K="3"
```

### C# Client

The LLMBridge is automatically initialized when a UILotControl is active with a sim.
Default endpoint: `http://127.0.0.1:5000/tick`

## Testing

```bash
# Start server
cd LLM_Server
python server.py

# In another terminal, test endpoint
python test_server.py
```

## Acceptance Tests

1. ✓ Python: `POST /tick` returns valid JSON schema (even if Ollama fails → IDLE)
2. ✓ Python: ChromaDB imports and initializes correctly
3. ✓ Python: FastAPI dependencies installed
4. ✓ C#: Only 1 HTTP tick in flight (SemaphoreSlim gating)
5. ⏳ C#: Interaction path uses same VMNetInteractionCmd as UI
6. ⏳ Memory: ChromaDB stores and retrieves past decisions

## Notes

- The Python server gracefully handles Ollama being offline (returns 502)
- ChromaDB uses default embedding function (no external model needed)
- Ollama structured output via `format` parameter enforces JSON schema
- Game ticks AI every ~5 seconds (configurable in Game.cs)
