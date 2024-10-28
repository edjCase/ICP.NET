using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC;
using EdjCase.ICP.PocketIC.Client;
using EdjCase.ICP.PocketIC.Models;
using Xunit;

namespace EdjCase.ICP.PocketIC.Tests;


public class PocketIcTests : IClassFixture<PocketIcServerFixture>
{
	private readonly PocketIcServerFixture fixture;
	private string url => this.fixture.Server.GetUrl();

	public PocketIcTests(PocketIcServerFixture fixture)
	{
		this.fixture = fixture;
	}

	[Fact]
	public async Task Test()
	{
		IPocketIcHttpClient httpClient = new PocketIcHttpClient(new System.Net.Http.HttpClient(), this.url);
		int? instanceId = null;
		// Create new pocketic instance for test, then dispose it
		await using (PocketIc pocketIc = await PocketIc.CreateAsync(httpClient))
		{
			instanceId = pocketIc.InstanceId;

			// Validate instance is available
			List<Instance> instances = await httpClient.GetInstancesAsync();
			Assert.Equal(InstanceStatus.Available, instances[instanceId.Value].Status);

			// Check topology
			List<SubnetTopology> subnetTopologies = await pocketIc.GetTopologyAsync(useCache: false);
			Assert.NotNull(subnetTopologies);
			SubnetTopology subnetTopology = Assert.Single(subnetTopologies);
			Assert.Equal(SubnetType.Application, subnetTopology.Type);
			Assert.Equal(13, subnetTopology.NodeIds.Count);

			UnboundedUInt initialCyclesAmount = 1_000_000_000_000; // 1 trillion cycles

			// Create canister
			CreateCanisterResponse response = await pocketIc.CreateCanisterAsync(
				settings: new CanisterSettings
				{
					ComputeAllocation = OptionalValue<UnboundedUInt>.WithValue(0),
					Controllers = OptionalValue<List<Principal>>.WithValue([Principal.Anonymous()]),
					FreezingThreshold = OptionalValue<UnboundedUInt>.WithValue(0),
					MemoryAllocation = OptionalValue<UnboundedUInt>.WithValue(0),
					ReservedCyclesLimit = OptionalValue<UnboundedUInt>.WithValue(0),
				},
				cyclesAmount: initialCyclesAmount,
				specifiedId: null
			);
			Assert.NotNull(response);
			Assert.NotNull(response.CanisterId);

			// Check cycles
			ulong balance = await pocketIc.GetCyclesBalanceAsync(response.CanisterId);
			Assert.Equal(initialCyclesAmount, balance);

			ulong newBalance = await pocketIc.AddCyclesAsync(response.CanisterId, 10);
			Assert.Equal(balance + 10, newBalance);

			// Install code
			byte[] wasmModule = File.ReadAllBytes("CanisterWasmModules/counter.wasm");
			CandidArg arg = CandidArg.FromCandid();

			await pocketIc.InstallCodeAsync(
				canisterId: response.CanisterId,
				wasmModule: wasmModule,
				arg: arg,
				mode: InstallCodeMode.Install
			);

			// Start canister
			await pocketIc.StartCanisterAsync(response.CanisterId);

			// Test 'get' counter value
			UnboundedUInt counterValue = await pocketIc.QueryCallAsync<UnboundedUInt>(
				Principal.Anonymous(),
				response.CanisterId,
				"get"
			);

			Assert.Equal((UnboundedUInt)0, counterValue);

			// Test 'inc' counter value
			await pocketIc.UpdateCallNoResponseAsync(
				Principal.Anonymous(),
				response.CanisterId,
				"inc"
			);

			// Test 'get' counter value after inc
			counterValue = await pocketIc.QueryCallAsync<UnboundedUInt>(
				Principal.Anonymous(),
				response.CanisterId,
				"get"
			);
			Assert.Equal((UnboundedUInt)1, counterValue);

			// Test tick doesn't throw
			await pocketIc.TickAsync();

			// Test time
			ICTimestamp initialTime = await pocketIc.GetTimeAsync();

			await pocketIc.AdvanceTimeAsync(TimeSpan.FromMinutes(1));

			ICTimestamp newTime = await pocketIc.GetTimeAsync();

			Assert.Equal(initialTime.NanoSeconds + 60_000_000_000ul, newTime.NanoSeconds);

			await pocketIc.SetTimeAsync(initialTime);

			ICTimestamp resetTime = await pocketIc.GetTimeAsync();

			Assert.Equal(initialTime.NanoSeconds, resetTime.NanoSeconds);

			// Test subnet id
			Principal subnetId = await pocketIc.GetSubnetIdForCanisterAsync(response.CanisterId);
			Assert.NotNull(subnetId);

			// Test public key
			Principal publicKey = await pocketIc.GetPublicKeyForSubnetAsync(subnetId);
			Assert.NotNull(publicKey);

			byte[] newStableMemory = new byte[8];
			newStableMemory[6] = 1;
			await pocketIc.SetStableMemoryAsync(response.CanisterId, newStableMemory);

			byte[] stableMemory = await pocketIc.GetStableMemoryAsync(response.CanisterId);
			Assert.Equal(newStableMemory, stableMemory[..8]);


			// Stop canister
			await pocketIc.StopCanisterAsync(response.CanisterId);
		}
		if (instanceId != null)
		{
			List<Instance> instances = await httpClient.GetInstancesAsync();
			Assert.Equal(InstanceStatus.Deleted, instances[instanceId.Value].Status);
		}
	}
}
