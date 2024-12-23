using System.Net;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid.Models;

namespace EdjCase.ICP.PocketIC.Client;

/// <summary>
/// Interface for communicating with a PocketIC server and managing IC instances
/// </summary>
public interface IPocketIcHttpClient
{
	/// <summary>
	/// Gets the base URL of the PocketIC server
	/// </summary>
	Uri GetServerUrl();

	/// <summary>
	/// Uploads a binary blob to the PocketIC server
	/// </summary>
	/// <param name="blob">The binary data to upload</param>
	/// <returns>The blob id for later retrieval</returns>
	Task<string> UploadBlobAsync(byte[] blob);

	/// <summary>
	/// Downloads a previously uploaded blob from the PocketIC server
	/// </summary>
	/// <param name="blobId">The id of the blob to download</param>
	/// <returns>The binary data of the blob</returns>
	Task<byte[]> DownloadBlobAsync(string blobId);

	/// <summary>
	/// Verifies a canister signature
	/// </summary>
	/// <param name="message">The message that was signed</param>
	/// <param name="publicKey">The public key to verify against</param>
	/// <param name="rootPublicKey">The root public key</param>
	/// <param name="signature">The signature to verify</param>
	/// <returns>True if signature is valid, false otherwise</returns>
	Task<bool> VerifySignatureAsync(
		byte[] message,
		Principal publicKey,
		Principal rootPublicKey,
		byte[] signature
	);

	/// <summary>
	/// Gets all PocketIC instances
	/// </summary>
	/// <returns>List of all instances and their status</returns>
	Task<List<Instance>> GetInstancesAsync();

	/// <summary>
	/// Creates a new PocketIC instance with the specified subnet configuration
	/// </summary>
	/// <param name="applicationSubnets">Optional application subnet configurations. Will create a single application subnet if not specified</param>
	/// <param name="bitcoinSubnet">Optional Bitcoin subnet configuration. Will not create if not specified</param>
	/// <param name="fiduciarySubnet">Optional fiduciary subnet configuration. Will not create if not specified</param>
	/// <param name="iiSubnet">Optional Internet Identity subnet configuration. Will not create if not specified</param>
	/// <param name="nnsSubnet">Optional Network Nervous System subnet configuration. Will not create if not specified</param>
	/// <param name="snsSubnet">Optional Service Nervous System subnet configuration. Will not create if not specified</param>
	/// <param name="systemSubnets">Optional system subnet configurations. Will not create if not specified</param>
	/// <param name="verifiedApplicationSubnets">Optional verified application subnet configurations</param>
	/// <param name="nonmainnetFeatures">Whether to enable non-mainnet features. Defaults to false</param>
	/// <returns>A tuple containing the new instance id and topology information</returns>
	Task<(int Id, List<SubnetTopology> Topology)> CreateInstanceAsync(
		List<SubnetConfig>? applicationSubnets = null,
		SubnetConfig? bitcoinSubnet = null,
		SubnetConfig? fiduciarySubnet = null,
		SubnetConfig? iiSubnet = null,
		SubnetConfig? nnsSubnet = null,
		SubnetConfig? snsSubnet = null,
		List<SubnetConfig>? systemSubnets = null,
		List<SubnetConfig>? verifiedApplicationSubnets = null,
		bool nonmainnetFeatures = false
	);

	/// <summary>
	/// Deletes a PocketIC instance
	/// </summary>
	/// <param name="id">The id of the instance to delete</param>
	Task DeleteInstanceAsync(int id);

	/// <summary>
	/// Makes a query call to a canister
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <param name="sender">The principal making the call</param>
	/// <param name="canisterId">The target canister id</param>
	/// <param name="method">The method name to call</param>
	/// <param name="request">The raw candid request argument</param>
	/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
	/// <returns>The raw candid response from the canister</returns>
	Task<CandidArg> QueryCallAsync(
		int instanceId,
		Principal sender,
		Principal canisterId,
		string method,
		CandidArg request,
		EffectivePrincipal? effectivePrincipal = null);

	/// <summary>
	/// Gets the topology information for a PocketIC instance
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <returns>List of subnet topologies</returns>
	Task<List<SubnetTopology>> GetTopologyAsync(int instanceId);

	/// <summary>
	/// Gets the current timestamp of a PocketIC instance
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <returns>The current timestamp</returns>
	Task<ICTimestamp> GetTimeAsync(int instanceId);

