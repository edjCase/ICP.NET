using System.Runtime.CompilerServices;
using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Candid;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC.Client;
using EdjCase.ICP.PocketIC.Models;
using Org.BouncyCastle.Tls.Crypto.Impl;

namespace EdjCase.ICP.PocketIC
{
	public class PocketIc : IAsyncDisposable
	{
		private static readonly Principal MANAGEMENT_CANISTER_ID = Principal.FromText("aaaaa-aa");

		public IPocketIcHttpClient HttpClient { get; }
		public int InstanceId { get; }
		private readonly CandidConverter candidConverter;
		private List<SubnetTopology>? topologyCache;

		private PocketIc(
			IPocketIcHttpClient client,
			int instanceId,
			List<SubnetTopology>? topology = null,
			CandidConverter? candidConverter = null
		)
		{
			this.HttpClient = client;
			this.InstanceId = instanceId;
			this.candidConverter = candidConverter ?? CandidConverter.Default;
			this.topologyCache = topology;
		}


		public async Task<Principal> CreateAndInstallCanisterAsync(
			byte[] wasmModule,
			CandidArg arg, // TODO can we take in a generic arg type? but issue is there can be multiple args
			CanisterSettings? settings = null,
			UnboundedUInt? cyclesAmount = null,
			Principal? specifiedId = null
		)
		{
			CreateCanisterResponse createCanisterResponse = await this.CreateCanisterAsync(
				settings: settings,
				cyclesAmount: cyclesAmount,
				specifiedId: specifiedId
			);
			await this.InstallCodeAsync(
				canisterId: createCanisterResponse.CanisterId,
				wasmModule: wasmModule,
				arg: arg,
				mode: InstallCodeMode.Install
			);
			return createCanisterResponse.CanisterId;
		}

		public async Task<CreateCanisterResponse> CreateCanisterAsync(
			CanisterSettings? settings = null,
			UnboundedUInt? cyclesAmount = null,
			Principal? specifiedId = null
		)
		{
			var request = new CreateCanisterRequest
			{
				Settings = settings == null ? OptionalValue<CanisterSettings>.NoValue() : OptionalValue<CanisterSettings>.WithValue(settings),
				Amount = cyclesAmount == null ? OptionalValue<UnboundedUInt>.NoValue() : OptionalValue<UnboundedUInt>.WithValue(cyclesAmount),
				SpecifiedId = specifiedId == null ? OptionalValue<Principal>.NoValue() : OptionalValue<Principal>.WithValue(specifiedId)
			};
			return await this.UpdateCallAsync<CreateCanisterRequest, CreateCanisterResponse>(
				Principal.Anonymous(),
				MANAGEMENT_CANISTER_ID,
				"provisional_create_canister_with_cycles",
				request
			);
		}

		public async Task StartCanisterAsync(Principal canisterId)
		{
			StartCanisterRequest request = new() { CanisterId = canisterId };
			await this.UpdateCallNoResponseAsync(
				Principal.Anonymous(),
				MANAGEMENT_CANISTER_ID,
				"start_canister",
				request
			);
		}

		public async Task StopCanisterAsync(Principal canisterId)
		{
			StopCanisterRequest request = new() { CanisterId = canisterId };

			await this.UpdateCallNoResponseAsync(
				Principal.Anonymous(),
				MANAGEMENT_CANISTER_ID,
				"stop_canister",
				request
			);
		}

		public async Task InstallCodeAsync(
			Principal canisterId,
			byte[] wasmModule,
			CandidArg arg,
			InstallCodeMode mode
		)
		{
			InstallCodeRequest request = new()
			{
				CanisterId = canisterId,
				Arg = arg.Encode(),
				WasmModule = wasmModule,
				Mode = mode
			};
			await this.UpdateCallNoResponseAsync(
				Principal.Anonymous(),
				MANAGEMENT_CANISTER_ID,
				"install_code",
				request
			);
		}

		public async Task<TResponse> QueryCallAsync<TResponse>(
			Principal sender,
			Principal canisterId,
			string method,
			EffectivePrincipal? effectivePrincipal = null
		)
		{
			CandidArg arg = CandidArg.FromCandid();
			CandidArg responseArg = await this.QueryCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
			return responseArg.ToObjects<TResponse>(this.candidConverter);
		}

