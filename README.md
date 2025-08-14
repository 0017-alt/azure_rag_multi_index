## RAG Multi‑Index Infrastructure Assistant

This project is an Azure-based Retrieval Augmented Generation (RAG) web application that answers infrastructure questions about servers, incidents, and ownership data. It combines Azure OpenAI (GPT + Embeddings), Azure AI Search (multiple indexes), and Azure Blob Storage. The app is deployed to Azure App Service using the Azure Developer CLI (`azd`) and Bicep infrastructure as code.

> Origin inspired by: https://github.com/Azure-Samples/app-service-rag-openai-ai-search-python (heavily adapted & extended for multi-index + infra prompt use case).

### Key Features
* Multiple Azure AI Search indexes (inventories, incidents, arc) unified at query time
* System prompt engineered for infrastructure knowledge + typo tolerant normalization logic
* Fully provisioned infra with a single `azd up` (OpenAI, Search, Storage, App Service, Log Analytics)
* Managed identities + role assignments (no API keys in code)
* Scripted index + data bootstrap
* Parameterized `SYSTEM_PROMPT` (editable in Bicep)

---
## Architecture Overview

Component | Purpose
----------|--------
App Service (Linux, Python) | Hosts FastAPI / Uvicorn app
Azure OpenAI (GPT + Embeddings) | Text generation & embedding vectorization
Azure AI Search | Structured + semantic retrieval across indexes
Storage Account (Blob) | Source documents (inventories / incidents / arc)
Log Analytics Workspace | Centralized diagnostics & logs
Managed Identities | Secure inter-service auth (no secrets)

Relevant files:
* `infra/main.bicep` – Bicep template defining all Azure resources & role assignments
* `azure.yaml` – `azd` project descriptor (service + infra path)
* `app/` – Application code (FastAPI, models, services)
* `scripts/` – Data and index provisioning helpers

---
## Prerequisites
* Python 3.11+ (project uses 3.12 on App Service; local 3.11/3.12 both fine)
* Azure CLI (`az`) and signed-in subscription access
* Azure Developer CLI (`azd`) latest version
* Git

---
## Quick Start (Local)
```pwsh
# 1. Clone
git clone <this-repo-url> && cd sre-sample

# 2. Python virtual env
python -m venv .venv
./.venv/Scripts/Activate.ps1  # (Windows PowerShell)

# 3. Install deps
pip install -r requirements.txt

# 4. (First time) Provision Azure infra (creates OpenAI/Search/etc.)
azd auth login
azd up   # or: azd provision (infra) + azd deploy (code)

# 5. Retrieve output values (or view in Portal)
azd env get-values > azd-values.txt

# 6. Create a .env from sample & fill dynamic values
copy .env.sample .env  # then edit

# 7. Upload data to Blob Storage
python ./scripts/upload_data_to_blob_storage.py

# 8. Create / update search indexes & vectors
python ./scripts/create_azure_ai_indices.py

# 9. Run locally
uvicorn main:app --reload
```

Local URL (default): http://127.0.0.1:8000

---
## Environment Variables
The application reads settings (Pydantic) from environment variables or `.env`.

Variable | Description | Source
---------|-------------|-------
AZURE_OPENAI_ENDPOINT | OpenAI resource endpoint | Bicep output
AZURE_OPENAI_GPT_DEPLOYMENT | GPT deployment name | Bicep param
AZURE_OPENAI_EMBEDDING_DEPLOYMENT | Embedding deployment | Bicep param
AZURE_SEARCH_SERVICE_URL | Search endpoint URL | Bicep output
AZURE_SEARCH_INDEX_NAME_INVENTORIES | Inventories index name | Bicep/param
AZURE_SEARCH_INDEX_NAME_INCIDENTS | Incidents index name | Bicep/param
AZURE_SEARCH_INDEX_NAME_ARC | Arc index name | Bicep output / env
AZURE_STORAGE_ACCOUNT_NAME | Blob storage account | Bicep output
AZURE_SEARCH_SERVICE_NAME | Search service internal name | Bicep output
SYSTEM_PROMPT | Core RAG system behavior instructions | Bicep param

