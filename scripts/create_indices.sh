#!/bin/bash

set -e

.venv/Scripts/activate

python ./scripts/upload_data_to_blob_storage.py
python ./scripts/create_index.py

azd env set SEARCH_INDEX_NAME