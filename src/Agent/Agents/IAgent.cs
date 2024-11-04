using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid.Models;
using System.Collections.Generic;
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
		/// <summary>
		/// The identity to use for requests. If null, then it will use the anonymous identity
		/// </summary>
		public IIdentity? Identity { get; set; }
		/// <summary>
		/// Gets the state of a specified canister with the subset of state information
		/// specified by the paths parameter
		/// </summary>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="paths">The state paths to get information for. Other state data will be pruned if not specified</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>A response that contains the certificate of the current canister state</returns>
		Task<ReadStateResponse> ReadStateAsync(Principal canisterId, List<StatePath> paths, CancellationToken? cancellationToken = null);

		/// <summary>
		/// Gets the status of a request that is being processed by the specified canister
		/// </summary>
		/// <param name="canisterId">Canister where the request was sent to</param>
		/// <param name="id">Id of the request to get a status for</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>A status variant of the request. If request is not found, will return null</returns>
		Task<RequestStatus?> GetRequestStatusAsync(Principal canisterId, RequestId id, CancellationToken? cancellationToken = null);

		/// <summary>
		/// Sends a call request to a specified canister method and gets back an id of the
		/// request that is being processed. This call does NOT wait for the request to be complete.
		/// Either check the status with `GetRequestStatusAsync` or use the `CallAndWaitAsync` method
		/// </summary>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="method">The name of the method to call on the canister</param>
		/// <param name="arg">The candid arg to send with the request</param>
		/// <param name="effectiveCanisterId">Optional. Specifies the relevant canister id if calling the root canister</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The id of the request that can be used to look up its status with `GetRequestStatusAsync`</returns>
		Task<RequestId> CallAsync(Principal canisterId, string method, CandidArg arg, Principal? effectiveCanisterId = null, CancellationToken? cancellationToken = null);

		/// <summary>
		/// Gets the status of the IC replica. This includes versioning information
		/// about the replica
		/// </summary>
		/// <returns>A response containing all replica status information</returns>
		Task<StatusResponse> GetReplicaStatusAsync(CancellationToken? cancellationToken = null);

		/// <summary>
		/// Sends a query request to a specified canister method
		/// </summary>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="method">The name of the method to call on the canister</param>
		/// <param name="arg">The candid arg to send with the request</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The response data of the query call</returns>
		Task<QueryResponse> QueryAsync(Principal canisterId, string method, CandidArg arg, CancellationToken? cancellationToken = null);

		/// <summary>
		/// Gets the root public key of the current Internet Computer network
		/// </summary>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The root public key bytes </returns>
		Task<SubjectPublicKeyInfo> GetRootKeyAsync(CancellationToken? cancellationToken = null);
	}

	/// <summary>
	/// Extension methods for the `IAgent` interface
	/// </summary>
	public static class IAgentExtensions
	{
		/// <summary>
		/// Sends a call request to a specified canister method, waits for the request to be processed,
		/// the returns the candid response to the call. This is helper method built on top of `CallAsync`
		/// to wait for the response so it doesn't need to be implemented manually
		/// </summary>
		/// <param name="agent">The agent to use for the call</param>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="method">The name of the method to call on the canister</param>
		/// <param name="arg">The candid arg to send with the request</param>
		/// <param name="effectiveCanisterId">Optional. Specifies the relevant canister id if calling the root canister</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The raw candid arg response</returns>
		public static async Task<CandidArg> CallAndWaitAsync(
			this IAgent agent,
			Principal canisterId,
			string method,
			CandidArg arg,
			Principal? effectiveCanisterId = null,
			CancellationToken? cancellationToken = null)
		{
			RequestId id = await agent.CallAsync(canisterId, method, arg, effectiveCanisterId);
			return await agent.WaitForRequestAsync(canisterId, id, cancellationToken);
		}

		/// <summary>
		/// Waits for a request to be processed and returns the candid response to the call. This is a helper
		/// method built on top of `GetRequestStatusAsync` to wait for the response so it doesn't need to be
		/// implemented manually
		/// </summary>
		/// <param name="agent">The agent to use for the call</param>
		/// <param name="canisterId">Canister to read state for</param>
		/// <param name="requestId">The unique identifier for the request</param>
		/// <param name="cancellationToken">Optional. Token to cancel request</param>
		/// <returns>The raw candid arg response</returns>
		public static async Task<CandidArg> WaitForRequestAsync(
			this IAgent agent,
			Principal canisterId,
			RequestId requestId,
			CancellationToken? cancellationToken = null
		)
		{
			while (true)
			{
				cancellationToken?.ThrowIfCancellationRequested();

				RequestStatus? requestStatus = await agent.GetRequestStatusAsync(canisterId, requestId);

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
	}
}
