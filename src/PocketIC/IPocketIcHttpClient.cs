
using EdjCase.ICP.Candid.Models;

namespace EdjCase.ICP.PocketIC.Client;

public interface IPocketIcHttpClient
{
	Uri GetServerUrl();

	Task<string> UploadBlobAsync(byte[] blob);

	Task<byte[]> DownloadBlobAsync(string blobId);

	Task<bool> VerifySignatureAsync(
		byte[] message,
		Principal publicKey,
		Principal rootPublicKey,
		byte[] signature
	);

	Task<List<Instance>> GetInstancesAsync();

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

	Task DeleteInstanceAsync(int id);

	Task<CandidArg> QueryCallAsync(
		int instanceId,
		Principal sender,
		Principal canisterId,
		string method,
		CandidArg request,
		EffectivePrincipal? effectivePrincipal = null);

	Task<List<SubnetTopology>> GetTopologyAsync(int instanceId);

	Task<ICTimestamp> GetTimeAsync(int instanceId);

	Task<CanisterHttpRequest> GetCanisterHttpAsync(int instanceId);

	Task<ulong> GetCyclesBalanceAsync(int instanceId, Principal canisterId);

	Task<byte[]> GetStableMemoryAsync(int instanceId, Principal canisterId);

	Task<Principal> GetSubnetIdForCanisterAsync(int instanceId, Principal canisterId);

	Task<Principal> GetPublicKeyForSubnetAsync(int instanceId, Principal subnetId);

	Task<CandidArg> SubmitIngressMessageAsync(
		int instanceId,
		Principal sender,
		Principal canisterId,
		string method,
		CandidArg request,
		EffectivePrincipal? effectivePrincipal = null);

	Task<CandidArg> ExecuteIngressMessageAsync(
		int instanceId,
		Principal sender,
		Principal canisterId,
		string method,
		CandidArg request,
		EffectivePrincipal? effectivePrincipal = null);

	Task AwaitIngressMessageAsync(int instanceId, byte[] messageId, Principal? effectivePrincipal = null);

	Task SetTimeAsync(int instanceId, ICTimestamp timestamp);


	Task AutoProgressTimeAsync(int instanceId, TimeSpan? artificialDelay = null);

	Task StopProgressTimeAsync(int instanceId);

	Task<ulong> AddCyclesAsync(int instanceId, Principal canisterId, ulong amount);

	Task SetStableMemoryAsync(int instanceId, Principal canisterId, byte[] memory);

	Task TickAsync(int instanceId);

	Task MockCanisterHttpResponseAsync(
		int instanceId,
		ulong requestId,
		Principal subnetId,
		CanisterHttpResponse response,
		List<CanisterHttpResponse> additionalResponses
	);

	Task<Uri> StartHttpGatewayAsync(int instanceId, int? port = null, List<string>? domains = null, HttpsConfig? httpsConfig = null);

	Task StopHttpGatewayAsync(int instanceId);
}


public class Instance
{
	public required int Id { get; set; }
	public required InstanceStatus Status { get; set; }
}

public enum InstanceStatus
{
	Available,
	Deleted
}

public class HttpsConfig
{
	public required string CertPath { get; set; }
	public required string KeyPath { get; set; }
}

public class SubnetTopology
{
	public required Principal Id { get; set; }
	public required SubnetType Type { get; set; }
	public required byte[] SubnetSeed { get; set; }
	public required List<byte[]> NodeIds { get; set; }
	public required List<CanisterRange> CanisterRanges { get; set; }
}

public class CanisterRange
{
	public required Principal Start { get; set; }
	public required Principal End { get; set; }
}

public enum SubnetType
{
	Application,
	Bitcoin,
	Fiduciary,
	InternetIdentity,
	NNS,
	SNS,
	System
}

public class EffectivePrincipal
{
	public required EffectivePrincipalType Type { get; set; }
	public required Principal Id { get; set; }
}

public enum EffectivePrincipalType
{
	Subnet,
	Canister
}


public class SubnetConfig
{
	public bool? EnableDeterministicTimeSlicing { get; set; }
	public bool? EnableBenchmarkingInstructionLimits { get; set; }
	public required SubnetStateConfig State { get; set; }
}

public class SubnetStateConfig
{
	public SubnetStateType Type { get; private set; }
	public string? Path { get; private set; }
	public Principal? SubnetId { get; private set; }

	private SubnetStateConfig(SubnetStateType type, string? path, Principal? subnetId)
	{
		this.Type = type;
		this.Path = path;
		this.SubnetId = subnetId;
	}

	public static SubnetStateConfig New()
	{
		return new SubnetStateConfig(SubnetStateType.New, null, null);
	}

	public static SubnetStateConfig FromPath(string path, Principal subnetId)
	{
		return new SubnetStateConfig(SubnetStateType.FromPath, path, subnetId);
	}
}

public enum SubnetStateType
{
	New,
	FromPath
}

public class CanisterHttpRequest
{
	public required Principal SubnetId { get; set; }
	public required ulong RequestId { get; set; }
	public required CanisterHttpMethod HttpMethod { get; set; }
	public required string Url { get; set; }
	public required List<CanisterHttpHeader> Headers { get; set; }
	public required byte[] Body { get; set; }
	public required ulong? MaxResponseBytes { get; set; }
}

public class CanisterHttpHeader
{
	public required string Name { get; set; }
	public required string Value { get; set; }
}

public enum CanisterHttpMethod
{
	Get,
	Post,
	Head
}
public class CanisterHttpResponse { }

public class CanisterHttpReply : CanisterHttpResponse
{
	public required ushort Status { get; set; }
	public required List<CanisterHttpHeader> Headers { get; set; }
	public required byte[] Body { get; set; }
}

public class CanisterHttpReject : CanisterHttpResponse
{
	public required ulong RejectCode { get; set; }
	public required string Message { get; set; }
}