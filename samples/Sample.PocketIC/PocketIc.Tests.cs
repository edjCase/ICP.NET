using System.Net;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC;
using EdjCase.ICP.PocketIC.Models;
using Xunit;

namespace Sample.PocketIC
{
	public class PocketIcTests
	{
		private PocketIcServer server;

		public PocketIcTests()
		{
			// Start the server for all tests
			this.server = PocketIcServer.Start().GetAwaiter().GetResult();
		}

		public async ValueTask DisposeAsync()
		{
			// Stop the server after all tests
			if (this.server != null)
			{
				await this.server.Stop();
				this.server.Dispose();
			}
		}

		[Fact]
		public async Task UpdateCallAsync_CounterWasm__Basic__Valid()
		{
			string url = this.server.GetUrl();
			byte[] wasmModule = File.ReadAllBytes("CanisterWasmModules/counter.wasm");
			CandidArg arg = CandidArg.FromCandid();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
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