`SYSTEM_PROMPT` is now parameterized in `infra/main.bicep` (`systemPrompt` param). To customize without editing Bicep, you can override via App Service Configuration after deployment or adjust the template then run `azd up` again.

---
## Data & Index Bootstrapping

Scripts:
* `scripts/upload_data_to_blob_storage.py` – Uploads raw docs (from `docs/` or other sources) into blob containers.
* `scripts/create_azure_ai_indices.py` – Creates (or updates) Azure AI Search indexes & optionally populates embeddings.
* `scripts/create_indices.sh` – Shell alternative (ensure execution + dependencies).

Run order (after infra exists & .env set):
```pwsh
python scripts/upload_data_to_blob_storage.py
python scripts/create_azure_ai_indices.py
```

Re-running index creation is safe (idempotent design recommended). If schema changes, you may need to delete indexes manually or adjust code.

---
## Deployment (Azure)
Initial full provision + deploy:
```pwsh
azd auth login
azd up     # provisions infra + builds + deploys code
```
Subsequent code-only deployments:
```pwsh
azd deploy
```
Infra changes only (faster iteration):
```pwsh
azd provision
```
Multiple environments (e.g. `stg`):
```pwsh
azd env new stg
azd up
```

Check environment values:
```pwsh
azd env get-values
```

Logs (App Service via Log Analytics): Use Azure Portal or `az monitor log-analytics query` (workspace ID from Bicep resource `law-...`).

---
## Customizing the System Prompt
Edit the multi-line parameter in `infra/main.bicep` (`systemPrompt`) then:
```pwsh
azd provision   # apply infra update
azd deploy      # (optional) redeploy code
```
Or override at runtime in the Portal (Configuration -> Application settings -> SYSTEM_PROMPT) & restart.

---
## Security Considerations
* Managed identities remove need for API keys between App Service ↔ OpenAI/Search/Storage.
* Ensure `.env` is never committed (use `.env.sample`).
* Regenerate any leaked secrets immediately.
* Restrict network access (future enhancement: private endpoints / VNet integration).

---
## Cost & Scaling
* App Service Plan SKU configurable via `appServicePlanSku` param (`B1`+). Start with `B1` or `S1` and scale as needed.
* Azure OpenAI deployments sized at capacity 20 (adjust in `openAiGptDeployment`, `openAiEmbeddingDeployment`). Lower capacity reduces cost.
* Search SKU defaults to `standard`; consider `basic` for dev.
* Log retention: 30 days (adjust in Log Analytics resource).

---
## Troubleshooting
Issue | Action
------|-------
Auth errors to OpenAI | Verify role assignments (may need a few minutes to propagate) & restart App
Missing env var at runtime | Confirm App Service Configuration includes it (Portal) or redeploy
Index not found | Re-run `create_azure_ai_indices.py` or check index names in `.env`
Slow first response | Cold start / model warm-up; send a small warm-up query after deploy

Collect diagnostic logs quickly:
```pwsh
az webapp log tail -n <appServiceName> -g <resourceGroup>
```

---
## Cleanup
Remove all provisioned resources (irreversible):
```pwsh
azd down
```
Or delete the resource group in the Azure Portal.

---
## Next Improvements (Ideas)
* Add vector store hybrid search scoring customization
* Implement caching layer (Azure Cache for Redis) for frequent queries
* Add CI/CD workflow (GitHub Actions with `azd pipeline config`)
* Add authentication (AAD) for the web front-end
* Private networking & Key Vault integration

---
## License / Attribution
Portions derived from Azure Samples (see original link). Adaptations are project-specific.

---
## Quick Command Reference
```pwsh
# Provision + deploy
azd up

# Data ingest
python scripts/upload_data_to_blob_storage.py
python scripts/create_azure_ai_indices.py

# Local run
uvicorn main:app --reload

# Tear down
azd down
```

---
Happy building! Contributions & refinements welcome.
