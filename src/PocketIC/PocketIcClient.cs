using System.Text;
using System.Text.Json;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

namespace EdjCase.ICP.PocketIC.Client
{
	public class PocketIcClient : IAsyncDisposable
	{
		private readonly HttpClient httpClient;
		private readonly string baseUrl;
		private readonly int instanceId;
		private readonly Dictionary<string, SubnetTopology> topology;

		private PocketIcClient(
			HttpClient httpClient,
			string url,
			int instanceId,
			Dictionary<string, SubnetTopology> topology)
		{
			this.httpClient = httpClient;
			this.baseUrl = url;
			this.instanceId = instanceId;
			this.topology = topology;
		}

		public async Task TickAsync()
		{
			await this.PostAsync("/update/tick", null);
		}

		public async Task<Principal> GetPublicKeyAsync(Principal subnetId)
		{
			var request = new JsonObject
			{
				["subnet_id"] = Convert.ToBase64String(subnetId.Raw)
			};
			JsonNode? response = await this.PostAsync("/read/pub_key", request);
			byte[] publicKey = response!["public_key"].Deserialize<byte[]>()!;
			return Principal.FromBytes(publicKey);
		}

		public Dictionary<string, SubnetTopology> GetTopology()
		{
			return this.topology;
		}

		public async Task<ICTimestamp> GetTimeAsync()
		{
			JsonNode? response = await this.GetAsync("/read/get_time");
			return ICTimestamp.FromNanoSeconds(response!["nanos_since_epoch"].Deserialize<ulong>()!);
		}

		public async Task SetTimeAsync(ICTimestamp timestamp)
		{
			if (!timestamp.NanoSeconds.TryToUInt64(out ulong nanosSinceEpoch))
			{
				throw new ArgumentException("Nanoseconds is too large to convert to ulong");
			}
			var request = new JsonObject
			{
				["nanos_since_epoch"] = nanosSinceEpoch
			};
			await this.PostAsync("/update/set_time", request);
		}

		public async Task<CandidArg> QueryCallAsync(
			Principal sender,
			Principal canisterId,
			string method,
			CandidArg request,
			EffectivePrincipal? effectivePrincipal = null)
		{
			return await this.RequestInternalAsync(
				"/read/query",
				sender,
				canisterId,
				method,
				request,
				effectivePrincipal
			);
		}

		public async Task<CandidArg> UpdateCallAsync(
			Principal sender,
			Principal canisterId,
			string method,
			CandidArg request,
			EffectivePrincipal? effectivePrincipal = null)
		{
			return await this.RequestInternalAsync(
				"/update/execute_ingress_message",
				sender,
				canisterId,
				method,
				request,
				effectivePrincipal
			);
		}


		private async Task<CandidArg> RequestInternalAsync(
			string route,
			Principal sender,
			Principal canisterId,
			string method,
			CandidArg arg,
			EffectivePrincipal? effectivePrincipal = null)
		{
			byte[] payload = arg.Encode();

			JsonNode effectivePrincipalJson = effectivePrincipal == null ?
				JsonValue.Create("None")! :
				new JsonObject
				{
					[effectivePrincipal.Type == EffectivePrincipalType.Subnet ? "SubnetId" : "CanisterId"] =
						Convert.ToBase64String(effectivePrincipal.Id.Raw)
				};

			var options = new JsonObject
			{
				["canister_id"] = Convert.ToBase64String(canisterId.Raw),
				["effective_principal"] = effectivePrincipalJson,
				["method"] = method,
				["payload"] = Convert.ToBase64String(payload),
				["sender"] = Convert.ToBase64String(sender.Raw)
			};
			JsonNode? response = await this.PostAsync(route, options);
			if (response == null)
			{
				throw new Exception("Failed to get response from canister");
			}
			if (response["Err"] != null)
			{
				string message = response!["Err"]!["description"]!.Deserialize<string>()!;
				string code = response!["Err"]!["code"]!.Deserialize<string>()!;
				throw new Exception($"Canister returned an error. Code: {code}, Message: {message}");
			}
			if (response["Ok"] == null)
			{
				throw new Exception("Failed to get a valid response from canister. Response: " + response?.ToJsonString());
			}
			byte[]? candidBytes = response!["Ok"]!["Reply"]?.Deserialize<byte[]>();
			if (candidBytes == null)
			{
				throw new Exception("Failed to get a valid response from canister. Response: " + response?.ToJsonString());
			}
			return CandidArg.FromBytes(candidBytes);
		}
		public async Task<Principal> GetCanisterSubnetIdAsync(Principal canisterId)
		{
			var request = new JsonObject
			{
				["canister_id"] = Convert.ToBase64String(canisterId.Raw)
			};
			JsonNode? response = await this.PostAsync("/read/get_subnet", request);
			byte[] subnetId = response!["subnet_id"].Deserialize<byte[]>()!;
			return Principal.FromBytes(subnetId);
		}

		public async Task<ulong> GetCyclesBalanceAsync(Principal canisterId)
		{
			var request = new JsonObject
			{
				["canister_id"] = Convert.ToBase64String(canisterId.Raw)
			};
			JsonNode? response = await this.PostAsync("/read/get_cycles", request);
			return response!["cycles"].Deserialize<ulong>()!;
		}

