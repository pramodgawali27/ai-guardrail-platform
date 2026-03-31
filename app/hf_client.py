import httpx


async def call_hf(prompt: str, settings) -> str:
    if not settings.hf_token:
        return "⚠️ HF_TOKEN is not set. Set it as a Space secret."

    url = "https://router.huggingface.co/together/v1/chat/completions"
    headers = {
        "Authorization": f"Bearer {settings.hf_token}",
        "Content-Type": "application/json",
    }
    payload = {
        "model": settings.hf_model_id,
        "messages": [{"role": "user", "content": prompt}],
        "max_tokens": settings.hf_max_tokens,
        "temperature": 0.7,
    }

    async with httpx.AsyncClient(timeout=30) as client:
        resp = await client.post(url, json=payload, headers=headers)
        if resp.status_code == 200:
            try:
                return resp.json()["choices"][0]["message"]["content"]
            except (KeyError, IndexError):
                return f"⚠️ Unexpected response format: {resp.text[:200]}"
        return f"⚠️ Model returned {resp.status_code}: {resp.text[:200]}"
