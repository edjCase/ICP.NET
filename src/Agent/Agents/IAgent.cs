using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Agent.Requests;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace EdjCase.ICP.Agent.Agents
{
	/// <summary>
	/// An agent is used to communicate with the Internet Computer with certain protocols that
	/// are specific to an `IAgent` implementation
	/// </summary>
	public interface IAgent
	{
		Task<CandidArg> CallAsync(SignedContent<CallRequest> content, Principal? effectiveCanisterId = null, CancellationToken? cancellationToken = null);
		Task<RequestId> CallAsynchronousAsync(SignedContent<CallRequest> content, Principal? effectiveCanisterId = null, CancellationToken? cancellationToken = null);
		Task<CandidArg> QueryAsync(SignedContent<QueryRequest> content, Principal? effectiveCanisterId = null, CancellationToken? cancellationToken = null);
		Task<ReadStateResponse> ReadStateAsync(Principal canisterId, SignedContent<ReadStateRequest> content, CancellationToken? cancellationToken = null);

		/// <summary>
		/// Gets the status of the IC replica. This includes versioning information
		/// about the replica
		/// </summary>
		/// <returns>A response containing all replica status information</returns>
		Task<StatusResponse> GetReplicaStatusAsync(CancellationToken? cancellationToken = null);

		/// <summary>
		/// Gets the root public key of the current Internet Computer network
		/// </summary>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The root public key bytes </returns>
		Task<SubjectPublicKeyInfo> GetRootKeyAsync(CancellationToken? cancellationToken = null);


		/// <summary>
		/// Gets the state of a specified canister with the subset of state information
		/// specified by the paths parameter
		/// </summary>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="paths">The state paths to get information for. Other state data will be pruned if not specified</param>
		/// <param name="identity">Optional. Identity to sign the request with</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>A response that contains the certificate of the current canister state</returns>
		public async Task<ReadStateResponse> ReadStateAsync(
			Principal canisterId,
			List<StatePath> paths,
			IIdentity? identity = null,
			CancellationToken? cancellationToken = null)
		{
			SignedContent<ReadStateRequest> content = this.SignContent(
				identity,
				(sender, ingressExpiry) => new ReadStateRequest(paths, sender, ingressExpiry)
			);
			return await this.ReadStateAsync(canisterId, content, cancellationToken);
		}


		/// <summary>
		/// Sends a call request to a specified canister method and gets the response candid arg back using /v3/../call
		/// and falls back to /v2/../call if the v3 is not available
		/// </summary>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="method">The name of the method to call on the canister</param>
		/// <param name="arg">The candid arg to send with the request</param>
		/// <param name="identity">Optional. Identity to sign the request with</param>
		/// <param name="nonce">Optional. If specified will make the request unique even with the same arguments</param>
		/// <param name="effectiveCanisterId">Optional. Specifies the relevant canister id if calling the root canister</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The id of the request that can be used to look up its status with `GetRequestStatusAsync`</returns>
		public async Task<CandidArg> CallAsync(
			Principal canisterId,
			string method,
			CandidArg arg,
			IIdentity? identity = null,
			byte[]? nonce = null,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null
		)
		{
			SignedContent<CallRequest> content = this.SignContent(
				identity,
				(sender, ingressExpiry) =>
				{
					return new CallRequest(canisterId, method, arg, sender, ingressExpiry, nonce);
				}
			);
			return await this.CallAsync(content, effectiveCanisterId, cancellationToken);
		}


		/// <summary>
		/// Sends a query request to a specified canister method
		/// </summary>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="method">The name of the method to call on the canister</param>
		/// <param name="arg">The candid arg to send with the request</param>
		/// <param name="nonce">Optional. If specified will make the request unique even with the same arguments</param>
		/// <param name="identity">Optional. Identity to sign the request with</param>
		/// <param name="effectiveCanisterId">Optional. Specifies the relevant canister id if calling the root canister</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The response data of the query call</returns>
		public async Task<CandidArg> QueryAsync(
			Principal canisterId,
			string method,
			CandidArg arg,
			byte[]? nonce = null,
			IIdentity? identity = null,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null
		)
		{
			SignedContent<QueryRequest> content = this.SignContent(
				identity,
				(sender, ingressExpiry) => new QueryRequest(canisterId, method, arg, sender, ingressExpiry, nonce)
			);
			return await this.QueryAsync(content, effectiveCanisterId, cancellationToken);
		}


		/// <summary>
		/// Sends a call request to a specified canister method and gets back an id of the
		/// request that is being processed using /v2/../call. This call does NOT wait for the request to be complete.
		/// Either check the status with `GetRequestStatusAsync` or use the `CallV2AndWaitAsync` method
		/// </summary>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="method">The name of the method to call on the canister</param>
		/// <param name="arg">The candid arg to send with the request</param>
		/// <param name="identity">Optional. Identity to sign the request with</param>
		/// <param name="nonce">Optional. If specified will make the request unique even with the same arguments</param>
		/// <param name="effectiveCanisterId">Optional. Specifies the relevant canister id if calling the root canister</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The id of the request that can be used to look up its status with `GetRequestStatusAsync`</returns>
		public async Task<RequestId> CallAsynchronousAsync(
			Principal canisterId,
			string method,
			CandidArg arg,
			IIdentity? identity = null,
			byte[]? nonce = null,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null
		)
		{
			SignedContent<CallRequest> content = this.SignContent(
				identity,
				(sender, ingressExpiry) =>
				{
					return new CallRequest(canisterId, method, arg, sender, ingressExpiry, nonce);
				}
			);
			return await this.CallAsynchronousAsync(content, effectiveCanisterId, cancellationToken);
		}


		/// <summary>
		/// Sends a call request to a specified canister method, waits for the request to be processed,
		/// the returns the candid response to the call. This is helper method built on top of `CallAsynchronousAsync`
		/// to wait for the response so it doesn't need to be implemented manually
		/// </summary>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="method">The name of the method to call on the canister</param>
		/// <param name="arg">The candid arg to send with the request</param>
		/// <param name="identity">Optional. Identity to sign the request with</param>
		/// <param name="nonce">Optional. If specified will make the request unique even with the same arguments</param>
		/// <param name="effectiveCanisterId">Optional. Specifies the relevant canister id if calling the root canister</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The raw candid arg response</returns>
		public async Task<CandidArg> CallAsynchronousAndWaitAsync(
			Principal canisterId,
			string method,
			CandidArg arg,
			IIdentity? identity = null,
			byte[]? nonce = null,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null)
		{
			RequestId id = await this.CallAsynchronousAsync(canisterId, method, arg, identity, nonce, effectiveCanisterId, cancellationToken);
			return await this.WaitForRequestAsync(canisterId, id, cancellationToken);
		}


		public async Task<CandidArg> CallAsynchronousAndWaitAsync(
			SignedContent<CallRequest> content,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null)
		{
			RequestId id = await this.CallAsynchronousAsync(content, effectiveCanisterId, cancellationToken);
			return await this.WaitForRequestAsync(content.Request.CanisterId, id, cancellationToken);
		}


		/// <summary>
		/// Gets the status of a request that is being processed by the specified canister
		/// </summary>
		/// <param name="canisterId">Canister where the request was sent to</param>
		/// <param name="id">Id of the request to get a status for</param>
		/// <param name="identity">Optional. Identity to sign the request with</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>A status variant of the request. If request is not found, will return null</returns>
		public async Task<RequestStatus?> GetRequestStatusAsync(
			Principal canisterId,
			RequestId id,
			IIdentity? identity = null,
			CancellationToken? cancellationToken = null
		)
		{
			var pathRequestStatus = StatePath.FromSegments("request_status", id.RawValue);
			var paths = new List<StatePath> { pathRequestStatus };
			SignedContent<ReadStateRequest> signedContent = this.SignContent(identity, (sender, ingressExpiry) => new ReadStateRequest(paths, sender, ingressExpiry));
			ReadStateResponse response = await this.ReadStateAsync(canisterId, signedContent, cancellationToken);
			HashTree? requestStatus = response.Certificate.Tree.GetValueOrDefault(pathRequestStatus);
			return ParseRequestStatus(requestStatus);
		}

		/// <summary>
		/// Waits for a request to be processed and returns the candid response to the call. This is a helper
		/// method built on top of `GetRequestStatusAsync` to wait for the response so it doesn't need to be
		/// implemented manually
		/// </summary>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="requestId">The unique identifier for the request</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The raw candid arg response</returns>
		public async Task<CandidArg> WaitForRequestAsync(
			Principal canisterId,
			RequestId requestId,
			CancellationToken? cancellationToken = null
		)
		{
			while (true)
			{
				cancellationToken?.ThrowIfCancellationRequested();

				RequestStatus? requestStatus = await this.GetRequestStatusAsync(canisterId, requestId);

				cancellationToken?.ThrowIfCancellationRequested();

				switch (requestStatus?.Type)
				{
					case null:
					case RequestStatus.StatusType.Received:
					case RequestStatus.StatusType.Processing:
						continue; // Still processing
					case RequestStatus.StatusType.Replied:
						return requestStatus.AsReplied();
					case RequestStatus.StatusType.Rejected:
						(RejectCode code, string message, string? errorCode) = requestStatus.AsRejected();
						throw new CallRejectedException(code, message, errorCode);
					case RequestStatus.StatusType.Done:
						throw new RequestCleanedUpException();
				}
			}
		}


		internal static RequestStatus? ParseRequestStatus(HashTree? requestStatus)
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


		private SignedContent<TRequest> SignContent<TRequest>(
			IIdentity? identity,
			Func<Principal, ICTimestamp, TRequest> getRequest
		)
			where TRequest : IRepresentationIndependentHashItem
		{

			Principal principal;
			if (identity == null)
			{
				principal = Principal.Anonymous();
			}
			else
			{
				SubjectPublicKeyInfo publicKey = identity.GetPublicKey();
				principal = publicKey.ToPrincipal();
			}
			TRequest request = getRequest(principal, ICTimestamp.Future(TimeSpan.FromMinutes(3)));

			if (identity == null)
			{
				var sha256 = SHA256HashFunction.Create();
				RequestId requestId = RequestId.FromObject(request.BuildHashableItem(), sha256);
				return new SignedContent<TRequest>(requestId, request, null, null, null);
			}
			return identity.SignContent(request);

		}
	}
}
