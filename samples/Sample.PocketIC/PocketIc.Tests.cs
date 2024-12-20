using System.Diagnostics;
using System.Net;
using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC;
using EdjCase.ICP.PocketIC.Client;
using EdjCase.ICP.PocketIC.Models;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Cms;
using Xunit;

namespace Sample.PocketIC
{
	public class PocketIcServerFixture : IDisposable
	{
		public PocketIcServer Server { get; private set; }


		public PocketIcServerFixture()
		{
			// Start the server for all tests
			this.Server = PocketIcServer.StartAsync(runtimeLogLevel: LogLevel.Debug, showErrorLogs: true).GetAwaiter().GetResult();

		}

		public void Dispose()
		{
			// Stop the server after all tests
			if (this.Server != null)
			{
				this.Server.StopAsync().GetAwaiter().GetResult();
				this.Server.DisposeAsync().GetAwaiter().GetResult();
			}
		}
	}

	public class PocketIcTests : IClassFixture<PocketIcServerFixture>
	{
		private readonly PocketIcServerFixture fixture;
		private string url => this.fixture.Server.GetUrl();

		public PocketIcTests(PocketIcServerFixture fixture)
		{
			this.fixture = fixture;
		}

		[Fact]
		public async Task UpdateCallAsync_CounterWasm__Basic__Valid()
		{
			byte[] wasmModule = File.ReadAllBytes("CanisterWasmModules/counter.wasm");
			CandidArg arg = CandidArg.FromCandid();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(this.url))
			{
				Principal canisterId = await pocketIc.CreateAndInstallCanisterAsync(wasmModule, arg);

				UnboundedUInt value = await pocketIc.QueryCallAsync<UnboundedUInt>(
					Principal.Anonymous(),
					canisterId,
					"get"
				);
				Assert.Equal((UnboundedUInt)0, value);


				await pocketIc.UpdateCallNoResponseAsync(
					Principal.Anonymous(),
					canisterId,
					"inc"
				);

				value = await pocketIc.QueryCallAsync<UnboundedUInt>(
					Principal.Anonymous(),
					canisterId,
					"get"
				);
				Assert.Equal((UnboundedUInt)1, value);

				await pocketIc.UpdateCallNoResponseAsync(
					Principal.Anonymous(),
					canisterId,
					"set",
					(UnboundedUInt)10
				);

				value = await pocketIc.QueryCallAsync<UnboundedUInt>(
					Principal.Anonymous(),
					canisterId,
					"get"
				);

				Assert.Equal((UnboundedUInt)10, value);
			}
		}


		[Fact]
		public async Task HttpGateway_CounterWasm__Basic__Valid()
		{
			byte[] wasmModule = File.ReadAllBytes("CanisterWasmModules/counter.wasm");
			CandidArg arg = CandidArg.FromCandid();


			SubnetConfig nnsSubnet = SubnetConfig.New(); // NNS subnet required for HttpGateway

			await using (PocketIc pocketIc = await PocketIc.CreateAsync(this.url, nnsSubnet: nnsSubnet))
			{
				Principal canisterId = await pocketIc.CreateAndInstallCanisterAsync(wasmModule, arg);

				await pocketIc.StartCanisterAsync(canisterId);

				await pocketIc.SetTimeAsync(ICTimestamp.Now());
				// Let time progress so that update calls get processed
				await using (await pocketIc.AutoProgressAsync())
				{
					await using (HttpGateway httpGateway = await pocketIc.RunHttpGatewayAsync())
					{
						HttpAgent agent = httpGateway.BuildHttpAgent();
						QueryResponse getResponse = await agent.QueryAsync(canisterId, "get", CandidArg.Empty());
						CandidArg getResponseArg = getResponse.ThrowOrGetReply();
						UnboundedUInt getResponseValue = getResponseArg.ToObjects<UnboundedUInt>();
						Assert.Equal((UnboundedUInt)0, getResponseValue);


						CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
						CandidArg incResponseArg = await agent.CallAsync(canisterId, "inc", CandidArg.Empty(), cancellationToken: cts.Token);
						Assert.Equal(CandidArg.Empty(), incResponseArg);

						getResponse = await agent.QueryAsync(canisterId, "get", CandidArg.Empty());
						getResponseArg = getResponse.ThrowOrGetReply();
						getResponseValue = getResponseArg.ToObjects<UnboundedUInt>();
						Assert.Equal((UnboundedUInt)1, getResponseValue);

						CandidArg setRequestArg = CandidArg.FromObjects((UnboundedUInt)10);
						cts = new(TimeSpan.FromSeconds(5));
						CandidArg setResponseArg = await agent.CallAsync(canisterId, "set", setRequestArg, cancellationToken: cts.Token);
						Assert.Equal(CandidArg.Empty(), setResponseArg);

						getResponse = await agent.QueryAsync(canisterId, "get", CandidArg.Empty());
						getResponseArg = getResponse.ThrowOrGetReply();
						getResponseValue = getResponseArg.ToObjects<UnboundedUInt>();
						Assert.Equal((UnboundedUInt)10, getResponseValue);

					}
				}
			}
		}
	}
}
