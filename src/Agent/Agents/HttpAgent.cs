using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Agent.Requests;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using EdjCase.ICP.Agent.Agents.Http;
using System.Net.Http.Headers;
using System.Formats.Cbor;
using EdjCase.ICP.Candid.Encodings;
using System.Linq;
using EdjCase.ICP.BLS;
using System.Threading;
using System.Security.Cryptography;
using System.Diagnostics;

namespace EdjCase.ICP.Agent.Agents
{
	/// <summary>
	/// An `IAgent` implementation using HTTP to make requests to the IC
	/// </summary>
	public class HttpAgent : IAgent
	{
		private byte[]? rootKeyCache = null;

		/// <summary>
		/// The identity that will be used on each request unless overriden
		/// This identity can be anonymous
		/// </summary>
		public IIdentity? Identity { get; set; }

		private readonly IHttpClient httpClient;
		private readonly IBlsCryptography bls;
		private bool v3CallSupported = true;

		/// <param name="identity">Optional. Identity to use for each request. If unspecified, will use anonymous identity</param>
		/// <param name="bls">Optional. Bls crypto implementation to validate signatures. If unspecified, will use default implementation</param>
		/// <param name="httpClient">Optional. Sets the http client to use, otherwise will use the default http client</param>
		public HttpAgent(
			IHttpClient httpClient,
			IIdentity? identity = null,
			IBlsCryptography? bls = null
		)
		{
			this.Identity = identity;
			this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			this.bls = bls ?? new DefaultBlsCryptograhy();
		}

		/// <param name="identity">Optional. Identity to use for each request. If unspecified, will use anonymous identity</param>
		/// <param name="bls">Optional. Bls crypto implementation to validate signatures. If unspecified, will use default implementation</param>
		/// <param name="httpBoundryNodeUrl">Url to the boundry node to connect to. Defaults to `https://ic0.app/`</param>
		public HttpAgent(
			IIdentity? identity = null,
			Uri? httpBoundryNodeUrl = null,
			IBlsCryptography? bls = null
		)
		{
			this.Identity = identity;
			this.httpClient = new DefaultHttpClient(new HttpClient()
			{
				BaseAddress = httpBoundryNodeUrl ?? new Uri("https://ic0.app/")
			});
			this.bls = bls ?? new DefaultBlsCryptograhy();
		}

		/// <inheritdoc/>
		public async Task<CandidArg> CallAsync(
			Principal canisterId,
			string method,
			CandidArg arg,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null
		)
		{
			if (!this.v3CallSupported)
			{
				return await this.CallAsynchronousAndWaitAsync(canisterId, method, arg, effectiveCanisterId, cancellationToken);
			}
			effectiveCanisterId ??= canisterId;
			(HttpResponse httpResponse, RequestId requestId) = await this.SendAsync($"/api/v3/canister/{effectiveCanisterId.ToText()}/call", BuildRequest, cancellationToken);

			if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
			{
				// If v3 is not available, fall back to v2
				this.v3CallSupported = false;
				return await this.CallAsynchronousAndWaitAsync(canisterId, method, arg, effectiveCanisterId, cancellationToken);
			}
			if (httpResponse.StatusCode == System.Net.HttpStatusCode.Accepted)
			{
				// If request takes too long, then it will return 202 Accepted and polling is required
				return await this.WaitForRequestAsync(canisterId, requestId, cancellationToken);
			}
			await httpResponse.ThrowIfErrorAsync();

			byte[] cborBytes = await httpResponse.GetContentAsync();
			var reader = new CborReader(cborBytes);
			V3CallResponse v3CallResponse = V3CallResponse.ReadCbor(reader);

			SubjectPublicKeyInfo rootPublicKey = await this.GetRootKeyAsync(cancellationToken);
			if (!v3CallResponse.Certificate.IsValid(this.bls, rootPublicKey))
			{
				throw new InvalidCertificateException("Certificate signature does not match the IC public key");
			}
			HashTree? requestStatusData = v3CallResponse.Certificate.Tree.GetValueOrDefault(StatePath.FromSegments("request_status", requestId.RawValue));
			RequestStatus? requestStatus = ParseRequestStatus(requestStatusData);
			switch (requestStatus?.Type)
			{
				case RequestStatus.StatusType.Replied:
					return requestStatus.AsReplied();
				case RequestStatus.StatusType.Rejected:
					(RejectCode code, string message, string? errorCode) = requestStatus.AsRejected();
					throw new CallRejectedException(code, message, errorCode);
				case RequestStatus.StatusType.Done:
					throw new RequestCleanedUpException();
				case null:
				case RequestStatus.StatusType.Received:
				case RequestStatus.StatusType.Processing:
					throw new InvalidOperationException("V3 calls should not return null/received/processing status");
				default:
					throw new NotImplementedException($"Invalid request status '{requestStatus.Type}'");
			}

			CallRequest BuildRequest(Principal sender, ICTimestamp now)
			{
				byte[] nonce = new byte[16];
				RandomNumberGenerator.Fill(nonce);
				return new CallRequest(canisterId, method, arg, sender, now, nonce);
			}
		}