		public async Task<TResponse> QueryCallAsync<T1, TResponse>(
			Principal sender,
			Principal canisterId,
			string method,
			T1 p1,
			EffectivePrincipal? effectivePrincipal = null
		)
			where T1 : notnull
		{
			CandidArg arg = CandidArg.FromCandid(
				this.candidConverter.FromTypedObject(p1)
			);
			CandidArg responseArg = await this.QueryCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
			return responseArg.ToObjects<TResponse>(this.candidConverter);
		}

		public async Task<TResponse> QueryCallAsync<T1, T2, TResponse>(
			Principal sender,
			Principal canisterId,
			string method,
			T1 p1,
			T2 p2,
			EffectivePrincipal? effectivePrincipal = null
		)
			where T1 : notnull
			where T2 : notnull
		{
			CandidArg arg = CandidArg.FromCandid(
				this.candidConverter.FromTypedObject(p1),
				this.candidConverter.FromTypedObject(p2)
			);
			CandidArg responseArg = await this.QueryCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
			return responseArg.ToObjects<TResponse>(this.candidConverter);
		}

		public async Task<TResponse> QueryCallAsync<T1, T2, T3, TResponse>(
			Principal sender,
			Principal canisterId,
			string method,
			T1 p1,
			T2 p2,
			T3 p3,
			EffectivePrincipal? effectivePrincipal = null
		)
			where T1 : notnull
			where T2 : notnull
			where T3 : notnull
		{
			CandidArg arg = CandidArg.FromCandid(
				this.candidConverter.FromTypedObject(p1),
				this.candidConverter.FromTypedObject(p2),
				this.candidConverter.FromTypedObject(p3)
			);
			CandidArg responseArg = await this.QueryCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
			return responseArg.ToObjects<TResponse>(this.candidConverter);
		}