	/// <summary>
	/// Gets the cycles balance of a canister
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <param name="canisterId">The canister id</param>
	/// <returns>The cycles balance of the canister</returns>
	Task<ulong> GetCyclesBalanceAsync(int instanceId, Principal canisterId);

	/// <summary>
	/// Gets the stable memory of a canister
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <param name="canisterId">The canister id</param>
	/// <returns>The stable memory bytes of the canister</returns>
	Task<byte[]> GetStableMemoryAsync(int instanceId, Principal canisterId);

	/// <summary>
	/// Gets the subnet id for a canister
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <param name="canisterId">The canister id</param>
	/// <returns>The subnet id of the canister</returns>
	Task<Principal> GetSubnetIdForCanisterAsync(int instanceId, Principal canisterId);

	/// <summary>
	/// Gets the public key for a subnet
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <param name="subnetId">The subnet id</param>
	/// <returns>The public key principal of the subnet</returns>
	Task<Principal> GetPublicKeyForSubnetAsync(int instanceId, Principal subnetId);

	/// <summary>
	/// Gets the status of an ingress message
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <param name="requestId">The id of the request to check the status for</param>
	/// <param name="effectivePrincipal">Optional effective principal for the call</param>
	/// <returns></returns>
	Task<IngressStatus> GetIngressStatusAsync(
		int instanceId,
		RequestId requestId,
		EffectivePrincipal effectivePrincipal);

	/// <summary>
	/// Executes an ingress message on a canister and waits for the response
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <param name="sender">The principal sending the message</param>
	/// <param name="canisterId">The target canister id</param>
	/// <param name="method">The method name to call</param>
	/// <param name="request">The raw candid request argument</param>
	/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
	/// <returns>The raw candid response</returns>
	Task<CandidArg> ExecuteIngressMessageAsync(
		int instanceId,
		Principal sender,
		Principal canisterId,
		string method,
		CandidArg request,
		EffectivePrincipal? effectivePrincipal = null);

	/// <summary>
	/// Submits an ingress message to a canister without waiting for execution
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <param name="sender">The principal sending the message</param>
	/// <param name="canisterId">The target canister id</param>
	/// <param name="method">The method name to call</param>
	/// <param name="request">The raw candid request argument</param>
	/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
	/// <returns>The request id for the message</returns>
	Task<RequestId> SubmitIngressMessageAsync(
		int instanceId,
		Principal sender,
		Principal canisterId,
		string method,
		CandidArg request,
		EffectivePrincipal? effectivePrincipal = null);

	/// <summary>
	/// Waits for an ingress message to complete execution
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <param name="requestId">The id of the ingress message</param>
	/// <param name="effectivePrincipal">Effective principal for the call</param>
	Task<CandidArg> AwaitIngressMessageAsync(int instanceId, RequestId requestId, EffectivePrincipal effectivePrincipal);

	/// <summary>
	/// Sets the current time of the IC instance
	/// </summary>
	/// <param name="instanceId">The IC instance</param>
	/// <param name="timestamp">The new timestamp</param>
	Task SetTimeAsync(int instanceId, ICTimestamp timestamp);

	/// <summary>
	/// Configures automatic time progression for the IC instance
	/// </summary>
	/// <param name="instanceId">The IC instance</param>
	/// <param name="artificialDelay">Optional delay between time updates</param>
	Task AutoProgressTimeAsync(int instanceId, TimeSpan? artificialDelay = null);

	/// <summary>
	/// Stops automatic time progression for the IC instance
	/// </summary>
	/// <param name="instanceId">The IC instance</param>
	Task StopProgressTimeAsync(int instanceId);

	/// <summary>
	/// Adds cycles to a canister
	/// </summary>
	/// <param name="instanceId">The id of the IC instance</param>
	/// <param name="canisterId">The canister id</param>
	/// <param name="amount">The amount of cycles to add</param>
	/// <returns>The new cycles balance of the canister</returns>
	Task<ulong> AddCyclesAsync(int instanceId, Principal canisterId, ulong amount);

	/// <summary>
	/// Sets the stable memory of a canister
	/// </summary>
	/// <param name="instanceId">The id of the IC instance</param>
	/// <param name="canisterId">The canister id</param>
	/// <param name="memory">The new stable memory bytes</param>
	Task SetStableMemoryAsync(int instanceId, Principal canisterId, byte[] memory);

	/// <summary>
	/// Makes the IC produce and progress by one block
	/// </summary>
	/// <param name="instanceId">The id of the IC instance</param>
	Task TickAsync(int instanceId);