		/// <inheritdoc/>
		public async Task<RequestId> CallAsynchronousAsync(
			Principal canisterId,
			string method,
			CandidArg arg,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null
		)
		{
			if (effectiveCanisterId == null)
			{
				effectiveCanisterId = canisterId;
			}
			(HttpResponse httpResponse, RequestId requestId) = await this.SendAsync($"/api/v2/canister/{effectiveCanisterId.ToText()}/call", BuildRequest, cancellationToken);

			await httpResponse.ThrowIfErrorAsync();
			if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
			{
				// If returns with a body, then an error happened https://forum.dfinity.org/t/breaking-changes-to-the-replica-api-agent-developers-take-note/19651

				byte[] cborBytes = await httpResponse.GetContentAsync();
				var reader = new CborReader(cborBytes);
				CallRejectedResponse response;
				try
				{
					response = CallRejectedResponse.FromCbor(reader);
				}
				catch (Exception ex)
				{
					string message = "Unable to parse call rejected cbor response.\n" +
						"Response bytes: " + ByteUtil.ToHexString(cborBytes);
					throw new Exception(message, ex);
				}
				throw new CallRejectedException(response.Code, response.Message, response.ErrorCode);
			}
			return requestId;

			CallRequest BuildRequest(Principal sender, ICTimestamp now)
			{
				byte[] nonce = new byte[16];
				RandomNumberGenerator.Fill(nonce);
				return new CallRequest(canisterId, method, arg, sender, now, nonce);
			}
		}

		/// <inheritdoc/>
		public async Task<QueryResponse> QueryAsync(
			Principal canisterId,
			string method,
			CandidArg arg,
			CancellationToken? cancellationToken = null
		)
		{
			(HttpResponse httpResponse, RequestId requestId) = await this.SendAsync($"/api/v2/canister/{canisterId.ToText()}/query", BuildRequest, cancellationToken);
			await httpResponse.ThrowIfErrorAsync();
			byte[] cborBytes = await httpResponse.GetContentAsync();
			return QueryResponse.ReadCbor(new CborReader(cborBytes));

			QueryRequest BuildRequest(Principal sender, ICTimestamp now)
			{
				return new QueryRequest(canisterId, method, arg, sender, now);
			}
		}

		/// <inheritdoc/>
		public async Task<ReadStateResponse> ReadStateAsync(
			Principal canisterId,
			List<StatePath> paths,
			CancellationToken? cancellationToken = null
		)
		{
			string url = $"/api/v2/canister/{canisterId.ToText()}/read_state";
			(HttpResponse httpResponse, RequestId requestId) = await this.SendAsync(url, BuildRequest, cancellationToken);

			await httpResponse.ThrowIfErrorAsync();
			byte[] cborBytes = await httpResponse.GetContentAsync();
			var reader = new CborReader(cborBytes);
			ReadStateResponse response = ReadStateResponse.ReadCbor(reader);

			SubjectPublicKeyInfo rootPublicKey = await this.GetRootKeyAsync(cancellationToken);
			if (!response.Certificate.IsValid(this.bls, rootPublicKey))
			{
				throw new InvalidCertificateException("Certificate signature does not match the IC public key");
			}

			return response;

			ReadStateRequest BuildRequest(Principal sender, ICTimestamp now)
			{
				return new ReadStateRequest(paths, sender, now);
			}
		}

