"""
RAG Chat Service using Azure OpenAI and Azure AI Search

This module implements a Retrieval Augmented Generation (RAG) service that connects
Azure OpenAI with Azure AI Search. RAG enhances LLM responses by grounding them in
your enterprise data stored in Azure AI Search.
"""
import logging
from typing import List
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from openai import AsyncAzureOpenAI
from app.models.chat_models import ChatMessage
from app.config import settings
from azure.search.documents import SearchClient

logger = logging.getLogger(__name__)


class RagChatService:
    """
    Service that provides Retrieval Augmented Generation (RAG) capabilities
    by connecting Azure OpenAI with Azure AI Search for grounded responses.
    
    This service:
    1. Handles authentication to Azure services using Managed Identity
    2. Implements the "On Your Data" pattern using Azure AI Search as a data source
    3. Processes user queries and returns AI-generated responses grounded in your data
    """
    
    def __init__(self):
        """Initialize the RAG chat service using settings from app config"""
        # Store settings for easy access
        self.openai_endpoint = settings.azure_openai_endpoint
        self.gpt_deployment = settings.azure_openai_gpt_deployment
        self.embedding_deployment = settings.azure_openai_embedding_deployment
        self.search_url = settings.azure_search_service_url
        self.search_index_name_inventories = settings.azure_search_index_name_inventories
        self.search_index_name_incidents = settings.azure_search_index_name_incidents
        self.search_index_name_arc = settings.azure_search_index_name_arc
        self.system_prompt = settings.system_prompt
        
        # Create Azure credentials for managed identity
        # This allows secure, passwordless authentication to Azure services
        self.credential = DefaultAzureCredential()
        token_provider = get_bearer_token_provider(
            self.credential,
            "https://cognitiveservices.azure.com/.default"
        )
        
        # Create Azure OpenAI client
        # We use the latest Azure OpenAI Python SDK with async support
        # NOTE: api_version must be a valid Azure OpenAI REST API version, not a model version.
        # If you specify a future / invalid version you may receive 403/404 errors.
        # Adjust here if your resource supports a newer version.
        self.openai_client = AsyncAzureOpenAI(
            azure_endpoint=self.openai_endpoint,
            azure_ad_token_provider=token_provider,
            api_version="2024-05-01-preview"
        )

        self.search_client_inventories = SearchClient(
            endpoint=self.search_url,
            index_name=self.search_index_name_inventories,
            credential=self.credential
        )

        self.search_client_incidents = SearchClient(
            endpoint=self.search_url,
            index_name=self.search_index_name_incidents,
            credential=self.credential
        )
        
        self.search_client_arc = SearchClient(
            endpoint=self.search_url,
            index_name=self.search_index_name_arc,
            credential=self.credential
        )

        logger.info("RagChatService initialized with environment variables")
    
    async def get_chat_completion(self, history: List[ChatMessage], top_k: int = 3):
        """
        Search multiple indexes using the AI Search REST API, embed the results into the prompt, and pass them to OpenAI for RAG processing.
        Uses LLM to determine which index(es) to use. Both can be True.
        Args:
            history: Chat history
            top_k: Number of documents to retrieve from each index
        Returns:
            Response from the OpenAI API
        """
        try:
            recent_history = history[-20:] if len(history) > 20 else history
            user_query = recent_history[-1].content if recent_history else ""

            # Use LLM to decide which index(es) to use
            index_selection_prompt = (
                "Given the following user query, decide which index(es) should be used to answer it. "
                "There are two indexes: 'inventories' and 'incidents'.\n"
                "- If the query is about responsible department or contact information, set 'inventories' to True.\n"
                "- If the query is about past incident information, set 'incidents' to True.\n"
                "- If the query is about Azure Arc information, set 'arc' to True.\n"
                "- If some apply, set them to True.\n"
                "Return a JSON object like: {\"inventories\": true, \"incidents\": false, \"arc\": false} or {\"inventories\": true, \"incidents\": true, \"arc\": true}.\n"
                "User query: " + user_query
            )

            logger.debug("Sending index selection prompt to OpenAI")
            selection_response = await self.openai_client.chat.completions.create(
                messages=[{"role": "user", "content": index_selection_prompt}],
                model=self.gpt_deployment
            )
            import json
            selection_text = selection_response.choices[0].message.content.strip()
            logger.info(f"Index selection response: {selection_text}")
            try:
                selection = json.loads(selection_text)
                search_inventories = bool(selection.get("inventories", False))
                search_incidents = bool(selection.get("incidents", False))
                search_arc = bool(selection.get("arc", False))
            except Exception:
                # fallback: search both
                search_inventories = True
                search_incidents = True
                search_arc = True

            sources = ""
            # Query Azure AI Search (separate try blocks so we can pinpoint 403 origin)
            if search_inventories:
                try:
                    logger.debug("Querying inventories index")
                    search_results = self.search_client_inventories.search(
                        search_text=user_query,
                        top=1,
                        select="content"
                    )
                    sources_formatted = "\n".join([f'{document.get("content", "")}' for document in search_results])
                    sources += sources_formatted + "\n"
                except Exception as se:
                    logger.error(f"Search query failed for inventories index: {se}")
            if search_incidents:
                try:
                    logger.debug("Querying incidents index")
                    search_results = self.search_client_incidents.search(
                        search_text=user_query,
                        top=top_k,
                        select="content"
                    )
                    sources_formatted = "\n".join([f'{document.get("content", "")}' for document in search_results])
                    sources += sources_formatted + "\n"
                except Exception as se:
                    logger.error(f"Search query failed for incidents index: {se}")
            if search_arc:
                try:
                    logger.debug("Querying arc index")
                    search_results = self.search_client_arc.search(
                        search_text=user_query,
                        top=1,
                        select="content"
                    )
                    sources_formatted = "\n".join([f'{document.get("content", "")}' for document in search_results])
                    sources += sources_formatted + "\n"
                except Exception as se:
                    logger.error(f"Search query failed for arc index: {se}")

            response = await self.openai_client.chat.completions.create(
                messages=[
                    {
                        "role": "user",
                        "content": self.system_prompt.format(query=user_query, sources=sources)
                    }
                ],
                model=self.gpt_deployment
            )

            return response

        except Exception as e:
            # Enrich logging for 403 / auth issues
            if hasattr(e, 'status_code'):
                logger.error(f"Error in get_chat_completion (status {getattr(e, 'status_code', 'n/a')}): {e}")
            else:
                logger.error(f"Error in get_chat_completion: {e}")
            raise


# Create singleton instance
rag_chat_service = RagChatService()
