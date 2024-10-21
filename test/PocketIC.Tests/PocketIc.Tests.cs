using System.Net;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC;
using EdjCase.ICP.PocketIC.Models;

namespace EdjCase.ICP.PocketIC.Tests
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
		public async Task CreateCanisterAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();
			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				CreateCanisterResponse response = await pocketIc.CreateCanisterAsync();

				Assert.NotNull(response);
				Assert.NotNull(response.CanisterId);
			}
		}

		[Test]
		public async Task CreateAndInstallCanisterAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();
			byte[] wasmModule = File.ReadAllBytes("CanisterWasmModules/counter.wasm");
			CandidArg arg = CandidArg.FromCandid();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				Principal canisterId = await pocketIc.CreateAndInstallCanisterAsync(wasmModule, arg);

				Assert.NotNull(canisterId);
			}
		}

		[Test]
		public async Task Create_And_StartCanisterAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				CreateCanisterResponse response = await pocketIc.CreateCanisterAsync();
				await pocketIc.StartCanisterAsync(new StartCanisterRequest { CanisterId = response.CanisterId });

				// No exception means success
				Assert.Pass();
			}
		}

		[Test]
		public async Task Create_And_StopCanisterAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				CreateCanisterResponse response = await pocketIc.CreateCanisterAsync();
				await pocketIc.StopCanisterAsync(new StopCanisterRequest { CanisterId = response.CanisterId });

				// No exception means success
				Assert.Pass();
			}
		}

		[Test]
		public async Task Create_And_InstallCodeAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();
			byte[] wasmModule = File.ReadAllBytes("CanisterWasmModules/counter.wasm");
			CandidArg arg = CandidArg.FromCandid();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				CreateCanisterResponse response = await pocketIc.CreateCanisterAsync();
				await pocketIc.InstallCodeAsync(new InstallCodeRequest
				{
					CanisterId = response.CanisterId,
					WasmModule = wasmModule,
					Arg = arg.Encode(),
					Mode = InstallCodeMode.Install
				});

				// No exception means success
				Assert.Pass();
			}
		}

		[Test]
		public async Task QueryCallAsync_CounterWasm__Basic__Valid()
		{
			string url = this.server.GetUrl();
			byte[] wasmModule = File.ReadAllBytes("CanisterWasmModules/counter.wasm");
			CandidArg arg = CandidArg.FromCandid();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				Principal canisterId = await pocketIc.CreateAndInstallCanisterAsync(wasmModule, arg);
				var result = await pocketIc.QueryCallNoRequestAsync<UnboundedUInt>(
					Principal.Anonymous(),
					canisterId,
					"get"
				);

				Assert.NotNull(result);
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
				await pocketIc.UpdateCallNoRequestOrResponseAsync(
					Principal.Anonymous(),
					canisterId,
					"inc"
				);

				Assert.Pass();
			}
		}

		[Test]
		public async Task TickAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				await pocketIc.TickAsync();

				// No exception means success
				Assert.Pass();
			}
		}

		[Test]
		public async Task GetTimeAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				var time = await pocketIc.GetTimeAsync();

				Assert.NotNull(time);
			}
		}

		[Test]
		public async Task AdvanceTimeAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				ICTimestamp initialTime = await pocketIc.GetTimeAsync();

				await pocketIc.AdvanceTimeAsync(TimeSpan.FromMinutes(1));

				ICTimestamp newTime = await pocketIc.GetTimeAsync();

				Assert.That(newTime.NanoSeconds, Is.EqualTo(initialTime.NanoSeconds + 60_000_000_000ul));

				await pocketIc.SetTimeAsync(initialTime);

				ICTimestamp resetTime = await pocketIc.GetTimeAsync();

				Assert.That(resetTime.NanoSeconds, Is.EqualTo(initialTime.NanoSeconds));

				// No exception means success
				Assert.Pass();
			}
		}

		[Test]
		public async Task GetCanisterSubnetIdAsync_Basic_Valid()
		{
			string url = this.server.GetUrl();
			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				CreateCanisterResponse response = await pocketIc.CreateCanisterAsync();
				Principal subnetId = await pocketIc.GetCanisterSubnetIdAsync(response.CanisterId);
				Assert.NotNull(subnetId);
			}
		}
	}
}