		/// <inheritdoc/>
		public async Task<RequestStatus?> GetRequestStatusAsync(
			Principal canisterId,
			RequestId id,
			CancellationToken? cancellationToken = null
		)
		{
			var pathRequestStatus = StatePath.FromSegments("request_status", id.RawValue);
			var paths = new List<StatePath> { pathRequestStatus };
			ReadStateResponse response = await this.ReadStateAsync(canisterId, paths, cancellationToken);
			HashTree? requestStatus = response.Certificate.Tree.GetValueOrDefault(pathRequestStatus);
			return ParseRequestStatus(requestStatus);
		}

		private static RequestStatus? ParseRequestStatus(HashTree? requestStatus)
		{
			string? status = requestStatus?.GetValueOrDefault("status")?.AsLeaf().AsUtf8();
			//received, processing, replied, rejected or done
			switch (status)
			{
				case null:
					return null;
				case "received":
					return RequestStatus.Received();
				case "processing":
					return RequestStatus.Processing();
				case "replied":
					HashTree.EncodedValue r = requestStatus!.GetValueOrDefault("reply")!.AsLeaf();
					return RequestStatus.Replied(CandidArg.FromBytes(r));
				case "rejected":
					RejectCode code = (RejectCode)(ulong)requestStatus!.GetValueOrDefault("reject_code")!.AsLeaf().AsNat();
					string message = requestStatus.GetValueOrDefault("reject_message")!.AsLeaf().AsUtf8();
					string? errorCode = requestStatus.GetValueOrDefault("error_code")?.AsLeaf().AsUtf8();
					return RequestStatus.Rejected(code, message, errorCode);
				case "done":
					return RequestStatus.Done();
				default:
					throw new NotImplementedException($"Invalid request status '{status}'");
			}
		}


		/// <inheritdoc/>
		public async Task<SubjectPublicKeyInfo> GetRootKeyAsync(
			CancellationToken? cancellationToken = null
		)
		{
			if (this.rootKeyCache == null)
			{
				StatusResponse jsonObject = await this.GetReplicaStatusAsync(cancellationToken);
				this.rootKeyCache = jsonObject.DevelopmentRootKey;
				if (this.rootKeyCache == null)
				{
					// If not specified, use main net
					return SubjectPublicKeyInfo.MainNetRootPublicKey;
				}
			}
			return SubjectPublicKeyInfo.FromDerEncoding(this.rootKeyCache);
		}


		/// <inheritdoc/>
		public async Task<StatusResponse> GetReplicaStatusAsync(
			CancellationToken? cancellationToken = null
		)
		{
			HttpResponse httpResponse = await this.httpClient.GetAsync("/api/v2/status", cancellationToken);
			await httpResponse.ThrowIfErrorAsync();
			byte[] bytes = await httpResponse.GetContentAsync();
			return StatusResponse.ReadCbor(new CborReader(bytes));
		}

		private async Task<(HttpResponse Response, RequestId RequestId)> SendAsync<TRequest>(
			string url,
			Func<Principal, ICTimestamp, TRequest> getRequest,
			CancellationToken? cancellationToken = null
		)
			where TRequest : IRepresentationIndependentHashItem
		{
			Principal principal;
			if (this.Identity == null)
			{
				principal = Principal.Anonymous();
			}
			else
			{
				SubjectPublicKeyInfo publicKey = this.Identity.GetPublicKey();
				principal = publicKey.ToPrincipal();
			}
			TRequest request = getRequest(principal, ICTimestamp.Future(TimeSpan.FromMinutes(3)));
			Dictionary<string, IHashable> content = request.BuildHashableItem();

			SignedContent signedContent;
			if (this.Identity == null)
			{
				signedContent = new SignedContent(content, null, null, null);
			}
			else
			{
				signedContent = this.Identity.SignContent(content);
			}


			byte[] cborBody = this.SerializeSignedContent(signedContent);
#if DEBUG
			string hex = ByteUtil.ToHexString(cborBody);
#endif
			HttpResponse httpResponse = await this.httpClient.PostAsync(url, cborBody, cancellationToken);
			var sha256 = SHA256HashFunction.Create();
			RequestId requestId = RequestId.FromObject(content, sha256); // TODO this is redundant, `CreateSignedContent` hashes it too
			return (httpResponse, requestId);

		}

		private byte[] SerializeSignedContent(SignedContent signedContent)
		{
			var writer = new CborWriter();
			writer.WriteTag(CborTag.SelfDescribeCbor);
			signedContent.WriteCbor(writer);
			return writer.Encode();
		}

	}

}


