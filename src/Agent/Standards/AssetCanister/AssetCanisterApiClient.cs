using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid;
using System.Threading.Tasks;
using EdjCase.ICP.Agent.Responses;
using System.Collections.Generic;
using System.IO;
using System;
using EdjCase.ICP.Agent.Standards.AssetCanister.Models;
using System.Linq;
using System.Threading;
using System.IO.Compression;

namespace EdjCase.ICP.Agent.Standards.AssetCanister
{
	/// <summary>
	/// Represents a client for interacting with the Asset Canister API.
	/// </summary>
	public class AssetCanisterApiClient
	{
		/// <summary>
		/// The maximum size of an ingress message in bytes.
		/// </summary>
		public const int MAX_INGRESS_MESSAGE_SIZE = 2 * 1024 * 1024; // 2MB
		/// <summary>
		/// The maximum size of a file chunk
		/// It is set to just under 2MB.
		/// </summary>
		public const int MAX_CHUNK_SIZE = MAX_INGRESS_MESSAGE_SIZE - 500; // Just under 2MB

		/// <summary>
		/// The IC agent
		/// </summary>
		public IAgent Agent { get; }

		/// <summary>
		/// The asset canister id
		/// </summary>
		public Principal CanisterId { get; }

		/// <summary>
		/// The optional custom candid converter for request and response objects
		/// </summary>
		public CandidConverter? Converter { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="AssetCanisterApiClient"/> class.
		/// </summary>
		/// <param name="agent">The agent used for communication.</param>
		/// <param name="canisterId">The ID of the asset canister.</param>
		/// <param name="converter">The Candid converter to use for encoding and decoding values (optional).</param>
		public AssetCanisterApiClient(
			IAgent agent,
			Principal canisterId,
			CandidConverter? converter = default
		)
		{
			this.Agent = agent;
			this.CanisterId = canisterId;
			this.Converter = converter;
		}

		/// <summary>
		/// A helper method to upload an asset to the asset canister.
		/// If the asset is larger than <see cref="MAX_CHUNK_SIZE"/>, it will be chunked.
		/// If the asset is smaller than <see cref="MAX_CHUNK_SIZE"/>, it will be uploaded in a single request.
		/// See <see cref="UploadAssetChunkedAsync"/> for more advanced options
		/// </summary>
		/// <param name="key">The key of the asset.</param>
		/// <param name="contentType">The content type of the asset.</param>
		/// <param name="contentEncoding">The content encoding of the asset.</param>
		/// <param name="content">The content of the asset.</param>
		/// <param name="sha256">The SHA256 hash of the asset (optional).</param>
		public async Task UploadAssetSimpleAsync(
			string key,
			string contentType,
			string contentEncoding,
			byte[] content,
			byte[]? sha256 = null
		)
		{
			if (content.Length >= MAX_CHUNK_SIZE)
			{
				// Chunk if too large
				using MemoryStream stream = new MemoryStream(content);
				await this.UploadAssetChunkedAsync(
					key,
					contentType,
					contentEncoding,
					stream,
					sha256
				);
				return;
			}
			await this.StoreAsync(
				key,
				contentType,
				contentEncoding,
				content,
				sha256
			);
		}

		/// <summary>
		///  A helper method to upload an asset to the asset canister in chunks
		/// </summary>
		/// <param name="key">The key of the asset.</param>
		/// <param name="contentType">The content type of the asset.</param>
		/// <param name="contentEncoding">The content encoding of the asset.</param>
		/// <param name="contentStream">The stream containing the asset content.</param>
		/// <param name="sha256">The SHA256 hash of the asset content.</param>
		/// <param name="allowRawAccess">Specifies whether raw access is allowed for the asset.</param>
		/// <param name="enableAliasing">Specifies whether aliasing is enabled for the asset.</param>
		/// <param name="headers">Additional headers to be included in the request.</param>
		/// <param name="maxAge">The maximum age of the asset in seconds.</param>
		public async Task UploadAssetChunkedAsync(
			string key,
			string contentType,
			string contentEncoding,
			Stream contentStream,
			byte[]? sha256 = null,
			bool? allowRawAccess = null,
			bool? enableAliasing = null,
			List<(string, string)>? headers = null,
			ulong? maxAge = null
		)
		{
			CreateBatchResult createBatchResult = await this.CreateBatchAsync();

			try
			{
				List<UnboundedUInt> chunkIds = new();
				byte[] buffer = new byte[MAX_CHUNK_SIZE];
				while (true)
				{
					int bytesRead = await contentStream.ReadAsync(buffer.AsMemory());
					if (bytesRead <= 0)
					{
						break;
					}
					byte[] chunkBytes = bytesRead < buffer.Length
						? buffer[0..bytesRead]
						: buffer;
					CreateChunkResult result = await this.CreateChunkAsync(createBatchResult.BatchId, chunkBytes);
					chunkIds.Add(result.ChunkId);
				}

				OptionalValue<byte[]> sha256Opt = sha256 == null
					? OptionalValue<byte[]>.NoValue()
					: OptionalValue<byte[]>.WithValue(sha256);

				OptionalValue<bool> allowRawAccessOpt = allowRawAccess == null
					? OptionalValue<bool>.NoValue()
					: OptionalValue<bool>.WithValue(allowRawAccess.Value);

				OptionalValue<bool> enableAliasingOpt = enableAliasing == null
					? OptionalValue<bool>.NoValue()
					: OptionalValue<bool>.WithValue(enableAliasing.Value);

				OptionalValue<List<(string, string)>> headersOpt = headers == null
					? OptionalValue<List<(string, string)>>.NoValue()
					: OptionalValue<List<(string, string)>>.WithValue(headers);

				OptionalValue<ulong> maxAgeOpt = maxAge == null
					? OptionalValue<ulong>.NoValue()
					: OptionalValue<ulong>.WithValue(maxAge.Value);


				List<BatchOperationKind> operations = new List<BatchOperationKind>
				{
					BatchOperationKind.CreateAsset(new CreateAssetArguments(key, contentType, maxAgeOpt, headersOpt, enableAliasingOpt, allowRawAccessOpt)),
					BatchOperationKind.SetAssetContent(new SetAssetContentArguments(key, contentEncoding, chunkIds, sha256Opt))
				};

				await this.CommitBatchAsync(createBatchResult.BatchId, operations);

			}
			catch
			{
				// Rollback batch
				await this.DeleteBatchAsync(createBatchResult.BatchId);
			}
		}

		/// <summary>
		/// A helper method to download an asset from the asset canister in chunks.
		/// </summary>
		/// <param name="key">The key of the asset to download.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent chunk downloads.</param>
		/// <param name="acceptEncodings">The encodings to accept for the asset in order. Defaults to ["gzip", "deflate", "br", "identity"]</param>
		/// <returns>The downloaded asset content as a byte array.</returns>
		public async Task<(byte[] Asset, string ContentEncoding)> DownloadAssetAsync(
			string key,
			int maxConcurrency = 10,
			List<string>? acceptEncodings = null
		)
		{
			acceptEncodings ??= new() { "gzip", "deflate", "br", "identity" };
			GetResult result = await this.GetAsync(key, acceptEncodings);

			if (!result.TotalLength.TryToUInt64(out ulong totalLength))
			{
				throw new Exception("Total file length is too large: " + result.TotalLength);
			}
			if (totalLength == (ulong)result.Content.Length)
			{
				return (result.Content, result.ContentEncoding);
			}
			int chunkCount = (int)Math.Ceiling((double)totalLength / result.Content.Length);

			// Create a list to store the chunk tasks
			List<Task<byte[]>> chunkTasks = new();

			// Create a semaphore to limit the number of concurrent tasks
			SemaphoreSlim semaphore = new(maxConcurrency);

			byte[]? sha256 = result.Sha256.GetValueOrDefault();
			// Download the rest of the chunks
			// Skip the first chunk as we already have it
			for (int i = 1; i < chunkCount; i++)
			{
				int chunkIndex = i;
				chunkTasks.Add(Task.Run(async () =>
				{
					await semaphore.WaitAsync();
					try
					{
						GetChunkResult chunkResult = await this.GetChunkAsync(key, result.ContentEncoding, UnboundedUInt.FromUInt64((ulong)chunkIndex), sha256);
						return chunkResult.Content;
					}
					finally
					{
						semaphore.Release();
					}
				}));
			}

			// Wait for all chunk tasks to complete
			await Task.WhenAll(chunkTasks);

			// Combine all the bytes into one byte[]
			byte[] combinedBytes = result.Content.Concat(chunkTasks.SelectMany(t => t.Result)).ToArray();

			return (combinedBytes, result.ContentEncoding);
		}

		/// <summary>
		/// Retrieves the API version for the asset canister
		/// </summary>
		/// <returns>The API version</returns>
		public async Task<ushort> GetApiVersionAsync()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "api_version", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<ushort>(this.Converter);
		}

