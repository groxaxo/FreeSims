import os
import json
import uuid
import logging
from datetime import datetime
from typing import List, Dict, Optional, Literal

import requests
import chromadb
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field, ValidationError

# -----------------------------
# Config
# -----------------------------
BIND_HOST = os.getenv("BIND_HOST", "127.0.0.1")
BIND_PORT = int(os.getenv("BIND_PORT", "5000"))

OLLAMA_URL = os.getenv("OLLAMA_URL", "http://127.0.0.1:11434")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "llama3.2")
OLLAMA_TIMEOUT_S = float(os.getenv("OLLAMA_TIMEOUT_S", "20"))

MEM_DB_PATH = os.getenv("MEM_DB_PATH", "./sims_memory_db")
MEM_COLLECTION = os.getenv("MEM_COLLECTION", "sim_episodic_memory")
TOP_K = int(os.getenv("TOP_K", "3"))

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger("llm_server")

app = FastAPI(title="Sims LLM Brain", version="1.0")

# -----------------------------
# Memory (ChromaDB)
# -----------------------------
chroma_client = chromadb.PersistentClient(path=MEM_DB_PATH)
memory_collection = chroma_client.get_or_create_collection(name=MEM_COLLECTION)

# -----------------------------
# DTOs
# -----------------------------
class InteractionInfo(BaseModel):
    id: int = Field(..., description="Stable interaction id/index as understood by the client")
    name: str = Field(..., description="Label/path shown in UI (for debugging only)")

class ObjectInfo(BaseModel):
    guid: str
    name: str
    interactions: List[InteractionInfo] = []
    distance: float

class SimState(BaseModel):
    sim_name: str
    motives: Dict[str, int]  # Hunger/Energy/etc (0-100)
    nearby_objects: List[ObjectInfo] = []
    recent_chat: List[str] = []
    current_action: str = "IDLE"

class AgentResponse(BaseModel):
    action_type: Literal["MOVE", "CHAT", "INTERACT", "IDLE"]
    target_guid: Optional[str] = None
    interaction_id: Optional[int] = None
    speech_text: Optional[str] = None
    thought_process: str

# A JSON Schema Ollama can enforce via the `format` field.
OLLAMA_SCHEMA = {
    "type": "object",
    "properties": {
        "action_type": {"type": "string", "enum": ["MOVE", "CHAT", "INTERACT", "IDLE"]},
        "target_guid": {"type": ["string", "null"]},
        "interaction_id": {"type": ["integer", "null"]},
        "speech_text": {"type": ["string", "null"]},
        "thought_process": {"type": "string"},
    },
    "required": ["action_type", "thought_process"],
    "additionalProperties": False,
}

def _ollama_generate(system_prompt: str, user_prompt: str) -> dict:
    """
    Calls Ollama /api/generate with structured output.
    `format` supports "json" or a JSON schema object.
    """
    url = f"{OLLAMA_URL.rstrip('/')}/api/generate"
    payload = {
        "model": OLLAMA_MODEL,
        "system": system_prompt,
        "prompt": user_prompt,
        "stream": False,
        "format": OLLAMA_SCHEMA,
    }
    try:
        r = requests.post(url, json=payload, timeout=OLLAMA_TIMEOUT_S)
        r.raise_for_status()
        data = r.json()
        # Ollama returns content in the "response" field (string); with schema it should be valid JSON text.
        raw = data.get("response", "")
        return json.loads(raw)
    except (requests.RequestException, json.JSONDecodeError) as e:
        raise HTTPException(status_code=502, detail=f"LLM call/parse failed: {e}")

def _safe_parse_response(obj: dict) -> AgentResponse:
    """Validate and coerce; fallback to IDLE if invalid."""
    try:
        return AgentResponse.model_validate(obj)
    except ValidationError:
        # Hard fallback — never crash the game loop.
        return AgentResponse(
            action_type="IDLE",
            thought_process="Invalid JSON/schema from LLM. Falling back to IDLE."
        )

@app.post("/tick", response_model=AgentResponse)
async def tick(state: SimState):
    # 1) Retrieve relevant memories (filter by sim_name for cleanliness)
    query_text = (
        f"sim={state.sim_name} "
        f"motives={state.motives} "
        f"objects={[o.name for o in state.nearby_objects[:5]]} "
        f"chat={state.recent_chat[-6:]}"
    )

    retrieved_docs: List[str] = []
    try:
        res = memory_collection.query(
            query_texts=[query_text],
            n_results=TOP_K,
            where={"sim_name": state.sim_name},
        )
        # Chroma returns list-of-lists.
        docs = (res.get("documents") or [[]])[0]
        retrieved_docs = docs[:TOP_K] if docs else []
    except Exception as e:
        logger.warning(f"ChromaDB query failed: {e}")
        retrieved_docs = []

    # 2) Prompt
    # Note: Interaction labels correspond to tree-table / pie-menu naming; treat them as UI/debug hints.
    system_prompt = (
        f"You are {state.sim_name}, a character in The Sims.\n"
        f"Roleplay strongly but output ONLY JSON matching the schema.\n"
        f"Needs: {state.motives}\n"
        f"Memories: {retrieved_docs}\n\n"
        f"Rules:\n"
        f"- If any need is <30, prioritize raising it.\n"
        f"- INTERACT must include target_guid and interaction_id that exists in the visible objects list.\n"
        f"- CHAT must include speech_text.\n"
        f"- Keep thought_process short and concrete.\n"
    )

    # Provide the model the exact action options and available interaction IDs.
    visible = []
    for o in state.nearby_objects:
        visible.append({
            "guid": o.guid,
            "name": o.name,
            "distance": o.distance,
            "interactions": [{"id": it.id, "name": it.name} for it in o.interactions],
        })

    user_prompt = (
        f"Current action: {state.current_action}\n"
        f"Recent chat: {state.recent_chat[-10:]}\n"
        f"Visible objects JSON:\n{json.dumps(visible, ensure_ascii=False)}\n"
        f"Decide the next action now."
    )

    # 3) Decide
    decision_dict = _ollama_generate(system_prompt, user_prompt)
    decision = _safe_parse_response(decision_dict)

    # 4) Store memory
    mem_text = (
        f"[{datetime.now().isoformat()}] "
        f"{state.sim_name} chose {decision.action_type} "
        f"target={decision.target_guid} interaction_id={decision.interaction_id} "
        f"said={decision.speech_text} "
        f"because={decision.thought_process}"
    )
    try:
        memory_collection.add(
            ids=[str(uuid.uuid4())],
            documents=[mem_text],
            metadatas=[{"timestamp": datetime.now().isoformat(), "sim_name": state.sim_name}],
        )
    except Exception as e:
        logger.error(f"Failed to store memory: {e}")

    return decision


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host=BIND_HOST, port=BIND_PORT)
