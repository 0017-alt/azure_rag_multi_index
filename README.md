```shell
$ python -m venv .venv # activate venv
$ pip install -r requirements.txt
$ azd auth login
$ azd provision
$ azd env get-values # then, fill in .env file
$ python ./scripts/upload_data_to_blob_storage.py # upload docs to Azure Blob Container
$ python ./scripts/create_index.py# 
$ az login
$ pip install -r requirements.txt
$ uvicorn main:app # you can try locally
$ azd up # deploy in Azure
```
