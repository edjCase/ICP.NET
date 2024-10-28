
using System.Text.Json.Nodes;
using EdjCase.ICP.Candid.Models;

namespace EdjCase.ICP.PocketIC.Client;

public interface IPocketIcHttpClient
{
	Task<JsonNode?> GetStatusAsync();

	Task<byte[]> UploadBlobAsync(byte[] blob);

	Task<byte[]> DownloadBlobAsync(byte[] blobId);

	Task<JsonNode?> VerifySignatureAsync(
		byte[] message,
		Principal publicKey,
		Principal rootPublicKey,
		byte[] signature
	);

	Task<JsonNode?> ReadGraphAsync(string stateLabel, string opId);

	Task<List<string>> GetInstanceIdsAsync();

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

	Task<JsonNode?> GetCanisterHttpAsync(int instanceId);

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

	Task<ulong> AddCyclesAsync(int instanceId, Principal canisterId, ulong amount);

	Task SetStableMemoryAsync(int instanceId, Principal canisterId, byte[] memory);

	Task TickAsync(int instanceId);
}


public class HttpGatewayConfig
{
	public required JsonObject ForwardTo { get; set; }
	public List<string>? Domains { get; set; }
	public ushort? Port { get; set; }
	public string? IpAddr { get; set; }
	public HttpsConfig? HttpsConfig { get; set; }
}

public class HttpsConfig
{
	public required string CertPath { get; set; }
	public required string KeyPath { get; set; }
}

public class HttpGatewayDetails
{
	public required uint InstanceId { get; set; }
	public required ushort Port { get; set; }
	public required JsonObject ForwardTo { get; set; }
	public List<string>? Domains { get; set; }
	public HttpsConfig? HttpsConfig { get; set; }
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

public class CanisterHttpHeader
{
	public required string Name { get; set; }
	public required string Value { get; set; }
}
