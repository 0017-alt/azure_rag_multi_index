* run `python -m venv .venv`
* activate venv
* run `pip install -r requirements.txt`
* run `chmod +x ./scripts/get_microsoft_docs.sh create_indices.sh`
* run `./scripts/get_microsoft_docs.sh`
* run `azd auth login`
* run `azd provision`
* run `azd env get-values` and fill in .env file
* run `./scripts/create_indices.sh`
* run `az login`
* run `pip install -r requirements.txt`
* run `uvicorn main:app`
* try in local
* run `azd up`