	/// <summary>
	/// Gets pending canister HTTP Outcall requests (not http calls to a canister)
	/// </summary>
	/// <param name="instanceId">The id of the PocketIC instance</param>
	/// <returns>The pending canister HTTP request</returns>
	Task<List<CanisterHttpRequest>> GetCanisterHttpAsync(int instanceId);

	/// <summary>
	/// Mocks a response to a canister HTTP Outcall request (not an http call to a canister)
	/// </summary>
	/// <param name="instanceId">The id of the IC instance</param>
	/// <param name="requestId">The id of the HTTP request</param>
	/// <param name="subnetId">The subnet id of the canister</param>
	/// <param name="response">The response to send</param>
	/// <param name="additionalResponses">Optional Additional responses to send</param>
	Task MockCanisterHttpResponseAsync(
		int instanceId,
		ulong requestId,
		Principal subnetId,
		CanisterHttpResponse response,
		List<CanisterHttpResponse>? additionalResponses = null
	);

	/// <summary>
	/// Starts an HTTP gateway for handling requests to the IC instance
	/// </summary>
	/// <param name="instanceId">The id of the IC instance</param>
	/// <param name="port">Optional port number to listen on</param>
	/// <param name="domains">Optional list of domains to accept requests from</param>
	/// <param name="httpsConfig">Optional HTTPS configuration</param>
	/// <returns>The URL of the HTTP gateway</returns>
	Task<Uri> StartHttpGatewayAsync(int instanceId, int? port = null, List<string>? domains = null, HttpsConfig? httpsConfig = null);

	/// <summary>
	/// Stops the HTTP gateway for an IC instance
	/// </summary>
	/// <param name="instanceId">The id of the IC instance</param>
	Task StopHttpGatewayAsync(int instanceId);
}

/// <summary>
/// Information about a PocketIC instance
/// </summary>
public class Instance
{
	/// <summary>
	/// The unique identifier for this instance
	/// </summary>
	public required int Id { get; set; }

	/// <summary>
	/// The current status of this instance
	/// </summary>
	public required InstanceStatus Status { get; set; }
}

/// <summary>
/// The status of a PocketIC instance
/// </summary>
public enum InstanceStatus
{
	/// <summary>
	/// The instance is available for use
	/// </summary>
	Available,
	/// <summary>
	/// The instance has been deleted
	/// </summary>
	Deleted
}
/// <summary>
/// A variant model representing the status of an ingress message
/// </summary>
public class IngressStatus
{
	/// <summary>
	/// The type of ingress status
	/// </summary>
	public IngressStatusType Type { get; }

	private object? value;

	private IngressStatus(IngressStatusType type, object? value)
	{
		this.Type = type;
		this.value = value;
	}

	/// <summary>
	/// Gets the Ok variant data for the status
	/// </summary>
	public RequestStatus AsOk()
	{
		return (RequestStatus)this.value!;
	}

	/// <summary>
	/// Creates a new Ok status with the variant data
	/// </summary>
	/// <param name="status">The request status data</param>
	/// <returns>An Ok ingress variant model</returns>
	public static IngressStatus Ok(RequestStatus status)
	{
		return new IngressStatus(IngressStatusType.Ok, status);
	}

	/// <summary>
	/// Creates a new NotFound status
	/// </summary>
	/// <returns>A NotFound ingress variant model</returns>
	public static IngressStatus NotFound()
	{
		return new IngressStatus(IngressStatusType.NotFound, null);
	}
}

/// <summary>
/// The variant types of ingress status
/// </summary>
public enum IngressStatusType
{
	/// <summary>
	/// Not Found
	/// </summary>
	NotFound,
	/// <summary>
	/// Ok
	/// </summary>
	Ok
}

/// <summary>
/// Configuration for HTTPS support
/// </summary>
public class HttpsConfig
{
	/// <summary>
	/// Path to the TLS certificate file
	/// </summary>
	public required string CertPath { get; set; }

	/// <summary>
	/// Path to the private key file
	/// </summary>
	public required string KeyPath { get; set; }
}

/// <summary>
/// Represents the topology information for a subnet
/// </summary>
public class SubnetTopology
{
	/// <summary>
	/// The subnet's principal id
	/// </summary>
	public required Principal Id { get; set; }

	/// <summary>
	/// The type of subnet
	/// </summary>
	public required SubnetType Type { get; set; }

	/// <summary>
	/// The subnet's seed bytes
	/// </summary>
	public required byte[] SubnetSeed { get; set; }

