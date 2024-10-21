using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC;
using EdjCase.ICP.PocketIC.Models;
using Xunit;

namespace EdjCase.ICP.PocketIC.Tests
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

		[Fact]
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

		[Fact]
		public async Task Create_And_StartCanisterAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				CreateCanisterResponse response = await pocketIc.CreateCanisterAsync();
				await pocketIc.StartCanisterAsync(new StartCanisterRequest { CanisterId = response.CanisterId });

			}
		}

		[Fact]
		public async Task Create_And_StopCanisterAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				CreateCanisterResponse response = await pocketIc.CreateCanisterAsync();
				await pocketIc.StopCanisterAsync(new StopCanisterRequest { CanisterId = response.CanisterId });

			}
		}

		[Fact]
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

			}
		}

		[Fact]
		public async Task QueryCallAsync_CounterWasm__Basic__Valid()
		{
			string url = this.server.GetUrl();
			byte[] wasmModule = File.ReadAllBytes("CanisterWasmModules/counter.wasm");
			CandidArg arg = CandidArg.FromCandid();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				Principal canisterId = await pocketIc.CreateAndInstallCanisterAsync(wasmModule, arg);
				var result = await pocketIc.QueryCallAsync<UnboundedUInt>(
					Principal.Anonymous(),
					canisterId,
					"get"
				);

				Assert.NotNull(result);
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
				await pocketIc.UpdateCallNoResponseAsync(
					Principal.Anonymous(),
					canisterId,
					"inc"
				);

			}
		}

		[Fact]
		public async Task TickAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				await pocketIc.TickAsync();
			}
		}

		[Fact]
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

		[Fact]
		public async Task AdvanceTimeAsync__Basic__Valid()
		{
			string url = this.server.GetUrl();

			// Create new pocketic instance for test, then dispose it
			await using (PocketIc pocketIc = await PocketIc.CreateAsync(url))
			{
				ICTimestamp initialTime = await pocketIc.GetTimeAsync();

				await pocketIc.AdvanceTimeAsync(TimeSpan.FromMinutes(1));

				ICTimestamp newTime = await pocketIc.GetTimeAsync();

				Assert.Equal(initialTime.NanoSeconds + 60_000_000_000ul, newTime.NanoSeconds);

				await pocketIc.SetTimeAsync(initialTime);

				ICTimestamp resetTime = await pocketIc.GetTimeAsync();

				Assert.Equal(initialTime.NanoSeconds, resetTime.NanoSeconds);
			}
		}

		[Fact]
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
