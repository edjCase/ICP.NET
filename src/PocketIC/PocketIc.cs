using System.Runtime.CompilerServices;
using EdjCase.ICP.Candid;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC.Client;
using EdjCase.ICP.PocketIC.Models;

namespace EdjCase.ICP.PocketIC
{
	public class PocketIc : IAsyncDisposable
	{
		private static readonly Principal MANAGEMENT_CANISTER_ID = Principal.FromText("aaaaa-aa");

		private readonly IPocketIcHttpClient client;
		private readonly int instanceId;
		private readonly CandidConverter candidConverter;
		private List<SubnetTopology>? topologyCache;

		private PocketIc(
			IPocketIcHttpClient client,
			int instanceId,
			List<SubnetTopology>? topology = null,
			CandidConverter? candidConverter = null
		)
		{
			this.client = client;
			this.instanceId = instanceId;
			this.candidConverter = candidConverter ?? CandidConverter.Default;
			this.topologyCache = topology;
		}

		public async Task<Principal> CreateAndInstallCanisterAsync(
			byte[] wasmModule,
			CandidArg arg, // TODO can we take in a generic arg type? but issue is there can be multiple args
			CreateCanisterRequest? request = null
		)
		{
			CreateCanisterResponse createCanisterResponse = await this.CreateCanisterAsync(request);
			byte[] argBytes = arg.Encode();
			await this.InstallCodeAsync(new InstallCodeRequest
			{
				CanisterId = createCanisterResponse.CanisterId,
				WasmModule = wasmModule,
				Arg = argBytes,
				Mode = InstallCodeMode.Install
			});
			return createCanisterResponse.CanisterId;
		}

		public async Task<CreateCanisterResponse> CreateCanisterAsync(CreateCanisterRequest? request = null)
		{
			request ??= new CreateCanisterRequest();
			return await this.UpdateCallAsync<CreateCanisterRequest, CreateCanisterResponse>(
				Principal.Anonymous(),
				MANAGEMENT_CANISTER_ID,
				"provisional_create_canister_with_cycles",
				request
			);
		}

		public async Task StartCanisterAsync(StartCanisterRequest request)
		{
			await this.UpdateCallNoResponseAsync(
				Principal.Anonymous(),
				MANAGEMENT_CANISTER_ID,
				"start_canister",
				request
			);
		}

		public async Task StopCanisterAsync(StopCanisterRequest request)
		{
			await this.UpdateCallNoResponseAsync(
				Principal.Anonymous(),
				MANAGEMENT_CANISTER_ID,
				"stop_canister",
				request
			);
		}

		public async Task InstallCodeAsync(InstallCodeRequest request)
		{
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
			return await this.client.QueryCallAsync(
				this.instanceId,
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
			return await this.client.ExecuteIngressMessageAsync(
				this.instanceId,
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
		}


		public async Task TickAsync(int times = 1)
		{
			for (int i = 0; i < times; i++)
			{
				await this.client.TickAsync(this.instanceId);
			}
		}

		public Task<ICTimestamp> GetTimeAsync()
		{
			return this.client.GetTimeAsync(this.instanceId);
		}

		public async Task ResetTimeAsync()
		{
			await this.SetTimeAsync(ICTimestamp.Now());
		}

		public Task SetTimeAsync(ICTimestamp time)
		{
			return this.client.SetTimeAsync(this.instanceId, time);
		}

		public async Task AdvanceTimeAsync(TimeSpan duration)
		{
			var currentTime = await this.GetTimeAsync();
			var newTime = ICTimestamp.FromNanoSeconds(currentTime.NanoSeconds + ((ulong)duration.TotalMilliseconds * 1_000_000));
			await this.SetTimeAsync(newTime);
		}

		public Task<Principal> GetPublicKeyForSubnetAsync(Principal subnetId)
		{
			return this.client.GetPublicKeyForSubnetAsync(this.instanceId, subnetId);
		}

		public Task<Principal> GetSubnetIdForCanisterAsync(Principal canisterId)
		{
			return this.client.GetSubnetIdForCanisterAsync(this.instanceId, canisterId);
		}

		public async ValueTask<Topology> GetTopologyAsync(bool useCache = true)
		{
			List<SubnetTopology>? topologies = null;
			if (useCache)
			{
				topologies = this.topologyCache;
			}
			if (topologies == null)
			{
				topologies = await this.client.GetTopologyAsync(this.instanceId);
				this.topologyCache = topologies;
			}
			return new Topology(topologies);
		}

		public Task<ulong> GetCyclesBalanceAsync(Principal canisterId)
		{
			return this.client.GetCyclesBalanceAsync(this.instanceId, canisterId);
		}

		public Task<ulong> AddCyclesAsync(Principal canisterId, ulong amount)
		{
			return this.client.AddCyclesAsync(this.instanceId, canisterId, amount);
		}

		public Task SetStableMemoryAsync(Principal canisterId, byte[] stableMemory)
		{
			return this.client.SetStableMemoryAsync(this.instanceId, canisterId, stableMemory);
		}

		public Task<byte[]> GetStableMemoryAsync(Principal canisterId)
		{
			return this.client.GetStableMemoryAsync(this.instanceId, canisterId);
		}

		public async ValueTask DisposeAsync()
		{
			await this.client.DeleteInstanceAsync(this.instanceId);
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
			HttpClient? httpClient = null
		)
		{
			httpClient ??= new HttpClient();
			IPocketIcHttpClient client = new PocketIcHttpClient(httpClient, url);
			(int instanceId, List<SubnetTopology> topology) = await client.CreateInstanceAsync(
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

			return new PocketIc(client, instanceId, topology, candidConverter);
		}

	}


	public class Topology
	{
		public Topology(List<SubnetTopology> subnets)
		{
			this.All = subnets;
		}

		public List<SubnetTopology> All { get; }


		public SubnetTopology? GetBitcoinSubnet()
		{
			return this.All.FirstOrDefault(s => s.Type == SubnetType.Bitcoin);
		}

		public SubnetTopology? GetFiduciarySubnet()
		{
			return this.All.FirstOrDefault(s => s.Type == SubnetType.Fiduciary);
		}

		public SubnetTopology? GetInternetIdentitySubnet()
		{
			return this.All.FirstOrDefault(s => s.Type == SubnetType.InternetIdentity);
		}

		public SubnetTopology? GetNnsSubnet()
		{
			return this.All.FirstOrDefault(s => s.Type == SubnetType.NNS);
		}

		public SubnetTopology? GetSnsSubnet()
		{
			return this.All.FirstOrDefault(s => s.Type == SubnetType.SNS);
		}

		public List<SubnetTopology> GetApplicationSubnets()
		{
			return this.All.Where(s => s.Type == SubnetType.Application).ToList();
		}

		public List<SubnetTopology> GetSystemSubnets()
		{
			return this.All.Where(s => s.Type == SubnetType.System).ToList();
		}
	}

}