	/// <summary>
	/// The node ids in this subnet
	/// </summary>
	public required List<byte[]> NodeIds { get; set; }

	/// <summary>
	/// The canister id ranges for this subnet
	/// </summary>
	public required List<CanisterRange> CanisterRanges { get; set; }
}

/// <summary>
/// Represents a range of canister ids
/// </summary>
public class CanisterRange
{
	/// <summary>
	/// The start of the canister id range
	/// </summary>
	public required Principal Start { get; set; }

	/// <summary>
	/// The end of the canister id range
	/// </summary>
	public required Principal End { get; set; }
}

/// <summary>
/// The type of subnet
/// </summary>
public enum SubnetType
{
	/// <summary>
	/// Application subnet
	/// </summary>
	Application,
	/// <summary>
	/// Bitcoin subnet
	/// </summary>
	Bitcoin,
	/// <summary>
	/// Fiduciary subnet
	/// </summary>
	Fiduciary,
	/// <summary>
	/// Internet Identity subnet
	/// </summary>
	InternetIdentity,
	/// <summary>
	/// Network Nervous System subnet
	/// </summary>
	NNS,
	/// <summary>
	/// Social Network System subnet
	/// </summary>
	SNS,
	/// <summary>
	/// System subnet
	/// </summary>
	System
}

/// <summary>
/// Specifies an effective principal for message routing
/// </summary>
public class EffectivePrincipal
{
	/// <summary>
	/// The type of effective principal
	/// </summary>
	public EffectivePrincipalType Type { get; }

	/// <summary>
	/// The principal id
	/// </summary>
	public Principal Id { get; }

	private EffectivePrincipal(EffectivePrincipalType type, Principal id)
	{
		this.Type = type;
		this.Id = id;
	}

	/// <summary>
	/// Creates an empty effective principal
	/// </summary>
	/// <returns>An empty effective principal</returns>
	public static EffectivePrincipal None()
	{
		return new EffectivePrincipal(EffectivePrincipalType.None, Principal.Anonymous());
	}

	/// <summary>
	/// Creates an effective principal for a subnet
	/// </summary>
	/// <param name="id">The subnet id</param>
	/// <returns>A subnet effective principal</returns>
	public static EffectivePrincipal Subnet(Principal id)
	{
		return new EffectivePrincipal(EffectivePrincipalType.Subnet, id);
	}

	/// <summary>
	/// Creates an effective principal for a canister
	/// </summary>
	/// <param name="id">The canister id</param>
	/// <returns>A canister effective principal</returns>
	public static EffectivePrincipal Canister(Principal id)
	{
		return new EffectivePrincipal(EffectivePrincipalType.Canister, id);
	}
}

/// <summary>
/// Types of effective principals
/// </summary>
public enum EffectivePrincipalType
{
	/// <summary>
	/// No effective principal
	/// </summary>
	None,
	/// <summary>
	/// Subnet
	/// </summary>
	Subnet,
	/// <summary>
	/// Canister
	/// </summary>
	Canister
}

/// <summary>
/// Configuration for a subnet
/// </summary>
public class SubnetConfig
{
	/// <summary>
	/// Whether to enable deterministic time slicing
	/// </summary>
	public bool? EnableDeterministicTimeSlicing { get; set; }

	/// <summary>
	/// Whether to enable high instruction limits for benchmarking
	/// </summary>
	public bool? EnableBenchmarkingInstructionLimits { get; set; }

	/// <summary>
	/// The subnet state configuration
	/// </summary>
	public required SubnetStateConfig State { get; set; }

	/// <summary>
	/// Helper function to create a new/blank subnet configuration
	/// </summary>
	/// <param name="enableDts">(Optional) If true, will enable DTS. Null value will use the IC default</param>
	/// <param name="enableBenchmarkInstructionLimits">(Optional) If true, will enable benchamrk instruction limits. Null value will use the IC default</param>
	/// <returns>A config for a new subnet</returns>
	public static SubnetConfig New(bool? enableDts = null, bool? enableBenchmarkInstructionLimits = null)
	{
		return new SubnetConfig
		{
			EnableDeterministicTimeSlicing = enableDts,
			EnableBenchmarkingInstructionLimits = enableBenchmarkInstructionLimits,
			State = SubnetStateConfig.New()
		};
	}