		public async Task<ulong> AddCyclesAsync(Principal canisterId, ulong amount)
		{
			var request = new JsonObject
			{
				["canister_id"] = Convert.ToBase64String(canisterId.Raw),
				["amount"] = amount
			};
			JsonNode? response = await this.PostAsync("/update/add_cycles", request);
			return response!["cycles"].Deserialize<ulong>()!;
		}

		public async Task SetStableMemoryAsync(Principal canisterId, byte[] memory)
		{
			byte[] blobId = await this.UploadBlobAsync(memory);
			var request = new JsonObject
			{
				["canister_id"] = Convert.ToBase64String(canisterId.Raw),
				["blob_id"] = JsonValue.Create(blobId)
			};
			await this.PostAsync("/update/set_stable_memory", request);
		}

		public async Task<byte[]> GetStableMemoryAsync(Principal canisterId)
		{
			var request = new JsonObject
			{
				["canister_id"] = Convert.ToBase64String(canisterId.Raw)
			};
			JsonNode? response = await this.PostAsync("/read/get_stable_memory", request);
			return response!["blob_id"].Deserialize<byte[]>()!;
		}

		private async Task<byte[]> UploadBlobAsync(byte[] blob)
		{
			var content = new ByteArrayContent(blob);
			HttpResponseMessage response = await this.httpClient.PostAsync($"{this.baseUrl}/blobstore", content);
			response.EnsureSuccessStatusCode();
			var stream = await response.Content.ReadAsStreamAsync();
			JsonNode? json = await JsonNode.ParseAsync(stream)!;
			return json!["blob"].Deserialize<byte[]>()!;
		}

		private async Task<JsonNode?> GetAsync(string endpoint)
		{
			HttpResponseMessage response = await this.httpClient.GetAsync($"{this.baseUrl}/instances/{this.instanceId}{endpoint}");
			response.EnsureSuccessStatusCode();
			Stream stream = await response.Content.ReadAsStreamAsync();
			return await JsonNode.ParseAsync(stream)!;
		}

		private async Task<JsonNode?> PostAsync(string endpoint, JsonObject? data)
		{
			string url = $"{this.baseUrl}/instances/{this.instanceId}{endpoint}";
			return await PocketIcClient.PostAsync(this.httpClient, url, data);
		}

		private static async Task<JsonNode?> PostAsync(HttpClient httpClient, string url, JsonObject? data)
		{
			var json = data?.ToJsonString() ?? "";
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = await httpClient.PostAsync(url, content);
			response.EnsureSuccessStatusCode();
			var stream = await response.Content.ReadAsStreamAsync();
			return await JsonNode.ParseAsync(stream);
		}

		public async ValueTask DisposeAsync()
		{
			try
			{
				HttpResponseMessage message = await this.httpClient.DeleteAsync($"{this.baseUrl}/instances/{this.instanceId}");
				message.EnsureSuccessStatusCode();
			}
			catch (Exception e)
			{
				throw new Exception("Failed to delete PocketIC instance", e);
			}
		}