		/// <summary>
		/// Retrieves the specified asset with the given accept encodings
		/// </summary>
		/// <param name="key">The asset key to get</param>
		/// <param name="acceptEncodings">A list of encodings to accept for the asset</param>
		/// <returns>The result of the get request</returns>
		public async Task<GetResult> GetAsync(string key, List<string> acceptEncodings)
		{
			GetRequest request = new(key, acceptEncodings);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<GetResult>(this.Converter);
		}

		/// <summary>
		/// Retrieves a chunk of data from the asset canister.
		/// </summary>
		/// <param name="key">The key of the asset.</param>
		/// <param name="contentEncoding">The content encoding of the asset.</param>
		/// <param name="index">The index of the chunk.</param>
		/// <param name="sha256">The SHA256 hash of the chunk (optional).</param>
		/// <returns>The result of the chunk retrieval operation.</returns>
		public async Task<GetChunkResult> GetChunkAsync(
			string key,
			string contentEncoding,
			UnboundedUInt index,
			byte[]? sha256 = null
		)
		{
			OptionalValue<byte[]> sha256Opt = sha256 == null
				? OptionalValue<byte[]>.NoValue()
				: OptionalValue<byte[]>.WithValue(sha256);
			GetChunkRequest request = new(key, contentEncoding, index, sha256Opt);

			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_chunk", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<GetChunkResult>(this.Converter);
		}