	/// <summary>
	/// Helper function to create a subnet configuration from an existing state path
	/// </summary>
	/// <param name="path">The filesystem path to the ic_state directory to load data from</param>
	/// <param name="subnetId">The id of the subnet being restored</param>
	/// <param name="enableDts">(Optional) If true, will enable DTS. Null value will use the IC default</param>
	/// <param name="enableBenchmarkInstructionLimits">(Optional) If true, will enable benchamrk instruction limits. Null value will use the IC default</param>
	/// <returns></returns>
	public static SubnetConfig FromPath(string path, Principal subnetId, bool? enableDts = null, bool? enableBenchmarkInstructionLimits = null)
	{
		return new SubnetConfig
		{
			EnableDeterministicTimeSlicing = enableDts,
			EnableBenchmarkingInstructionLimits = enableBenchmarkInstructionLimits,
			State = SubnetStateConfig.FromPath(path, subnetId)
		};
	}
}

/// <summary>
/// Configuration for subnet state initialization
/// </summary>
public class SubnetStateConfig
{
	/// <summary>
	/// The type of state configuration
	/// </summary>
	public SubnetStateType Type { get; private set; }

	/// <summary>
	/// Path to existing state, if loading from path
	/// </summary>
	public string? Path { get; private set; }

	/// <summary>
	/// Subnet id if loading existing state
	/// </summary>
	public Principal? SubnetId { get; private set; }

	private SubnetStateConfig(SubnetStateType type, string? path, Principal? subnetId)
	{
		this.Type = type;
		this.Path = path;
		this.SubnetId = subnetId;
	}

	/// <summary>
	/// Creates configuration for a new subnet with empty state
	/// </summary>
	public static SubnetStateConfig New()
	{
		return new SubnetStateConfig(SubnetStateType.New, null, null);
	}

	/// <summary>
	/// Creates configuration for loading existing state from a path
	/// </summary>
	public static SubnetStateConfig FromPath(string path, Principal subnetId)
	{
		return new SubnetStateConfig(SubnetStateType.FromPath, path, subnetId);
	}
}

/// <summary>
/// The type of subnet state configuration
/// </summary>
public enum SubnetStateType
{
	/// <summary>
	/// Create a new subnet with empty state
	/// </summary>
	New,
	/// <summary>
	/// Load existing state from a path
	/// </summary>
	FromPath
}

/// <summary>
/// HTTP request from a canister
/// </summary>
public class CanisterHttpRequest
{
	/// <summary>
	/// The subnet id where the request originated
	/// </summary>
	public required Principal SubnetId { get; set; }

	/// <summary>
	/// Unique identifier for this request
	/// </summary>
	public required ulong RequestId { get; set; }

	/// <summary>
	/// The HTTP method for the request
	/// </summary>
	public required CanisterHttpMethod HttpMethod { get; set; }

	/// <summary>
	/// The target URL for the request
	/// </summary>
	public required string Url { get; set; }

	/// <summary>
	/// The HTTP headers for the request
	/// </summary>
	public required List<(string Key, string Value)> Headers { get; set; }

	/// <summary>
	/// The body of the request
	/// </summary>
	public required byte[] Body { get; set; }

	/// <summary>
	/// Optional maximum size for the response
	/// </summary>
	public required ulong? MaxResponseBytes { get; set; }
}

/// <summary>
/// HTTP methods supported for canister HTTP calls
/// </summary>
public enum CanisterHttpMethod
{
	/// <summary>
	/// HTTP GET method
	/// </summary>
	Get,
	/// <summary>
	/// HTTP POST method
	/// </summary>
	Post,
	/// <summary>
	/// HTTP HEAD method
	/// </summary>
	Head
}

/// <summary>
/// Base class for HTTP responses to canister HTTP requests
/// </summary>
public abstract class CanisterHttpResponse { }

/// <summary>
/// Successful HTTP response to a canister HTTP request
/// </summary>
public class CanisterHttpReply : CanisterHttpResponse
{
	/// <summary>
	/// The HTTP status code
	/// </summary>
	public required HttpStatusCode Status { get; set; }

	/// <summary>
	/// The response headers
	/// </summary>
	public required List<(string Name, string Value)> Headers { get; set; }

	/// <summary>
	/// The response body
	/// </summary>
	public required byte[] Body { get; set; }
}

/// <summary>
/// Error response to a canister HTTP request
/// </summary>
public class CanisterHttpReject : CanisterHttpResponse
{
	/// <summary>
	/// The reject code indicating the type of error
	/// </summary>
	public required ulong RejectCode { get; set; }

	/// <summary>
	/// A message describing the error
	/// </summary>
	public required string Message { get; set; }
}