		public async Task<CandidArg> QueryCallRawAsync(
			Principal sender,
			Principal canisterId,
			string method,
			CandidArg arg,
			EffectivePrincipal? effectivePrincipal = null
		)
		{
			return await this.HttpClient.QueryCallAsync(
				this.InstanceId,
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
		}

		public async Task<TResponse> UpdateCallAsync<TResponse>(
			Principal sender,
			Principal canisterId,
			string method,
			EffectivePrincipal? effectivePrincipal = null
		)
		{
			CandidArg arg = CandidArg.FromCandid();
			CandidArg responseArg = await this.UpdateCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
			return responseArg.ToObjects<TResponse>(this.candidConverter);
		}

		public async Task<TResponse> UpdateCallAsync<T1, TResponse>(
			Principal sender,
			Principal canisterId,
			string method,
			T1 p1,
			EffectivePrincipal? effectivePrincipal = null
		)
			where T1 : notnull
		{
			CandidArg arg = CandidArg.FromCandid(
				this.candidConverter.FromTypedObject(p1)
			);
			CandidArg responseArg = await this.UpdateCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
			return responseArg.ToObjects<TResponse>(this.candidConverter);
		}

		public async Task<TResponse> UpdateCallAsync<T1, T2, TResponse>(
			Principal sender,
			Principal canisterId,
			string method,
			T1 p1,
			T2 p2,
			EffectivePrincipal? effectivePrincipal = null
		)
			where T1 : notnull
			where T2 : notnull
		{
			CandidArg arg = CandidArg.FromCandid(
				this.candidConverter.FromTypedObject(p1),
				this.candidConverter.FromTypedObject(p2)
			);
			CandidArg responseArg = await this.UpdateCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
			return responseArg.ToObjects<TResponse>(this.candidConverter);
		}

		public async Task<TResponse> UpdateCallAsync<T1, T2, T3, TResponse>(
			Principal sender,
			Principal canisterId,
			string method,
			T1 p1,
			T2 p2,
			T3 p3,
			EffectivePrincipal? effectivePrincipal = null
		)
			where T1 : notnull
			where T2 : notnull
			where T3 : notnull
		{
			CandidArg arg = CandidArg.FromCandid(
				this.candidConverter.FromTypedObject(p1),
				this.candidConverter.FromTypedObject(p2),
				this.candidConverter.FromTypedObject(p3)
			);
			CandidArg responseArg = await this.UpdateCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
			return responseArg.ToObjects<TResponse>(this.candidConverter);
		}

		public async Task UpdateCallNoResponseAsync(
			Principal sender,
			Principal canisterId,
			string method,
			EffectivePrincipal? effectivePrincipal = null
		)
		{
			CandidArg arg = CandidArg.FromCandid();
			await this.UpdateCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
		}

		public async Task UpdateCallNoResponseAsync<T1>(
			Principal sender,
			Principal canisterId,
			string method,
			T1 p1,
			EffectivePrincipal? effectivePrincipal = null
		)
			where T1 : notnull
		{
			CandidArg arg = CandidArg.FromCandid(
				this.candidConverter.FromTypedObject(p1)
			);
			await this.UpdateCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
		}

		public async Task UpdateCallNoResponseAsync<T1, T2>(
			Principal sender,
			Principal canisterId,
			string method,
			T1 p1,
			T2 p2,
			EffectivePrincipal? effectivePrincipal = null
		)
			where T1 : notnull
			where T2 : notnull
		{
			CandidArg arg = CandidArg.FromCandid(
				this.candidConverter.FromTypedObject(p1),
				this.candidConverter.FromTypedObject(p2)
			);
			await this.UpdateCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
		}

		public async Task UpdateCallNoResponseAsync<T1, T2, T3>(
			Principal sender,
			Principal canisterId,
			string method,
			T1 p1,
			T2 p2,
			T3 p3,
			EffectivePrincipal? effectivePrincipal = null
		)
			where T1 : notnull
			where T2 : notnull
			where T3 : notnull
		{
			CandidArg arg = CandidArg.FromCandid(
				this.candidConverter.FromTypedObject(p1),
				this.candidConverter.FromTypedObject(p2),
				this.candidConverter.FromTypedObject(p3)
			);
			await this.UpdateCallRawAsync(
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
		}


		public async Task<CandidArg> UpdateCallRawAsync(
			Principal sender,
			Principal canisterId,
			string method,
			CandidArg arg,
			EffectivePrincipal? effectivePrincipal = null
		)
		{
			return await this.HttpClient.ExecuteIngressMessageAsync(
				this.InstanceId,
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
		}

		public async Task<HttpGateway> RunHttpGatewayAsync()
		{
			Uri instanceUri = this.HttpClient.GetServerUrl();
			Uri uri = await this.HttpClient.StartHttpGatewayAsync(this.InstanceId, port: null, domains: null, httpsConfig: null);

			return new HttpGateway(uri, async () => await this.HttpClient.StopHttpGatewayAsync(this.InstanceId));
		}


		public async Task TickAsync(int times = 1)
		{
			for (int i = 0; i < times; i++)
			{
				await this.HttpClient.TickAsync(this.InstanceId);
			}
		}

		public Task<ICTimestamp> GetTimeAsync()
		{
			return this.HttpClient.GetTimeAsync(this.InstanceId);
		}

		public Task SetTimeAsync(ICTimestamp time)
		{
			return this.HttpClient.SetTimeAsync(this.InstanceId, time);
		}

		public async Task<IAsyncDisposable> AutoProgressTimeAsync(TimeSpan? artificalDelay = null)
		{
			await this.HttpClient.AutoProgressTimeAsync(this.InstanceId, artificalDelay);

			return new AutoProgressionDisposable(() => this.HttpClient.StopProgressTimeAsync(this.InstanceId));
		}

		public Task<Principal> GetPublicKeyForSubnetAsync(Principal subnetId)
		{
			return this.HttpClient.GetPublicKeyForSubnetAsync(this.InstanceId, subnetId);
		}

		public Task<Principal> GetSubnetIdForCanisterAsync(Principal canisterId)
		{
			return this.HttpClient.GetSubnetIdForCanisterAsync(this.InstanceId, canisterId);
		}

		public async ValueTask<List<SubnetTopology>> GetTopologyAsync(bool useCache = true)
		{
			List<SubnetTopology>? topologies = null;
			if (useCache)
			{
				topologies = this.topologyCache;
			}
			if (topologies == null)
			{
				topologies = await this.HttpClient.GetTopologyAsync(this.InstanceId);
				this.topologyCache = topologies;
			}
			return topologies;
		}

		public Task<ulong> GetCyclesBalanceAsync(Principal canisterId)
		{
			return this.HttpClient.GetCyclesBalanceAsync(this.InstanceId, canisterId);
		}

		public Task<ulong> AddCyclesAsync(Principal canisterId, ulong amount)
		{
			return this.HttpClient.AddCyclesAsync(this.InstanceId, canisterId, amount);
		}

		public Task SetStableMemoryAsync(Principal canisterId, byte[] stableMemory)
		{
			return this.HttpClient.SetStableMemoryAsync(this.InstanceId, canisterId, stableMemory);
		}

		public Task<byte[]> GetStableMemoryAsync(Principal canisterId)
		{
			return this.HttpClient.GetStableMemoryAsync(this.InstanceId, canisterId);
		}

		public async ValueTask DisposeAsync()
		{
			await this.HttpClient.DeleteInstanceAsync(this.InstanceId);
		}

		public static async Task<PocketIc> CreateAsync(
			IPocketIcHttpClient httpClient,
			List<SubnetConfig>? applicationSubnets = null,
			SubnetConfig? bitcoinSubnet = null,
			SubnetConfig? fiduciarySubnet = null,
			SubnetConfig? iiSubnet = null,
			SubnetConfig? nnsSubnet = null,
			SubnetConfig? snsSubnet = null,
			List<SubnetConfig>? systemSubnets = null,
			List<SubnetConfig>? verifiedApplicationSubnets = null,
			bool nonmainnetFeatures = false,
			CandidConverter? candidConverter = null
		)
		{
			(int instanceId, List<SubnetTopology> topology) = await httpClient.CreateInstanceAsync(
				applicationSubnets,
				bitcoinSubnet,
				fiduciarySubnet,
				iiSubnet,
				nnsSubnet,
				snsSubnet,
				systemSubnets,
				verifiedApplicationSubnets,
				nonmainnetFeatures
			);

			return new PocketIc(httpClient, instanceId, topology, candidConverter);
		}


		public static async Task<PocketIc> CreateAsync(
			string url,
			List<SubnetConfig>? applicationSubnets = null,
			SubnetConfig? bitcoinSubnet = null,
			SubnetConfig? fiduciarySubnet = null,
			SubnetConfig? iiSubnet = null,
			SubnetConfig? nnsSubnet = null,
			SubnetConfig? snsSubnet = null,
			List<SubnetConfig>? systemSubnets = null,
			List<SubnetConfig>? verifiedApplicationSubnets = null,
			bool nonmainnetFeatures = false,
			CandidConverter? candidConverter = null,
			TimeSpan? requestTimeout = null
		)
		{
			IPocketIcHttpClient httpClient = new PocketIcHttpClient(new HttpClient(), url, requestTimeout ?? TimeSpan.FromSeconds(30));
			return await PocketIc.CreateAsync(
				httpClient,
				applicationSubnets,
				bitcoinSubnet,
				fiduciarySubnet,
				iiSubnet,
				nnsSubnet,
				snsSubnet,
				systemSubnets,
				verifiedApplicationSubnets,
				nonmainnetFeatures,
				candidConverter
			);
		}

	}

	public class HttpGateway : IAsyncDisposable
	{
		public Uri Url { get; }
		private readonly Func<Task> disposeTask;

		internal HttpGateway(Uri url, Func<Task> disposeTask)
		{
			this.Url = url;
			this.disposeTask = disposeTask;
		}

		public HttpAgent BuildHttpAgent(IIdentity? identity = null)
		{
			return new HttpAgent(
				identity: identity,
				httpBoundryNodeUrl: this.Url
			);
		}

		public async ValueTask DisposeAsync()
		{
			await this.disposeTask();
		}
	}

	internal class AutoProgressionDisposable : IAsyncDisposable
	{
		private readonly Func<Task> disposeTask;

		internal AutoProgressionDisposable(Func<Task> disposeTask)
		{
			this.disposeTask = disposeTask;
		}

		public async ValueTask DisposeAsync()
		{
			await this.disposeTask();
		}
	}

}