from functools import lru_cache
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    hf_token: str = ""
    hf_model_id: str = "Qwen/Qwen2.5-7B-Instruct-Turbo"
    hf_max_tokens: int = 512
    database_url: str = "sqlite:///./guardrail.db"
    policy_seed_path: str = "/app/policies/samples"
    evaluation_seed_path: str = "/app/evaluations/datasets"
    seed_on_startup: bool = True

    model_config = {"env_file": ".env", "env_file_encoding": "utf-8", "extra": "ignore"}


@lru_cache()
def get_settings() -> Settings:
    return Settings()
