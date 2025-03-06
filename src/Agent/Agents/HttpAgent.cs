using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Agent.Requests;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Utilities;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using EdjCase.ICP.Agent.Agents.Http;
using System.Formats.Cbor;
using EdjCase.ICP.BLS;
using System.Threading;
using System.Security.Cryptography;
using Org.BouncyCastle.Asn1.Ocsp;

namespace EdjCase.ICP.Agent.Agents
{
	/// <summary>
	/// An `IAgent` implementation using HTTP to make requests to the IC
	/// </summary>
	public class HttpAgent : IAgent
	{
		private byte[]? rootKeyCache = null;

		private readonly IHttpClient httpClient;
		private bool skipCertificateValidation = false;
		private bool v3CallSupported = true;

		/// <param name="httpClient">Optional. Sets the http client to use, otherwise will use the default http client</param>
		/// <param name="skipCertificateValidation">If true, will skip response certificate validation. Defaults to false</param>
		public HttpAgent(IHttpClient httpClient, bool skipCertificateValidation = false)
		{
			this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			this.skipCertificateValidation = skipCertificateValidation;
		}

		/// <param name="httpBoundryNodeUrl">Url to the boundry node to connect to. Defaults to `https://ic0.app/`</param>
		/// <param name="skipCertificateValidation">If true, will skip response certificate validation. Defaults to false</param>
		public HttpAgent(Uri? httpBoundryNodeUrl = null, bool skipCertificateValidation = false)
		{
			this.httpClient = new DefaultHttpClient(new HttpClient()
			{
				BaseAddress = httpBoundryNodeUrl ?? new Uri("https://icp-api.io/")
			});
			this.skipCertificateValidation = skipCertificateValidation;
		}





		/// <inheritdoc/>
		public async Task<CandidArg> CallAsync(
			SignedContent<CallRequest> content,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null
		)
		{
			effectiveCanisterId ??= content.Request.CanisterId;
			string url = this.GetCallUrl(effectiveCanisterId, this.v3CallSupported);

			HttpResponse httpResponse = await this.SendAsync(url, content, cancellationToken);


			if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
			{
				// If v3 is not available, fall back to v2
				this.v3CallSupported = false;
				return await this.CallAsynchronousAndWaitAsync(content, effectiveCanisterId, cancellationToken);
			}
			if (httpResponse.StatusCode == System.Net.HttpStatusCode.Accepted)
			{
				// If request takes too long, then it will return 202 Accepted and polling is required
				return await this.WaitForRequestAsync(content.Request.CanisterId, content.RequestId, cancellationToken);
			}
			await httpResponse.ThrowIfErrorAsync();

			byte[] cborBytes = await httpResponse.GetContentAsync();
			var reader = new CborReader(cborBytes);
			V3CallResponse v3CallResponse = V3CallResponse.ReadCbor(reader);

			if (!this.skipCertificateValidation)
			{
				SubjectPublicKeyInfo rootPublicKey = await this.GetRootKeyAsync(cancellationToken);
				if (!v3CallResponse.Certificate.IsValid(rootPublicKey))
				{
					throw new InvalidCertificateException("Certificate signature does not match the IC public key");
				}
			}
			HashTree? requestStatusData = v3CallResponse.Certificate.Tree.GetValueOrDefault(StatePath.FromSegments("request_status", content.RequestId.RawValue));
			RequestStatus? requestStatus = IAgentExtensions.ParseRequestStatus(requestStatusData);
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
		}

		/// <inheritdoc/>
		public async Task<RequestId> CallAsynchronousAsync(
			SignedContent<CallRequest> content,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null
		)
		{
			effectiveCanisterId ??= content.Request.CanisterId;
			string url = this.GetCallUrl(effectiveCanisterId, false);

			HttpResponse httpResponse = await this.SendAsync(url, content, cancellationToken);

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
			return content.RequestId;
		}

		/// <inheritdoc/>
		public async Task<CandidArg> QueryAsync(
			SignedContent<QueryRequest> content,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null
		)
		{
			effectiveCanisterId ??= content.Request.CanisterId;
			HttpResponse httpResponse = await this.SendAsync($"/api/v2/canister/{effectiveCanisterId.ToText()}/query", content, cancellationToken);
			await httpResponse.ThrowIfErrorAsync();
			byte[] cborBytes = await httpResponse.GetContentAsync();
			return QueryResponse.ReadCbor(new CborReader(cborBytes)).ThrowOrGetReply();
		}

		/// <inheritdoc/>
		public async Task<ReadStateResponse> ReadStateAsync(
			Principal canisterId,
			SignedContent<ReadStateRequest> content,
			CancellationToken? cancellationToken = null
		)
		{
			string url = $"/api/v2/canister/{canisterId.ToText()}/read_state";
			HttpResponse httpResponse = await this.SendAsync(url, content, cancellationToken);

			await httpResponse.ThrowIfErrorAsync();
			byte[] cborBytes = await httpResponse.GetContentAsync();
			var reader = new CborReader(cborBytes);
			ReadStateResponse response = ReadStateResponse.ReadCbor(reader);

			if (!this.skipCertificateValidation)
			{
				SubjectPublicKeyInfo rootPublicKey = await this.GetRootKeyAsync(cancellationToken);
				if (!response.Certificate.IsValid(rootPublicKey))
				{
					throw new InvalidCertificateException("Certificate signature does not match the IC public key");
				}
			}

			return response;
		}


















		private string GetCallUrl(Principal canisterId, bool v3)
		{
			if (v3)
			{
				return $"/api/v3/canister/{canisterId.ToText()}/call";
			}
			return $"/api/v2/canister/{canisterId.ToText()}/call";
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


		private async Task<HttpResponse> SendAsync<TRequest>(
			string url,
			SignedContent<TRequest> content,
			CancellationToken? cancellationToken = null
		)
			where TRequest : IRepresentationIndependentHashItem
		{
			byte[] cborBody = content.ToCborBytes();
#if DEBUG
			string hex = ByteUtil.ToHexString(cborBody);
#endif
			return await this.httpClient.PostAsync(url, cborBody, cancellationToken);


		}
	}

}


