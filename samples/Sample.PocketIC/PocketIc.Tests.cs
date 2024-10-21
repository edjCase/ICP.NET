using System.Net;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC;
using EdjCase.ICP.PocketIC.Models;

namespace Sample.PocketIC
{
	public class PocketIcTests
	{
		private PocketIcServer server;

		[OneTimeSetUp]
		public async Task Setup()
		{
			// Start the server for all tests
			this.server = await PocketIcServer.Start();
		}

		[OneTimeTearDown]
		public async Task Teardown()
		{
			// Stop the server after all tests
			if (this.server != null)
			{
				await this.server.Stop();
				this.server.Dispose();
			}
		}

		[Test]
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
				Assert.That(value, Is.EqualTo((UnboundedUInt)0));


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
				Assert.That(value, Is.EqualTo((UnboundedUInt)1));

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

				Assert.That(value, Is.EqualTo((UnboundedUInt)10));
			}
		}
	}
}