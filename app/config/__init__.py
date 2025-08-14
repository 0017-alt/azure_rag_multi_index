"""
Config settings for the FastAPI RAG app
"""
from typing import Optional
from pydantic import BaseModel, Field
from pydantic_settings import BaseSettings
import logging

logger = logging.getLogger(__name__)


class OpenAISettings(BaseModel):
    """Azure OpenAI settings"""
    endpoint: str
    gpt_deployment: str
    embedding_deployment: Optional[str] = ""


class SearchSettings(BaseModel):
    """Azure AI Search settings"""
    url: str
    index_name: str


class AppSettings(BaseSettings):
    """Application settings with environment variable loading capabilities"""
    azure_subscription_id: str = ""
    azure_env_name: str = ""
    azure_storage_account_name: str = ""
    azure_search_service_name: str = ""
    
    # Azure OpenAI Settings
    azure_openai_endpoint: str = Field(..., env="AZURE_OPENAI_ENDPOINT")
    azure_openai_gpt_deployment: str = Field(..., env="AZURE_OPENAI_GPT_DEPLOYMENT")
    azure_openai_embedding_deployment: str = Field("", env="AZURE_OPENAI_EMBEDDING_DEPLOYMENT")
    
    # Azure AI Search Settings
    azure_search_service_url: str = Field(..., env="AZURE_SEARCH_SERVICE_URL")
    azure_search_index_name_inventories: str = Field(..., env="AZURE_SEARCH_INDEX_NAME_INVENTORIES")
    azure_search_index_name_incidents: str = Field(..., env="AZURE_SEARCH_INDEX_NAME_INCIDENTS")
    azure_search_index_name_arc: str = Field(..., env="AZURE_SEARCH_INDEX_NAME_ARC")

    # Other settings
    system_prompt: str = Field(
        "You are an infrastructure knowledge assistant answering about servers, incidents and ownership.\nUse ONLY the information contained in the Sources section. If information is missing, state you don't know. Never invent data.\n\nTOLERATE TYPOS & NORMALIZE:\n- Accept minor typos / case differences / missing leading zeros in server IDs (e.g. srv1, SRV1, SRV01 => SRV001 if that exists; payment-gw-stagin => payment-gw-staging).\n- Normalize server_id pattern: PREFIX + digits. If digits length < canonical (3), zero‑pad (SRV1 => SRV001). Remove extra zeros when comparing.\n- Ignore hyphens/underscores/case when matching IDs or team names (auth_api_prod ~ auth-api-prod).\n- For team / owner names allow edit distance 1 (Platfrom => Platform).\n- If multiple candidates remain, list the possible matches and ask the user to clarify; do not guess.\n\nANSWER FORMAT:\n- Provide concise bullet points (<=5) unless user requests another format.\n- For each factual bullet cite the server_id or incident identifier in parentheses.\n- If summarizing multiple rows, group by environment or status.\n\nRULES:\n1. Use only facts from Sources.\n2. Do not output internal reasoning.\n3. Clearly say 'insufficient information' when data not found.\n4. Do not include unrelated marketing or speculative content.\n\nNow answer the user Query in the language of the user Query using only Sources.\nQuery: {query}\nSources:\n{sources}",
        env="SYSTEM_PROMPT"
    )
    
    # Optional port setting
    port: int = Field(8080, env="PORT")
    
    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"
        case_sensitive = False
        env_nested_delimiter = "__"
        # Setting env_priority to True prioritizes environment variables over .env file
        env_priority = True
    
    @property
    def openai(self) -> OpenAISettings:
        """Return OpenAI settings in the format used by the application"""
        return OpenAISettings(
            endpoint=self.azure_openai_endpoint,
            gpt_deployment=self.azure_openai_gpt_deployment,
            embedding_deployment=self.azure_openai_embedding_deployment
        )
    
    @property
    def search(self) -> SearchSettings:
        """Return Search settings in the format used by the application"""
        return SearchSettings(
            url=self.azure_search_service_url,
            index_name=self.azure_search_index_name
        )

settings = AppSettings()