		/// <summary>
		/// Retrieves a list of assets from the asset canister
		/// </summary>
		/// <returns>List of assets</returns>
		public async Task<List<Asset>> ListAsync()
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.EmptyRecord());
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "list", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<List<Asset>>(this.Converter);
		}

		/// <summary>
		/// Retrieves the certified tree of the asset canister
		/// </summary>
		/// <returns>The certified tree result.</returns>
		public async Task<CertifiedTreeResult> GetCertifiedTreeAsync()
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.EmptyRecord());
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "certified_tree", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<CertifiedTreeResult>(this.Converter);
		}

		/// <summary>
		/// Creates a new batch for asset operations.
		/// </summary>
		/// <returns>The result of the batch creation operation.</returns>
		public async Task<CreateBatchResult> CreateBatchAsync()
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.EmptyRecord());
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "create_batch", arg);
			return reply.ToObjects<CreateBatchResult>(this.Converter);
		}

		/// <summary>
		/// Creates a new chunk in a batch.
		/// </summary>
		/// <param name="batchId">The ID of the batch.</param>
		/// <param name="content">The content of the chunk.</param>
		/// <returns>The result of the chunk creation operation.</returns>
		public async Task<CreateChunkResult> CreateChunkAsync(UnboundedUInt batchId, byte[] content)
		{
			CreateChunkRequest request = new(batchId, content);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "create_chunk", arg);
			return reply.ToObjects<CreateChunkResult>(this.Converter);
		}

		/// <summary>
		/// Commits a batch of operations to the asset canister.
		/// </summary>
		/// <param name="batchId">The ID of the batch.</param>
		/// <param name="operations">The operations to commit.</param>
		public async Task CommitBatchAsync(UnboundedUInt batchId, List<BatchOperationKind> operations)
		{
			CommitBatchArguments request = new(batchId, operations);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "commit_batch", arg);
		}

		/// <summary>
		/// Proposes a batch of operations for later commitment.
		/// </summary>
		/// <param name="batchId">The ID of the batch.</param>
		/// <param name="operations">The operations to propose.</param>
		public async Task ProposeCommitBatchAsync(UnboundedUInt batchId, List<BatchOperationKind> operations)
		{
			CommitBatchArguments request = new(batchId, operations);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "propose_commit_batch", arg);
		}

		/// <summary>
		/// Commits a previously proposed batch of operations.
		/// </summary>
		/// <param name="batchId">The ID of the batch.</param>
		/// <param name="evidence">The evidence for the proposed batch.</param>
		public async Task CommitProposedBatchAsync(UnboundedUInt batchId, byte[] evidence)
		{
			CommitProposedBatchRequest request = new(batchId, evidence);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "commit_proposed_batch", arg);
		}

		/// <summary>
		/// Computes evidence for a proposed batch of operations.
		/// </summary>
		/// <param name="batchId">The ID of the batch.</param>
		/// <param name="maxIterations">The maximum number of iterations (optional).</param>
		/// <returns>The computed evidence.</returns>
		public async Task<OptionalValue<byte[]>> ComputeEvidenceAsync(UnboundedUInt batchId, ushort? maxIterations = null)
		{
			OptionalValue<ushort> maxIterationsOpt = maxIterations == null
				? OptionalValue<ushort>.NoValue()
				: OptionalValue<ushort>.WithValue(maxIterations.Value);
			ComputeEvidenceArguments request = new ComputeEvidenceArguments(batchId, maxIterationsOpt);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "compute_evidence", arg);
			return reply.ToObjects<OptionalValue<byte[]>>(this.Converter);
		}

		/// <summary>
		/// Deletes a batch of operations.
		/// </summary>
		/// <param name="batchId">The ID of the batch.</param>
		public async Task DeleteBatchAsync(UnboundedUInt batchId)
		{
			DeleteBatchArguments request = new(batchId);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "delete_batch", arg);
		}

		/// <summary>
		/// Creates a new asset in the asset canister.
		/// </summary>
		/// <param name="key">The key of the asset.</param>
		/// <param name="contentType">The content type of the asset.</param>
		/// <param name="maxAge">The maximum age of the asset in seconds (optional).</param>
		/// <param name="headers">Additional headers for the asset (optional).</param>
		/// <param name="enableAliasing">Specifies whether aliasing is enabled for the asset (optional).</param>
		/// <param name="allowRawAccess">Specifies whether raw access is allowed for the asset (optional).</param>

		public async Task CreateAssetAsync(
			string key,
			string contentType,
			ulong? maxAge = null,
			List<(string, string)>? headers = null,
			bool? enableAliasing = null,
			bool? allowRawAccess = null
		)
		{
			OptionalValue<ulong> maxAgeOpt = maxAge == null
				? OptionalValue<ulong>.NoValue()
				: OptionalValue<ulong>.WithValue(maxAge.Value);

			OptionalValue<List<(string, string)>> headersOpt = headers == null
				? OptionalValue<List<(string, string)>>.NoValue()
				: OptionalValue<List<(string, string)>>.WithValue(headers);

			OptionalValue<bool> enableAliasingOpt = enableAliasing == null
				? OptionalValue<bool>.NoValue()
				: OptionalValue<bool>.WithValue(enableAliasing.Value);

			OptionalValue<bool> allowRawAccessOpt = allowRawAccess == null
				? OptionalValue<bool>.NoValue()
				: OptionalValue<bool>.WithValue(allowRawAccess.Value);
			CreateAssetArguments request = new(
				key,
				contentType,
				maxAgeOpt,
				headersOpt,
				enableAliasingOpt,
				allowRawAccessOpt
			);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "create_asset", arg);
		}

		/// <summary>
		/// Sets the content for an asset in the asset canister.
		/// </summary>
		/// <param name="key">The key of the asset.</param>
		/// <param name="contentEncoding">The content encoding of the asset.</param>
		/// <param name="chunkIds">The IDs of the chunks comprising the asset content.</param>
		/// <param name="sha256">The SHA256 hash of the asset content (optional).</param>
		public async Task SetAssetContentAsync(
			string key,
			string contentEncoding,
			List<UnboundedUInt> chunkIds,
			byte[]? sha256 = null
		)
		{
			OptionalValue<byte[]> sha256Opt = sha256 == null
				? OptionalValue<byte[]>.NoValue()
				: OptionalValue<byte[]>.WithValue(sha256);
			SetAssetContentArguments request = new(key, contentEncoding, chunkIds, sha256Opt);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "set_asset_content", arg);
		}

		/// <summary>
		/// Removes a content encoding from an asset in the asset canister.
		/// </summary>
		/// <param name="key">The key of the asset.</param>
		/// <param name="contentEncoding">The content encoding to remove.</param>

		public async Task UnsetAssetContentAsync(string key, string contentEncoding)
		{
			UnsetAssetContentArguments request = new(key, contentEncoding);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "unset_asset_content", arg);
		}

		/// <summary>
		/// Deletes an asset from the asset canister.
		/// </summary>
		/// <param name="key">The key of the asset to delete.</param>
		public async Task DeleteAssetAsync(string key)
		{
			DeleteAssetArguments request = new(key);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "delete_asset", arg);
		}

		/// <summary>
		/// Deletes all assets from the asset canister.
		/// </summary>
		public async Task ClearAsync()
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.EmptyRecord());
			await this.Agent.CallAsync(this.CanisterId, "clear", arg);
		}

		/// <summary>
		/// Stores an asset in the asset canister.
		/// </summary>
		/// <param name="key">The key of the asset.</param>
		/// <param name="contentType">The content type of the asset.</param>
		/// <param name="contextEncoding">The content encoding of the asset.</param>
		/// <param name="content">The content of the asset.</param>
		/// <param name="sha256">The SHA256 hash of the asset (optional).</param>
		public async Task StoreAsync(
			string key,
			string contentType,
			string contextEncoding,
			byte[] content,
			byte[]? sha256 = null
		)
		{
			OptionalValue<byte[]> sha256Opt = sha256 == null
				? OptionalValue<byte[]>.NoValue()
				: OptionalValue<byte[]>.WithValue(sha256);
			StoreRequest request = new(key, contentType, contextEncoding, content, sha256Opt);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "store", arg);
		}

		/// <summary>
		/// Grants the Commit permission to an identity
		/// </summary>
		/// <param name="principal">The principal to authorize.</param>
		public async Task AuthorizeAsync(Principal principal)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(principal, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "authorize", arg);
		}

		/// <summary>
		/// Revokes the Commit permission from a principal.
		/// </summary>
		/// <param name="principal">The principal to deauthorize.</param>
		public async Task DeauthorizeAsync(Principal principal)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(principal, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "deauthorize", arg);
		}

		/// <summary>
		/// Retrieves a list of principals with the Commit permission.
		/// </summary>
		/// <returns>List of principals.</returns>
		public async Task<List<Principal>> ListAuthorizedAsync()
		{
			CandidArg arg = CandidArg.FromCandid();
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "list_authorized", arg);
			return reply.ToObjects<List<Principal>>(this.Converter);
		}

		/// <summary>
		/// Grants a permission to an identity
		/// </summary>
		/// <param name="principal">The principal to grant permission to.</param>
		/// <param name="permission">The permission to grant.</param>
		public async Task GrantPermissionAsync(Principal principal, Permission permission)
		{
			GrantPermission request = new(principal, permission);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "grant_permission", arg);
		}

		/// <summary>
		/// Revokes a permission from a principal.
		/// </summary>
		/// <param name="principal">The principal to revoke permission from.</param>
		/// <param name="permission">The permission to revoke.</param>
		public async Task RevokePermissionAsync(Principal principal, Permission permission)
		{
			RevokePermission request = new RevokePermission(principal, permission);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "revoke_permission", arg);
		}

		/// <summary>
		/// Retrieves a list of principals with a specified permission.
		/// </summary>
		/// <param name="withPermission">The permission to check.</param>
		/// <returns>List of principals.</returns>
		public async Task<List<Principal>> ListPermittedAsync(Permission withPermission)
		{
			ListPermitted request = new ListPermitted(withPermission);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "list_permitted", arg);
			return reply.ToObjects<List<Principal>>(this.Converter);
		}

		/// <summary>
		/// Takes ownership of the asset canister.
		/// </summary>
		public async Task TakeOwnershipAsync()
		{
			CandidArg arg = CandidArg.FromCandid();
			await this.Agent.CallAsync(this.CanisterId, "take_ownership", arg);
		}

		/// <summary>
		/// Retrieves properties of a specific asset.
		/// </summary>
		/// <param name="key">The key of the asset.</param>
		/// <returns>The asset properties.</returns>
		public async Task<GetAssetPropertiesResult> GetAssetPropertiesAsync(string key)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(key, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_asset_properties", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<GetAssetPropertiesResult>(this.Converter);
		}

		/// <summary>
		/// Sets properties for an asset in the asset canister.
		/// </summary>
		/// <param name="request">The request containing the asset properties to set.</param>
		public async Task SetAssetPropertiesAsync(SetAssetPropertiesRequest request)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "set_asset_properties", arg);
		}

		/// <summary>
		/// Retrieves the configuration of the asset canister.
		/// </summary>
		/// <returns>The configuration response.</returns>
		public async Task<ConfigurationResponse> GetConfigurationAsync()
		{
			CandidArg arg = CandidArg.FromCandid();
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "get_configuration", arg);
			return reply.ToObjects<ConfigurationResponse>(this.Converter);
		}

		/// <summary>
		/// Configures the asset canister.
		/// </summary>
		/// <param name="request">The request containing the configuration settings.</param>
		public async Task ConfigureAsync(ConfigureRequest request)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "configure", arg);
		}

		/// <summary>
		/// Validates the granting of a permission to a principal.
		/// </summary>
		/// <param name="principal">The principal to grant permission to.</param>
		/// <param name="permission">The permission to grant.</param>
		/// <returns>The validation result.</returns>
		public async Task<ValidationResult> ValidateGrantPermissionAsync(Principal principal, Permission permission)
		{
			GrantPermission request = new(principal, permission);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "validate_grant_permission", arg);
			return reply.ToObjects<ValidationResult>(this.Converter);
		}

		/// <summary>
		/// Validates the revocation of a permission from a principal.
		/// </summary>
		/// <param name="principal">The principal to revoke permission from.</param>
		/// <param name="permission">The permission to revoke.</param>
		/// <returns>The validation result.</returns>
		public async Task<ValidationResult> ValidateRevokePermissionAsync(Principal principal, Permission permission)
		{
			RevokePermission request = new(principal, permission);
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "validate_revoke_permission", arg);
			return reply.ToObjects<ValidationResult>(this.Converter);
		}

		/// <summary>
		/// Validates the taking of ownership of the asset canister.
		/// </summary>
		/// <returns>The validation result.</returns>
		public async Task<ValidationResult> ValidateTakeOwnershipAsync()
		{
			CandidArg arg = CandidArg.FromCandid();
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "validate_take_ownership", arg);
			return reply.ToObjects<ValidationResult>(this.Converter);
		}

		/// <summary>
		/// Validates the commitment of a proposed batch of operations.
		/// </summary>
		/// <param name="request">The request containing the proposed batch details.</param>
		/// <returns>The validation result.</returns>
		public async Task<ValidationResult> ValidateCommitProposedBatchAsync(CommitProposedBatchRequest request)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "validate_commit_proposed_batch", arg);
			return reply.ToObjects<ValidationResult>(this.Converter);
		}

		/// <summary>
		/// Validates the configuration settings for the asset canister.
		/// </summary>
		/// <param name="request">The request containing the configuration settings.</param>
		/// <returns>The validation result.</returns>
		public async Task<ValidationResult> ValidateConfigureAsync(ConfigureRequest request)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(request, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "validate_configure", arg);
			return reply.ToObjects<ValidationResult>(this.Converter);
		}
	}
}
