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
		// private readonly PocketIcServerFixture fixture;
		// private string url => this.fixture.Server.GetUrl();

		// public PocketIcTests(PocketIcServerFixture fixture)
		// {
		// 	this.fixture = fixture;
		// }
		private string url = "http://localhost:42915";
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
				await using (await pocketIc.AutoProgressTimeAsync())
				{
					await using (HttpGateway httpGateway = await pocketIc.RunHttpGatewayAsync())
					{
						HttpAgent agent = httpGateway.BuildHttpAgent();
						QueryResponse getResponse = await agent.QueryAsync(canisterId, "get", CandidArg.Empty());
						CandidArg getResponseArg = getResponse.ThrowOrGetReply();
						UnboundedUInt getResponseValue = getResponseArg.ToObjects<UnboundedUInt>();
						Assert.Equal((UnboundedUInt)0, getResponseValue);


						Debug.WriteLine("---Start-----\n\n\n\n\n\n\n\n\n\n\n");


						// CandidArg incResponseArg = await agent.CallAsync(canisterId, "inc", CandidArg.Empty(), cancellationToken: cts.Token);
						// Assert.Equal(CandidArg.Empty(), incResponseArg);

						// This alternative also doesnt work
						var status = await agent.GetReplicaStatusAsync();
						var state = await agent.ReadStateAsync(canisterId, new List<StatePath>());
						RequestId requestId = await agent.CallAsynchronousAsync(canisterId, "inc", CandidArg.Empty());
						// ICTimestamp currentTime = await pocketIc.GetTimeAsync();
						// await pocketIc.SetTimeAsync(currentTime + TimeSpan.FromSeconds(5)); // Doesnt work
						// await pocketIc.TickAsync(1); // Works
						await Task.Delay(5000); // Doesnt work
						IngressStatus ingressStatus = await pocketIc.GetIngressStatusAsync(requestId, EffectivePrincipal.Canister(canisterId));

						switch (ingressStatus.Type)
						{
							case IngressStatusType.Ok:
								break;
							case IngressStatusType.NotFound:
								throw new Exception("Ingress message not found");
							default:
								throw new Exception("Ingress message ????");
						}

						throw new Exception("Success");


						CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
						CandidArg incResponseArg = await agent.WaitForRequestAsync(canisterId, requestId, cts.Token); // Waits indefinitely here
						Assert.Equal(CandidArg.Empty(), incResponseArg);

						getResponse = await agent.QueryAsync(canisterId, "get", CandidArg.Empty());
						getResponseArg = getResponse.ThrowOrGetReply();
						getResponseValue = getResponseArg.ToObjects<UnboundedUInt>();
						Assert.Equal((UnboundedUInt)1, getResponseValue);

						// CandidArg setRequestArg = CandidArg.FromObjects((UnboundedUInt)10);
						// cts = new(TimeSpan.FromSeconds(5));
						// CandidArg setResponseArg = await agent.CallAsync(canisterId, "set", setRequestArg, cancellationToken: cts.Token);
						// Assert.Equal(CandidArg.Empty(), setResponseArg);

						// await pocketIc.TickAsync();

						// getResponse = await agent.QueryAsync(canisterId, "get", CandidArg.Empty());
						// getResponseArg = getResponse.ThrowOrGetReply();
						// getResponseValue = getResponseArg.ToObjects<UnboundedUInt>();
						// Assert.Equal((UnboundedUInt)10, getResponseValue);

					}
				}
			}
		}

		private async Task Common(Func<HttpAgent, Principal, HttpGateway, Task<RequestId>> callFunc)
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
				await using (await pocketIc.AutoProgressTimeAsync())
				{
					await using (HttpGateway httpGateway = await pocketIc.RunHttpGatewayAsync())
					{
						HttpAgent agent = httpGateway.BuildHttpAgent();

						Debug.Indent();
						try
						{
							Debug.WriteLine("---Start-----\n\n\n\n\n\n\n\n\n\n\n");
							List<RequestId> badRequestIds = new();
							List<RequestId> goodRequestIds = new();
							int retries = 0;
							while (true)
							{
								RequestId requestId = await callFunc(agent, canisterId, httpGateway);
								// await pocketIc.TickAsync(1); // Works
								await Task.Delay(100);
								IngressStatus ingressStatus = await pocketIc.GetIngressStatusAsync(requestId, EffectivePrincipal.Canister(canisterId));

								switch (ingressStatus.Type)
								{
									case IngressStatusType.Ok:
										goodRequestIds.Add(requestId);
										Debug.WriteLine("Success");
										foreach (var badRequestId in badRequestIds)
										{
											Debug.WriteLine(badRequestId + " - Bad");
										}
										foreach (var goodRequestId in goodRequestIds)
										{
											Debug.WriteLine(goodRequestId + " - Good");
										}
										return;
									case IngressStatusType.NotFound:
										badRequestIds.Add(requestId);
										retries++;
										continue;
									default:
										throw new Exception("Ingress message ????");
								}
							}
						}
						finally
						{
							Debug.Unindent();
						}
					}
				}
			}
		}


		[Fact]
		public async Task HttpGateway_Ingress_Code()
		{
			await this.Common(async (agent, canisterId, httpGateway) =>
			{
				return await agent.CallAsynchronousAsync(canisterId, "inc", CandidArg.Empty());
			});
		}

		[Fact]
		public async Task HttpGateway_Ingress_Dfx()
		{
			await this.Common(async (agent, canisterId, httpGateway) =>
			{
				var processInfo = new ProcessStartInfo
				{
					FileName = "dfx",
					Arguments = $"canister call {canisterId} inc () --network {httpGateway.Url} --identity anonymous --verbose --async",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WorkingDirectory = "/home/gekctek/git/ICP.NET",
				};
				Debug.WriteLine("dfx " + processInfo.Arguments);
				using (var process = Process.Start(processInfo))
				{
					string output = process!.StandardOutput.ReadToEnd();
					string error = process.StandardError.ReadToEnd();
					process.WaitForExit();

					// Optionally log or handle the output/error
					Debug.WriteLine($"Output: {output}");
					Debug.WriteLine($"Error: {error}");
					return new RequestId(Convert.FromHexString(output.Trim().Substring(2)));
				}
			});
		}

		[Fact]
		public async Task HttpGateway_Ingress_Code_V3()
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
				await using (await pocketIc.AutoProgressTimeAsync())
				{
					await using (HttpGateway httpGateway = await pocketIc.RunHttpGatewayAsync())
					{
						HttpAgent agent = httpGateway.BuildHttpAgent();

						Debug.Indent();
						try
						{
							Debug.WriteLine("---Start-----\n\n\n\n\n\n\n\n\n\n\n");
							int retries = 0;
							while (true)
							{
								CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
								try
								{
									await agent.CallAsync(canisterId, "inc", CandidArg.Empty(), cancellationToken: cts.Token);
									Debug.WriteLine("Success. Retries: " + retries);
									return;
								}
								catch (OperationCanceledException e)
								{
									retries++;
								}
							}
						}
						finally
						{
							Debug.Unindent();
						}
					}
				}
			}
		}
	}
}
