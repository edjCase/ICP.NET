using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Candid;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC.Client;
using EdjCase.ICP.PocketIC.Models;
using Org.BouncyCastle.Asn1.Ocsp;

namespace EdjCase.ICP.PocketIC
{
	/// <summary>
	/// The main interface for interacting with a PocketIC instance.
	/// PocketIC is a local canister smart contract testing platform for the Internet Computer.
	/// </summary>
	public class PocketIc : IAsyncDisposable
	{
		private static readonly Principal MANAGEMENT_CANISTER_ID = Principal.FromText("aaaaa-aa");

		/// <summary>
		/// The REST HTTP client for making requests to the PocketIC server
		/// </summary>
		public IPocketIcHttpClient HttpClient { get; }
		/// <summary>
		/// The unique identifier for this PocketIC instance
		/// </summary>
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

		/// <summary>
		/// Creates and installs a new canister with the provided WASM module and initialization arguments.
		/// </summary>
		/// <param name="wasmModule">The WASM module bytes to install</param>
		/// <param name="arg">The initialization arguments in candid format</param>
		/// <param name="settings">Optional canister settings</param>
		/// <param name="cyclesAmount">Optional amount of cycles to add to the canister</param>
		/// <param name="specifiedId">Optional specific canister ID to use</param>
		/// <returns>The Principal ID of the created canister</returns>
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

