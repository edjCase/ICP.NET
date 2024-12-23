using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid.Models;
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
		SubnetConfig nnsSubnet = SubnetConfig.New();

		IPocketIcHttpClient httpClient = new PocketIcHttpClient(new System.Net.Http.HttpClient(), this.url, TimeSpan.FromSeconds(5));
		int? instanceId = null;
		// Create new pocketic instance for test, then dispose it
		await using (PocketIc pocketIc = await PocketIc.CreateAsync(httpClient, nnsSubnet: nnsSubnet))
		{
			instanceId = pocketIc.InstanceId;

			// Validate instance is available
			List<Instance> instances = await httpClient.GetInstancesAsync();
			Assert.Equal(InstanceStatus.Available, instances[instanceId.Value].Status);

			// Check topology
			List<SubnetTopology> subnetTopologies = await pocketIc.GetTopologyAsync(useCache: false);
			Assert.NotNull(subnetTopologies);

			SubnetTopology appTopology = Assert.Single(subnetTopologies, s => s.Type == SubnetType.Application);
			Assert.Equal(SubnetType.Application, appTopology.Type);
			Assert.Equal(13, appTopology.NodeIds.Count);

			SubnetTopology nnsTopology = Assert.Single(subnetTopologies, s => s.Type == SubnetType.NNS);
			Assert.Equal(SubnetType.NNS, nnsTopology.Type);
			Assert.Equal(40, nnsTopology.NodeIds.Count);


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
			Principal canisterId = response.CanisterId;

			// Check cycles
			ulong balance = await pocketIc.GetCyclesBalanceAsync(canisterId);
			Assert.Equal(initialCyclesAmount, balance);

			ulong newBalance = await pocketIc.AddCyclesAsync(canisterId, 10);
			Assert.Equal(balance + 10, newBalance);

			// Install code
			byte[] wasmModule = File.ReadAllBytes("CanisterWasmModules/counter.wasm");
			CandidArg arg = CandidArg.FromCandid();

			await pocketIc.InstallCodeAsync(
				canisterId: canisterId,
				wasmModule: wasmModule,
				arg: arg,
				mode: InstallCodeMode.Install
			);

			// Start canister
			await pocketIc.StartCanisterAsync(canisterId);

			// Test 'get' counter value
			UnboundedUInt counterValue = await pocketIc.QueryCallAsync<UnboundedUInt>(
				Principal.Anonymous(),
				canisterId,
				"get"
			);

			Assert.Equal((UnboundedUInt)0, counterValue);

			// Test 'inc' counter value
			await pocketIc.UpdateCallNoResponseAsync(
				Principal.Anonymous(),
				canisterId,
				"inc"
			);

			// Test 'get' counter value after inc
			counterValue = await pocketIc.QueryCallAsync<UnboundedUInt>(
				Principal.Anonymous(),
				canisterId,
				"get"
			);
			Assert.Equal((UnboundedUInt)1, counterValue);

			RequestId requestId = await pocketIc.UpdateCallRawAsynchronousAsync(
				Principal.Anonymous(),
				canisterId,
				"inc"
			);
			CandidArg incResponse = await pocketIc.AwaitUpdateCallAsync(requestId, canisterId);
			Assert.Equal(CandidArg.Empty(), incResponse);


			// Test 'get' counter value after inc
			counterValue = await pocketIc.QueryCallAsync<UnboundedUInt>(
				Principal.Anonymous(),
				canisterId,
				"get"
			);
			Assert.Equal((UnboundedUInt)2, counterValue);



			// Test tick doesn't throw
			await pocketIc.TickAsync();

			// Test time
			ICTimestamp initialTime = await pocketIc.GetTimeAsync();

			ICTimestamp newTime = ICTimestamp.Now();
			await pocketIc.SetTimeAsync(newTime);

			ICTimestamp resetTime = await pocketIc.GetTimeAsync();

			Assert.Equal(newTime.NanoSeconds, resetTime.NanoSeconds);


			// Test subnet id
			Principal subnetId = await pocketIc.GetSubnetIdForCanisterAsync(canisterId);
			Assert.NotNull(subnetId);

			// Test public key
			Principal publicKey = await pocketIc.GetPublicKeyForSubnetAsync(subnetId);
			Assert.NotNull(publicKey);

			byte[] newStableMemory = new byte[8];
			newStableMemory[6] = 1;
			await pocketIc.SetStableMemoryAsync(canisterId, newStableMemory);

			byte[] stableMemory = await pocketIc.GetStableMemoryAsync(canisterId);
			Assert.Equal(newStableMemory, stableMemory[..8]);


			// Test auto progress time
			await using (await pocketIc.AutoProgressAsync())
			{
				await Task.Delay(100);
				ICTimestamp autoProgressedTime = await pocketIc.GetTimeAsync();
				Assert.True(autoProgressedTime.NanoSeconds > resetTime.NanoSeconds);
				// Setup http gateway and test api call to canister (needs auto progress)
				await using (HttpGateway httpGateway = await pocketIc.RunHttpGatewayAsync())
				{
					HttpAgent agent = httpGateway.BuildHttpAgent();
					QueryResponse getResponse = await agent.QueryAsync(canisterId, "get", CandidArg.Empty());
					CandidArg getResponseArg = getResponse.ThrowOrGetReply();
					UnboundedUInt getResponseValue = getResponseArg.ToObjects<UnboundedUInt>();
					Assert.Equal((UnboundedUInt)2, getResponseValue);

					CancellationTokenSource cts = new(TimeSpan.FromSeconds(50));
					CandidArg incResponseArg = await agent.CallAsync(canisterId, "inc", CandidArg.Empty(), cancellationToken: cts.Token);
					Assert.Equal(CandidArg.Empty(), incResponseArg);

					getResponse = await agent.QueryAsync(canisterId, "get", CandidArg.Empty());
					getResponseArg = getResponse.ThrowOrGetReply();
					getResponseValue = getResponseArg.ToObjects<UnboundedUInt>();
					Assert.Equal((UnboundedUInt)3, getResponseValue);

				}
			}

			// Verify time is stopped
			ICTimestamp stopProgressTime = await pocketIc.GetTimeAsync();
			await Task.Delay(100);
			ICTimestamp stopProgressTime2 = await pocketIc.GetTimeAsync();
			Assert.Equal(stopProgressTime.NanoSeconds, stopProgressTime2.NanoSeconds);

			// Stop canister
			await pocketIc.StopCanisterAsync(canisterId);
		}
		if (instanceId != null)
		{
			List<Instance> instances = await httpClient.GetInstancesAsync();
			Assert.Equal(InstanceStatus.Deleted, instances[instanceId.Value].Status);
		}
	}


	[Fact]
	public async Task Test_MockHttp()
	{
		SubnetConfig nnsSubnet = SubnetConfig.New();

		IPocketIcHttpClient httpClient = new PocketIcHttpClient(new System.Net.Http.HttpClient(), this.url, TimeSpan.FromSeconds(5));
		// Create new pocketic instance for test, then dispose it
		await using (PocketIc pocketIc = await PocketIc.CreateAsync(httpClient, nnsSubnet: nnsSubnet))
		{

			byte[] wasmModule = File.ReadAllBytes("CanisterWasmModules/http_outcall.wasm");

			Principal canisterId = await pocketIc.CreateAndInstallCanisterAsync(
				wasmModule,
				CandidArg.Empty(),
				cyclesAmount: 1_000_000_000_000
			);

			CanisterHttpReply httpResponse = new CanisterHttpReply
			{
				Body = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
				Headers = new List<(string, string)>
				{
					("Content-Type", "application/octet-stream")
				},
				Status = HttpStatusCode.OK
			};
			CandidArg response = await pocketIc.UpdateCallRawWithHttpOutcallMockAsync(
				Principal.Anonymous(),
				canisterId,
				"http_outcall",
				CandidArg.Empty(),
				httpResponse
			);

			byte[] returnedBytes = response.ToObjects<byte[]>();
			Assert.Equal(httpResponse.Body, returnedBytes);
		}
	}
}
