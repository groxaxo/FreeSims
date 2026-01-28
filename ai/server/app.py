import json
import logging
import os
import re
import time
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import httpx
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

app = FastAPI()
logger = logging.getLogger("freesims.ai")
if not logger.handlers:
    logging.basicConfig(level=logging.INFO)

MEMORY_DIR = Path(os.getenv("MEMORY_DIR", "/data/memory"))
MEMORY_DIR.mkdir(parents=True, exist_ok=True)

AGENTS_FILE = Path(os.getenv("AGENTS_FILE", "/config/agents.json"))

OLLAMA_BASE_URL = os.getenv("OLLAMA_BASE_URL", "http://ollama:11434")
OPENAI_BASE_URL = os.getenv("OPENAI_BASE_URL", "https://api.openai.com/v1")
DEEPSEEK_BASE_URL = os.getenv("DEEPSEEK_BASE_URL", "https://api.deepseek.com/v1")

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY", "")
DEEPSEEK_API_KEY = os.getenv("DEEPSEEK_API_KEY", "")

WORD_RE = re.compile(r"[a-zA-Z0-9_]{2,}")


def load_agents() -> Dict[str, Any]:
    if not AGENTS_FILE.exists():
        raise RuntimeError(f"agents.json not found at {AGENTS_FILE}")
    return json.loads(AGENTS_FILE.read_text(encoding="utf-8"))


AGENTS = load_agents()


def mem_path(agent_id: str) -> Path:
    safe = re.sub(r"[^a-zA-Z0-9_.-]+", "_", agent_id)
    return MEMORY_DIR / f"{safe}.jsonl"


def append_mem(agent_id: str, role: str, content: str) -> None:
    rec = {"ts": int(time.time()), "role": role, "content": content}
    with mem_path(agent_id).open("a", encoding="utf-8") as f:
        f.write(json.dumps(rec, ensure_ascii=False) + "\n")


def load_mem(agent_id: str, max_lines: int = 600) -> List[Dict[str, Any]]:
    path = mem_path(agent_id)
    if not path.exists():
        return []
    lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()[-max_lines:]
    out: List[Dict[str, Any]] = []
    for line in lines:
        try:
            out.append(json.loads(line))
        except Exception:
            logger.warning("Skipping invalid memory line for agent %s.", agent_id)
    return out


def tok(text: str) -> set:
    return set(word.lower() for word in WORD_RE.findall(text or ""))


def select_relevant(mem: List[Dict[str, Any]], query: str, k: int = 12) -> List[Dict[str, Any]]:
    query_tokens = tok(query)
    if not query_tokens:
        return mem[-k:]
    scored: List[Tuple[int, Dict[str, Any]]] = []
    for entry in mem:
        content = str(entry.get("content", ""))
        score = len(query_tokens & tok(content))
        if score > 0:
            scored.append((score, entry))
    scored.sort(key=lambda item: item[0], reverse=True)
    return [item for _, item in scored[:k]] or mem[-k:]


class ThinkRequest(BaseModel):
    agent_id: str
    sim_state: Dict[str, Any] = Field(default_factory=dict)
    world_state: Dict[str, Any] = Field(default_factory=dict)
    last_heard: str = ""


class MoveTo(BaseModel):
    x: int
    y: int
    reason: str = ""


class Decision(BaseModel):
    say: str = ""
    move_to: Optional[MoveTo] = None
    memory_add: str = ""
    debug: str = ""


@app.get("/health")
def health():
    return {"ok": True, "agents": list(AGENTS.keys())}


def provider_endpoint(provider: str) -> Tuple[str, str]:
    provider = provider.lower()
    if provider == "ollama":
        return (f"{OLLAMA_BASE_URL}/v1", "")
    if provider == "openai":
        if not OPENAI_API_KEY:
            raise HTTPException(status_code=500, detail="OPENAI_API_KEY is not configured.")
        return (OPENAI_BASE_URL, OPENAI_API_KEY)
    if provider == "deepseek":
        if not DEEPSEEK_API_KEY:
            raise HTTPException(status_code=500, detail="DEEPSEEK_API_KEY is not configured.")
        return (DEEPSEEK_BASE_URL, DEEPSEEK_API_KEY)
    raise ValueError(f"Unknown provider: {provider}")


async def chat_completion(base_url: str, api_key: str, model: str, messages: List[Dict[str, str]]) -> str:
    url = f"{base_url}/chat/completions"
    headers = {"content-type": "application/json"}
    if api_key:
        headers["authorization"] = f"Bearer {api_key}"
    payload = {"model": model, "messages": messages, "temperature": 0.8, "max_tokens": 350}
    async with httpx.AsyncClient(timeout=120.0) as client:
        resp = await client.post(url, headers=headers, json=payload)
        if resp.status_code >= 400:
            raise HTTPException(status_code=resp.status_code, detail=resp.text)
        data = resp.json()
    try:
        return data["choices"][0]["message"]["content"]
    except Exception:
        raise HTTPException(status_code=502, detail="Unexpected response from provider.")


JSON_RULES = """Return ONLY valid JSON matching this schema:
{
  "say": "string (optional)",
  "move_to": {"x": int, "y": int, "reason": "string"} | null,
  "memory_add": "string (optional)",
  "debug": "string (optional)"
}
No markdown. No extra keys.
"""


@app.post("/agent/think", response_model=Decision)
async def agent_think(req: ThinkRequest):
    if req.agent_id not in AGENTS:
        raise HTTPException(status_code=404, detail="unknown agent_id")

    cfg = AGENTS[req.agent_id]
    provider = cfg.get("provider", "ollama")
    model = cfg.get("model", "")
    system = cfg.get("system", "")

    base_url, api_key = provider_endpoint(provider)

    mem = load_mem(req.agent_id)
    relevant = select_relevant(mem, req.last_heard, k=10)

    mem_lines = []
    for item in relevant:
        role = item.get("role", "unknown")
        content = str(item.get("content", "")).replace("\n", " ")
        mem_lines.append(f"- ({role}) {content[:220]}")
    mem_block = "Relevant past memory:\n" + "\n".join(mem_lines) if mem_lines else "Relevant past memory: (none)"

    user_context = {
        "sim_state": req.sim_state,
        "world_state": req.world_state,
        "last_heard": req.last_heard,
    }

    messages = [
        {"role": "system", "content": system},
        {"role": "system", "content": JSON_RULES},
        {"role": "system", "content": mem_block},
        {"role": "user", "content": json.dumps(user_context, ensure_ascii=False)},
    ]

    if req.last_heard.strip():
        append_mem(req.agent_id, "user", req.last_heard.strip())

    raw = await chat_completion(base_url, api_key, model, messages)

    try:
        parsed = json.loads(raw)
    except Exception:
        parsed = {"say": raw.strip(), "move_to": None, "memory_add": "", "debug": "model returned non-json"}

    decision = Decision(
        say=str(parsed.get("say", ""))[:600],
        move_to=parsed.get("move_to"),
        memory_add=str(parsed.get("memory_add", ""))[:400],
        debug=str(parsed.get("debug", ""))[:400],
    )

    if decision.memory_add.strip():
        append_mem(req.agent_id, "memory", decision.memory_add.strip())
    if decision.say.strip():
        append_mem(req.agent_id, "assistant", decision.say.strip())

    return decision
