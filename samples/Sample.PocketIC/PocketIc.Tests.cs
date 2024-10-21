using System.Net;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC;
using EdjCase.ICP.PocketIC.Models;
using Xunit;

namespace Sample.PocketIC
{
	public class PocketIcServerFixture : IDisposable
	{
		public PocketIcServer Server { get; private set; }

		public PocketIcServerFixture()
		{
			// Start the server for all tests
			this.Server = PocketIcServer.Start().GetAwaiter().GetResult();
		}

		public void Dispose()
		{
			// Stop the server after all tests
			if (this.Server != null)
			{
				this.Server.Stop().GetAwaiter().GetResult();
				this.Server.Dispose();
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
	}
}