		public static async Task<PocketIcClient> CreateAsync(
			string url,
			List<SubnetConfig>? applicationSubnets = null,
			SubnetConfig? bitcoinSubnet = null,
			SubnetConfig? fiduciarySubnet = null,
			SubnetConfig? iiSubnet = null,
			SubnetConfig? nnsSubnet = null,
			SubnetConfig? snsSubnet = null,
			List<SubnetConfig>? systemSubnets = null,
			List<SubnetConfig>? verifiedApplicationSubnets = null,
			bool nonmainnetFeatures = false
		)
		{

			applicationSubnets ??= new List<SubnetConfig>
			{
				new SubnetConfig
				{
					State = SubnetStateConfig.New()
				}
			};

			JsonNode? MapSubnet(SubnetConfig? subnetConfig)
			{
				if (subnetConfig == null)
				{
					return null;
				}
				JsonNode stateConfig;
				switch (subnetConfig.State.Type)
				{
					case SubnetStateType.New:
						stateConfig = JsonValue.Create("New")!;
						break;
					case SubnetStateType.FromPath:
						stateConfig = new JsonObject
						{
							["FromPath"] = new JsonArray
							{
								subnetConfig.State.Path!,
								new JsonObject
								{
									["subnet_id"] = Convert.ToBase64String(subnetConfig.State.SubnetId!.Raw)
								}
							}
						};
						break;
					default:
						throw new NotImplementedException();
				}
				return new JsonObject
				{
					["dts_flag"] = subnetConfig.EnableDeterministicTimeSlicing == false ? "Disabled" : "Enabled",
					["instruction_config"] = subnetConfig.EnableBenchmarkingInstructionLimits == true ? "Benchmarking" : "Production",
					["state_config"] = stateConfig
				};
			}

			JsonArray MapSubnets(List<SubnetConfig>? subnets)
			{
				if (subnets == null)
				{
					return new JsonArray();
				}
				return new JsonArray(subnets.Select(s => MapSubnet(s)).ToArray());
			}

			var request = new JsonObject
			{
				["subnet_config_set"] = new JsonObject
				{
					["application"] = MapSubnets(applicationSubnets),
					["bitcoin"] = MapSubnet(bitcoinSubnet),
					["fiduciary"] = MapSubnet(fiduciarySubnet),
					["ii"] = MapSubnet(iiSubnet),
					["nns"] = MapSubnet(nnsSubnet),
					["sns"] = MapSubnet(snsSubnet),
					["system"] = MapSubnets(systemSubnets),
					["verified_application"] = MapSubnets(verifiedApplicationSubnets)
				},
				["nonmainnet_features"] = nonmainnetFeatures
			};

			var httpClient = new HttpClient();
			JsonNode? response = await PostAsync(httpClient, $"{url}/instances", request);
			if (response == null)
			{
				throw new Exception("Failed to create PocketIC instance, no response from server");
			}

			if (response["Error"] != null)
			{
				string message = response!["error"]!["message"]!.Deserialize<string>()!;
				throw new Exception($"Failed to create PocketIC instance: {message}");
			}
			JsonObject? created = response["Created"]?.AsObject();
			if (created == null)
			{
				throw new Exception("Failed to create PocketIC instance, invalid response from server");
			}

			int instanceId = created["instance_id"]!.Deserialize<int>()!;

			SubnetTopology MapSubnetTopology(string subnetId, JsonNode value)
			{
				string? subnetTypeString = value["subnet_kind"]?.Deserialize<string>();
				if (subnetTypeString == null || !Enum.TryParse<SubnetType>(subnetTypeString, out var subnetType))
				{
					throw new Exception($"Invalid subnet type: {subnetTypeString}");
				}

				byte[] subnetSeed = value["subnet_seed"]
					?.AsArray()
					.Select(b => b.Deserialize<byte>())
					.ToArray()
					?? throw new Exception("Subnet seed is missing or invalid");

				List<byte[]> nodeIds = value["node_ids"]
					?.AsArray()
					.Select(id =>
					{
						byte[]? nodeId = id!["node_id"]?.Deserialize<byte[]>() ?? throw new Exception("Node ID is missing or invalid");
						return nodeId;
					})
					.ToList()
					?? [];

				Principal MapCanisterRangeValue(JsonNode? value)
				{
					byte[] canisterIdBytes = value?["canister_id"]?.Deserialize<byte[]>() ?? throw new Exception("Canister range value is missing or invalid");
					return Principal.FromBytes(canisterIdBytes);
				}

				List<CanisterRange> canisterRanges = value["canister_ranges"]
					?.AsArray()
					.Select(r => new CanisterRange
					{
						Start = MapCanisterRangeValue(r?["start"]),
						End = MapCanisterRangeValue(r?["end"])
					})
					.ToList()
					?? [];

				return new SubnetTopology
				{
					Id = Principal.FromText(subnetId),
					Type = subnetType,
					SubnetSeed = subnetSeed,
					NodeIds = nodeIds,
					CanisterRanges = canisterRanges
				};
			}
			Dictionary<string, SubnetTopology> topology = created["topology"]
				?.Deserialize<Dictionary<string, JsonNode>>()
				?.ToDictionary(kv => kv.Key, kv => MapSubnetTopology(kv.Key, kv.Value))
				?? [];



			return new PocketIcClient(
				httpClient,
				url,
				instanceId,
				topology
			);
		}
	}


	public class SubnetTopology
	{
		public required Principal Id { get; set; }
		public required SubnetType Type { get; set; }
		public required byte[] SubnetSeed { get; set; }
		public required List<byte[]> NodeIds { get; set; }
		public required List<CanisterRange> CanisterRanges { get; set; }
	}

	public class CanisterRange
	{
		public required Principal Start { get; set; }
		public required Principal End { get; set; }
	}

	public enum SubnetType
	{
		Application,
		Bitcoin,
		Fiduciary,
		InternetIdentity,
		NNS,
		SNS,
		System
	}

	public class EffectivePrincipal
	{
		public required EffectivePrincipalType Type { get; set; }
		public required Principal Id { get; set; }
	}

	public enum EffectivePrincipalType
	{
		Subnet,
		Canister
	}


	public class SubnetConfig
	{
		public bool? EnableDeterministicTimeSlicing { get; set; }
		public bool? EnableBenchmarkingInstructionLimits { get; set; }
		public required SubnetStateConfig State { get; set; }
	}

	public class SubnetStateConfig
	{
		public SubnetStateType Type { get; private set; }
		public string? Path { get; private set; }
		public Principal? SubnetId { get; private set; }

		private SubnetStateConfig(SubnetStateType type, string? path, Principal? subnetId)
		{
			this.Type = type;
			this.Path = path;
			this.SubnetId = subnetId;
		}

		public static SubnetStateConfig New()
		{
			return new SubnetStateConfig(SubnetStateType.New, null, null);
		}

		public static SubnetStateConfig FromPath(string path, Principal subnetId)
		{
			return new SubnetStateConfig(SubnetStateType.FromPath, path, subnetId);
		}
	}

	public enum SubnetStateType
	{
		New,
		FromPath
	}
}