		/// <summary>
		/// Creates a new canister with optional settings
		/// </summary>
		/// <param name="settings">Optional canister settings</param>
		/// <param name="cyclesAmount">Optional amount of cycles to add</param>
		/// <param name="specifiedId">Optional specific canister ID to use</param>
		/// <returns>The response containing the created canister's info</returns>
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
				request,
				EffectivePrincipal.None()
			);
		}

		/// <summary>
		/// Starts an idle canister
		/// </summary>
		/// <param name="canisterId">The ID of the canister to start</param>
		public async Task StartCanisterAsync(Principal canisterId)
		{
			StartCanisterRequest request = new() { CanisterId = canisterId };
			await this.UpdateCallNoResponseAsync(
				Principal.Anonymous(),
				MANAGEMENT_CANISTER_ID,
				"start_canister",
				request,
				EffectivePrincipal.None()
			);
		}

		/// <summary>
		/// Stops a running canister
		/// </summary>
		/// <param name="canisterId">The ID of the canister to stop</param>
		public async Task StopCanisterAsync(Principal canisterId)
		{
			StopCanisterRequest request = new() { CanisterId = canisterId };

			await this.UpdateCallNoResponseAsync(
				Principal.Anonymous(),
				MANAGEMENT_CANISTER_ID,
				"stop_canister",
				request,
				EffectivePrincipal.None()
			);
		}

		/// <summary>
		/// Installs WASM code on a canister
		/// </summary>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="wasmModule">The WASM module bytes to install</param>
		/// <param name="arg">The installation arguments in candid format</param>
		/// <param name="mode">The installation mode (install, upgrade, reinstall)</param>
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
				request,
				EffectivePrincipal.None()
			);
		}

		/// <summary>
		/// Creates and starts an HTTP gateway for this PocketIC instance for handling HTTP requests.
		/// The gateway will expose an API endpoint for making HTTP requests to the IC instance.
		/// When disposed, the gateway will be stopped.
		/// NOTE: The gateway requires an NNS subnet to be running in the IC instance.
		/// </summary>
		/// <param name="port">Optional port number to listen on. If not specified, will choose an available port</param>
		/// <param name="domains">Optional list of domains the gateway should accept requests from. Defaults to localhost if not specified</param>
		/// <param name="httpsConfig">Optional HTTPS configuration if TLS support is needed</param>
		/// <returns>A disposable HttpGateway object that represents the running gateway and provides access to the gateway URL</returns>
		public async Task<HttpGateway> RunHttpGatewayAsync(int? port = null, List<string>? domains = null, HttpsConfig? httpsConfig = null)
		{
			Uri instanceUri = this.HttpClient.GetServerUrl();
			Uri uri = await this.HttpClient.StartHttpGatewayAsync(this.InstanceId, port: port, domains: domains, httpsConfig: httpsConfig);

			return new HttpGateway(uri, async () => await this.HttpClient.StopHttpGatewayAsync(this.InstanceId));
		}

		/// <summary>
		/// Makes the IC produce and progress by one or more blocks
		/// </summary>
		/// <param name="times">Number of ticks to execute</param>
		public async Task TickAsync(int times = 1)
		{
			for (int i = 0; i < times; i++)
			{
				await this.HttpClient.TickAsync(this.InstanceId);
			}
		}

		/// <summary>
		/// Gets the current time of the IC
		/// </summary>
		/// <returns>The current IC timestamp</returns>
		public Task<ICTimestamp> GetTimeAsync()
		{
			return this.HttpClient.GetTimeAsync(this.InstanceId);
		}

		/// <summary>
		/// Sets the current time of the IC
		/// </summary>
		/// <param name="time">The timestamp to set</param>
		public Task SetTimeAsync(ICTimestamp time)
		{
			return this.HttpClient.SetTimeAsync(this.InstanceId, time);
		}

		/// <summary>
		/// Enables automatic time/tick progression for the IC instance until disposed
		/// </summary>
		/// <param name="artificalDelay">Optional delay between time updates</param>
		/// <returns>A disposable object that will stop auto progression when disposed</returns>
		public async Task<IAsyncDisposable> AutoProgressAsync(TimeSpan? artificalDelay = null)
		{
			await this.HttpClient.AutoProgressTimeAsync(this.InstanceId, artificalDelay);

			return new AutoProgressionDisposable(() => this.HttpClient.StopProgressTimeAsync(this.InstanceId));
		}

		/// <summary>
		/// Gets the public key for the given subnet
		/// </summary>
		/// <param name="subnetId">The subnet id to look up</param>
		/// <returns>The subnet public key principal</returns>
		public Task<Principal> GetPublicKeyForSubnetAsync(Principal subnetId)
		{
			return this.HttpClient.GetPublicKeyForSubnetAsync(this.InstanceId, subnetId);
		}

		/// <summary>
		/// Gets the subnet Id for a given canister
		/// </summary>
		/// <param name="canisterId">The canister Id to look up</param>
		/// <returns>The subnet principal where the canister is hosted</returns>
		public Task<Principal> GetSubnetIdForCanisterAsync(Principal canisterId)
		{
			return this.HttpClient.GetSubnetIdForCanisterAsync(this.InstanceId, canisterId);
		}

		/// <summary>
		/// Gets the topology information for all subnets in this IC instance
		/// </summary>
		/// <param name="useCache">Whether to use cached topology info. Defaults to true</param>
		/// <returns>List of subnet topology information</returns>
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

		/// <summary>
		/// Gets the ingress status of the specified request
		/// </summary>
		/// <param name="requestId">The request ID to check</param>
		/// <param name="effectivePrincipal">The effective principal for the request</param>
		/// <returns>The ingress status of the request</returns>
		public async Task<IngressStatus> GetIngressStatusAsync(RequestId requestId, EffectivePrincipal effectivePrincipal)
		{
			return await this.HttpClient.GetIngressStatusAsync(this.InstanceId, requestId, effectivePrincipal);
		}

		/// <summary>
		/// Gets the cycles balance of a canister
		/// </summary>
		/// <param name="canisterId">The canister to check</param>
		/// <returns>The cycles balance</returns>
		public Task<ulong> GetCyclesBalanceAsync(Principal canisterId)
		{
			return this.HttpClient.GetCyclesBalanceAsync(this.InstanceId, canisterId);
		}

		/// <summary>
		/// Adds cycles to a canister
		/// </summary>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="amount">The amount of cycles to add</param>
		/// <returns>The new cycles balance</returns>
		public Task<ulong> AddCyclesAsync(Principal canisterId, ulong amount)
		{
			return this.HttpClient.AddCyclesAsync(this.InstanceId, canisterId, amount);
		}

		/// <summary>
		/// Sets the stable memory contents of a canister
		/// </summary>
		/// <param name="canisterId">The target canister</param>
		/// <param name="stableMemory">The stable memory bytes to set</param>
		public Task SetStableMemoryAsync(Principal canisterId, byte[] stableMemory)
		{
			return this.HttpClient.SetStableMemoryAsync(this.InstanceId, canisterId, stableMemory);
		}

		/// <summary>
		/// Gets the stable memory contents of a canister
		/// </summary>
		/// <param name="canisterId">The canister to read from</param>
		/// <returns>The stable memory bytes</returns>
		public Task<byte[]> GetStableMemoryAsync(Principal canisterId)
		{
			return this.HttpClient.GetStableMemoryAsync(this.InstanceId, canisterId);
		}


		/// <summary>
		/// Executes a query call on a canister with no arguments
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>The query response decoded as type TResponse</returns>
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

		/// <summary>
		/// Executes a query call on a canister with a single argument
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="p1">The first candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>The query response decoded as type TResponse</returns>
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

		/// <summary>
		/// Executes a query call on a canister with two arguments
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="p1">The first candid argument for the call</param>
		/// <param name="p2">The second candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>The query response decoded as type TResponse</returns>
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

		/// <summary>
		/// Executes a query call on a canister with three arguments
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="p1">The first candid argument for the call</param>
		/// <param name="p2">The second candid argument for the call</param>
		/// <param name="p3">The third candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>The query response decoded as type TResponse</returns>
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

		/// <summary>
		/// Executes a query call on a canister with a raw CandidArg
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="arg">The raw candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>The query response decoded as type TResponse</returns>
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

		/// <summary>
		/// Executes an update call on a canister with no arguments
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>The update response decoded as type TResponse</returns>
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

		/// <summary>
		/// Executes an update call on a canister with a single argument
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="p1">The first candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>The update response decoded as type TResponse</returns>
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

		/// <summary>
		/// Executes an update call on a canister with two arguments
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="p1">The first candid argument for the call</param>
		/// <param name="p2">The second candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>The update response decoded as type TResponse</returns>
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

		/// <summary>
		/// Executes an update call on a canister with three arguments
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="p1">The first candid argument for the call</param>
		/// <param name="p2">The second candid argument for the call</param>
		/// <param name="p3">The third candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>The update response decoded as type TResponse</returns>
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

		/// <summary>
		/// Executes an update call on a canister with no arguments and no response
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns></returns>
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

		/// <summary>
		/// Executes an update call on a canister with a single argument and no response
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="p1">The first candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns></returns>
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

		/// <summary>
		/// Executes an update call on a canister with a two arguments and no response
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="p1">The first candid argument for the call</param>
		/// <param name="p2">The second candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns></returns>
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

		/// <summary>
		/// Executes an update call on a canister with a three arguments and no response
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="p1">The first candid argument for the call</param>
		/// <param name="p2">The second candid argument for the call</param>
		/// <param name="p3">The third candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns></returns>
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


		/// <summary>
		/// Executes an update call on a canister with a raw CandidArg and raw CandidArg response
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="arg">The raw candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>A raw candid argument from the response</returns>
		public async Task<CandidArg> UpdateCallRawAsync(
			Principal sender,
			Principal canisterId,
			string method,
			CandidArg? arg = null,
			EffectivePrincipal? effectivePrincipal = null
		)
		{
			return await this.HttpClient.ExecuteIngressMessageAsync(
				this.InstanceId,
				sender,
				canisterId,
				method,
				arg ?? CandidArg.Empty(),
				effectivePrincipal
			);
		}

		/// <summary>
		/// Submits an update call on a canister with a raw CandidArg and gets a request id 
		/// in the response that can be awaited with <see cref="AwaitUpdateCallAsync(RequestId, Principal)"/> method
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="arg">The raw candid argument for the call</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>A raw candid argument from the response</returns>
		public async Task<RequestId> UpdateCallRawAsynchronousAsync(
			Principal sender,
			Principal canisterId,
			string method,
			CandidArg? arg = null,
			EffectivePrincipal? effectivePrincipal = null
		)
		{
			return await this.HttpClient.SubmitIngressMessageAsync(
				this.InstanceId,
				sender,
				canisterId,
				method,
				arg ?? CandidArg.Empty(),
				effectivePrincipal
			);
		}


		/// <summary>
		/// Executes an update call on a canister with a raw CandidArg and raw CandidArg response with a single HTTP outcall mock response.
		/// If there are multiple outcalls, only one will be mocked, and if there are none, an exception will be thrown.
		/// NOTE: If you want more advanced outcall mocking, use the <see cref="IPocketIcHttpClient"/> directly
		/// </summary>
		/// <param name="sender">The principal making the call</param>
		/// <param name="canisterId">The target canister ID</param>
		/// <param name="method">The method name to call</param>
		/// <param name="arg">The raw candid argument for the call</param>
		/// <param name="response">The HTTP outcall mock response</param>
		/// <param name="additionalResponses">Optional additional HTTP outcall mock responses</param>
		/// <param name="effectivePrincipal">Optional effective principal for the call, defaults to canister id</param>
		/// <returns>A raw candid argument from the response</returns>
		public async Task<CandidArg> UpdateCallRawWithHttpOutcallMockAsync(
			Principal sender,
			Principal canisterId,
			string method,
			CandidArg arg,
			CanisterHttpResponse response,
			List<CanisterHttpResponse>? additionalResponses = null,
			EffectivePrincipal? effectivePrincipal = null
		)
		{
			effectivePrincipal ??= EffectivePrincipal.Canister(canisterId);
			RequestId requestId = await this.HttpClient.SubmitIngressMessageAsync(
				this.InstanceId,
				sender,
				canisterId,
				method,
				arg,
				effectivePrincipal
			);
			await this.TickAsync(2);
			List<CanisterHttpRequest> outcalls = await this.HttpClient.GetCanisterHttpAsync(this.InstanceId);
			if (outcalls.Count < 1)
			{
				throw new Exception("No outcalls found");
			}
			CanisterHttpRequest outcall = outcalls[0];


			await this.HttpClient.MockCanisterHttpResponseAsync(
				this.InstanceId,
				outcall.RequestId,
				outcall.SubnetId,
				response,
				additionalResponses
			);

			return await this.HttpClient.AwaitIngressMessageAsync(this.InstanceId, requestId, effectivePrincipal);
		}


		/// <summary>
		/// Awaits an update call response for a given request id, from the <see cref="UpdateCallRawAsynchronousAsync"/> method
		/// </summary>
		/// <param name="requestId">The request id to await</param>
		/// <param name="effectivePrincipal">The effective principal for the request</param>
		/// <returns>The response from the update call</returns>
		public async Task<CandidArg> AwaitUpdateCallAsync(RequestId requestId, EffectivePrincipal effectivePrincipal)
		{
			return await this.HttpClient.AwaitIngressMessageAsync(this.InstanceId, requestId, effectivePrincipal);
		}

		/// <summary>
		/// Awaits an update call response for a given request id, from the <see cref="UpdateCallRawAsynchronousAsync"/> method
		/// </summary>
		/// <param name="requestId">The request id to await</param>
		/// <param name="canisterId">The canister id for the request</param>
		public async Task<CandidArg> AwaitUpdateCallAsync(RequestId requestId, Principal canisterId)
		{
			return await this.AwaitUpdateCallAsync(requestId, EffectivePrincipal.Canister(canisterId));
		}

		/// <summary>
		/// Disposes of the PocketIC instance by deleting the instance
		/// </summary>
		/// <returns></returns>
		public async ValueTask DisposeAsync()
		{
			await this.HttpClient.DeleteInstanceAsync(this.InstanceId);
		}

		/// <summary>
		/// Creates a new PocketIC instance using an IPocketIcHttpClient instance
		/// </summary>
		/// <param name="httpClient">The HTTP client to use</param>
		/// <param name="applicationSubnets">Optional application subnet configurations. Will create a single application subnet if not specified</param>
		/// <param name="bitcoinSubnet">Optional Bitcoin subnet configuration. Will not create if not specified</param>
		/// <param name="fiduciarySubnet">Optional fiduciary subnet configuration. Will not create if not specified</param>
		/// <param name="iiSubnet">Optional Internet Identity subnet configuration. Will not create if not specified</param>
		/// <param name="nnsSubnet">Optional Network Nervous System subnet configuration. Will not create if not specified</param>
		/// <param name="snsSubnet">Optional Service Nervous System subnet configuration. Will not create if not specified</param>
		/// <param name="systemSubnets">Optional system subnet configurations. Will not create if not specified</param>
		/// <param name="verifiedApplicationSubnets">Optional verified application subnet configurations</param>
		/// <param name="nonmainnetFeatures">Whether to enable non-mainnet features. Defaults to false</param>
		/// <param name="candidConverter">Optional candid converter to use, otherwise will use the default</param>
		/// <returns>A new PocketIC instance</returns>
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

		/// <summary>
		/// Creates a new PocketIC instance
		/// </summary>
		/// <param name="url">The PocketIC server url</param>
		/// <param name="applicationSubnets">Optional application subnet configurations. Will create a single application subnet if not specified</param>
		/// <param name="bitcoinSubnet">Optional Bitcoin subnet configuration. Will not create if not specified</param>
		/// <param name="fiduciarySubnet">Optional fiduciary subnet configuration. Will not create if not specified</param>
		/// <param name="iiSubnet">Optional Internet Identity subnet configuration. Will not create if not specified</param>
		/// <param name="nnsSubnet">Optional Network Nervous System subnet configuration. Will not create if not specified</param>
		/// <param name="snsSubnet">Optional Service Nervous System subnet configuration. Will not create if not specified</param>
		/// <param name="systemSubnets">Optional system subnet configurations. Will not create if not specified</param>
		/// <param name="verifiedApplicationSubnets">Optional verified application subnet configurations</param>
		/// <param name="nonmainnetFeatures">Whether to enable non-mainnet features. Defaults to false</param>
		/// <param name="candidConverter">Optional candid converter to use, otherwise will use the default</param>
		/// <param name="requestTimeout">Optional request timeout for http requests. Defaults to 30 seconds</param>
		/// <returns>A new PocketIC instance</returns>
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

	/// <summary>
	/// Represents an HTTP gateway for accessing the Internet Computer. Disposing of this object will stop the gateway
	/// </summary>
	public class HttpGateway : IAsyncDisposable
	{
		/// <summary>
		/// The URL of the HTTP gateway
		/// </summary>
		public Uri Url { get; }
		private readonly Func<Task> disposeTask;

		internal HttpGateway(Uri url, Func<Task> disposeTask)
		{
			this.Url = url;
			this.disposeTask = disposeTask;
		}

		/// <summary>
		/// Creates a new HTTP agent configured to use this gateway
		/// </summary>
		/// <param name="identity">Optional identity to use for the agent, otherwise will use anonymous identity</param>
		/// <returns>A configured HTTP agent</returns>
		public HttpAgent BuildHttpAgent(IIdentity? identity = null)
		{
			return new HttpAgent(
				identity: identity,
				httpBoundryNodeUrl: this.Url
			);
		}

		/// <summary>
		/// Disposes of the HTTP gateway
		/// </summary>
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