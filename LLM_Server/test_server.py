#!/usr/bin/env python3
"""
Simple test for the LLM server endpoint.
"""
import json
import requests
import time
import subprocess
import sys
from pathlib import Path

def test_server():
    print("Testing LLM Server...")
    
    # Test data
    test_payload = {
        "sim_name": "TestSim",
        "motives": {
            "Hunger": 50,
            "Energy": 60,
            "Comfort": 70,
            "Hygiene": 80,
            "Bladder": 90,
            "Room": 75,
            "Social": 65,
            "Fun": 55
        },
        "nearby_objects": [
            {
                "guid": "123",
                "name": "Fridge",
                "distance": 2.5,
                "interactions": [
                    {"id": 1, "name": "Food/Have Snack"},
                    {"id": 2, "name": "Food/Prepare Meal"}
                ]
            }
        ],
        "recent_chat": [],
        "current_action": "IDLE"
    }
    
    # Test 1: POST /tick with minimal LLM (will fail without Ollama, but should return proper error)
    print("\n1. Testing POST /tick endpoint...")
    try:
        response = requests.post(
            "http://127.0.0.1:5000/tick",
            json=test_payload,
            timeout=5
        )
        print(f"   Status Code: {response.status_code}")
        if response.status_code == 200:
            data = response.json()
            print(f"   Response: {json.dumps(data, indent=2)}")
            # Validate response structure
            assert "action_type" in data, "Missing action_type"
            assert "thought_process" in data, "Missing thought_process"
            print("   ✓ Valid response structure")
        elif response.status_code == 502:
            print(f"   ✓ Expected error (Ollama not available): {response.text[:200]}")
        else:
            print(f"   ✗ Unexpected status: {response.status_code}")
            print(f"   Response: {response.text[:500]}")
            return False
    except requests.exceptions.ConnectionError:
        print("   ✗ Server not running on port 5000")
        print("   Note: Start server with: python LLM_Server/server.py")
        return False
    except Exception as e:
        print(f"   ✗ Error: {e}")
        return False
    
    # Test 2: Validate Python imports work
    print("\n2. Testing Python imports...")
    try:
        import chromadb
        print(f"   ✓ chromadb version: {chromadb.__version__}")
    except Exception as e:
        print(f"   ✗ chromadb import failed: {e}")
        return False
    
    try:
        import fastapi
        print(f"   ✓ fastapi version: {fastapi.__version__}")
    except Exception as e:
        print(f"   ✗ fastapi import failed: {e}")
        return False
    
    try:
        import pydantic
        print(f"   ✓ pydantic version: {pydantic.__version__}")
    except Exception as e:
        print(f"   ✗ pydantic import failed: {e}")
        return False
    
    print("\n✓ All tests passed!")
    return True

if __name__ == "__main__":
    success = test_server()
    sys.exit(0 if success else 1)
