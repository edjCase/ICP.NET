# ICP.NET CLI Samples

## Usage
```
cd samples/Sample.CLI

dotnet run
```

### Asset upload example
Command: upload-asset

Required:
- canister_id - The asset canister id to use
- asset_key - The key/id of the asset
- encoding - The encoding of the file (e.g UTF-8)
- content_type - The content type of the asset (e.g text/plain)
- file_path - The path of the file to upload

Optional:
- ic_url - Defaults to icp-api.io. Use if wanting to use local (e.g http://127.0.0.1:4943)
- pem_file_path - Use to set identity of upload request. Defaults to anonymous identity
- pem_password - Use if the pem_file_path is set and it is encrypted
```
dotnet run -- upload-asset -c {canister_id} -k {asset_key} -e {encoding} -t {content_type} -f {file_path} -u {ic_url} -i {pem_file_path} -p {pem_password} 
```


### Asset download example
Command: download-asset

Required:
- canister_id - The asset canister id to use
- asset_key - The key/id of the asset
- encoding - The encoding of the file (e.g UTF-8)
- output_file_path - The path of the file to download to

Optional:
- ic_url - Defaults to icp-api.io. Use if wanting to use local (e.g http://127.0.0.1:4943)
- pem_file_path - Use to set identity of upload request. Defaults to anonymous identity
- pem_password - Use if the pem_file_path is set and it is encrypted
```
dotnet run -- download-asset -u {ic_url} -c {canister_id} -k {asset_key} -e {encoding} -f {output_file_path} -i {pem_file_path} -p {pem_password}
```
