using System.Text;
using System.Text.Json;
using EdjCase.ICP.Candid.Models;
using System.Text.Json.Nodes;

namespace EdjCase.ICP.PocketIC.Client;

public class PocketIcHttpClient : IPocketIcHttpClient
{
	private readonly HttpClient httpClient;
	private readonly string baseUrl;

	public PocketIcHttpClient(
		HttpClient httpClient,
		string url
	)
	{
		this.httpClient = httpClient;
		this.baseUrl = url;
	}

	public async Task<JsonNode?> GetStatusAsync()
	{
		return await this.GetAsync("/status");
	}

	public async Task<byte[]> UploadBlobAsync(byte[] blob)
	{
		var content = new ByteArrayContent(blob);
		HttpResponseMessage response = await this.httpClient.PostAsync($"{this.baseUrl}/blobstore", content);
		response.EnsureSuccessStatusCode();
		var stream = await response.Content.ReadAsStreamAsync();
		JsonNode? json = await JsonNode.ParseAsync(stream)!;
		return json!["blob"].Deserialize<byte[]>()!;
	}

	public async Task<byte[]> DownloadBlobAsync(byte[] blobId)
	{
		HttpResponseMessage response = await this.httpClient.GetAsync($"{this.baseUrl}/blobstore/{Convert.ToBase64String(blobId)}");
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsByteArrayAsync();
	}

	public async Task<JsonNode?> VerifySignatureAsync(
		byte[] message,
		Principal publicKey,
		Principal rootPublicKey,
		byte[] signature
	)
	{
		var request = new JsonObject
		{
			["msg"] = JsonValue.Create(message),
			["pubkey"] = JsonValue.Create(publicKey.Raw),
			["root_pubkey"] = JsonValue.Create(rootPublicKey.Raw),
			["sig"] = JsonValue.Create(signature),
		};
		return await this.PostAsync("/verify_signature", request);
	}

	public async Task<JsonNode?> ReadGraphAsync(string stateLabel, string opId)
	{
		return await this.GetAsync($"/read_graph/{stateLabel}/{opId}");
	}

	public async Task<List<string>> GetInstanceIdsAsync()
	{
		JsonNode? response = await this.GetAsync("/instances");
		return response!.AsArray().Select(i => i!.Deserialize<string>()!).ToList();
	}

	public async Task<(int Id, List<SubnetTopology> Topology)> CreateInstanceAsync(
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
		// Default to a single application subnet
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

		JsonNode? response = await this.PostAsync("/instances", request);
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

		List<SubnetTopology> topology = created["topology"]
			?.Deserialize<Dictionary<string, JsonNode>>()
			?.Select(kv => MapSubnetTopology(kv.Key, kv.Value))
			?.ToList()
			?? [];
		return (instanceId, topology);
	}

	public async Task DeleteInstanceAsync(int id)
	{
		try
		{
			HttpResponseMessage message = await this.httpClient.DeleteAsync($"{this.baseUrl}/instances/{id}");
			message.EnsureSuccessStatusCode();
		}
		catch (Exception e)
		{
			throw new Exception("Failed to delete PocketIC instance", e);
		}
	}

	public async Task<CandidArg> QueryCallAsync(
		int instanceId,
		Principal sender,
		Principal canisterId,
		string method,
		CandidArg request,
		EffectivePrincipal? effectivePrincipal = null)
	{
		return await this.ProcessIngressMessageInternalAsync(
			$"/instances/{instanceId}/read/query",
			sender,
			canisterId,
			method,
			request,
			effectivePrincipal
		);
	}

	public async Task<List<SubnetTopology>> GetTopologyAsync(int instanceId)
	{
		JsonNode? node = await this.GetAsync($"/instances/{instanceId}/read/topology");
		return node!
			.AsObject()
			?.Deserialize<Dictionary<string, JsonNode>>()
			?.Select(kv => MapSubnetTopology(kv.Key, kv.Value))
			?.ToList()
			?? [];
	}

	public async Task<ICTimestamp> GetTimeAsync(int instanceId)
	{
		JsonNode? response = await this.GetAsync($"/instances/{instanceId}/read/get_time");
		return ICTimestamp.FromNanoSeconds(response!["nanos_since_epoch"].Deserialize<ulong>()!);
	}

	public async Task<JsonNode?> GetCanisterHttpAsync(int instanceId)
	{
		JsonNode? response = await this.GetAsync($"/instances/{instanceId}/read/get_canister_http");
		return response;
	}

	public async Task<ulong> GetCyclesBalanceAsync(int instanceId, Principal canisterId)
	{
		var request = new JsonObject
		{
			["canister_id"] = Convert.ToBase64String(canisterId.Raw)
		};
		JsonNode? response = await this.PostAsync($"/instances/{instanceId}/read/get_cycles", request);
		return response!["cycles"].Deserialize<ulong>()!;
	}

	public async Task<byte[]> GetStableMemoryAsync(int instanceId, Principal canisterId)
	{
		var request = new JsonObject
		{
			["canister_id"] = Convert.ToBase64String(canisterId.Raw)
		};
		JsonNode? response = await this.PostAsync($"/instances/{instanceId}/read/get_stable_memory", request);
		return response!["blob_id"].Deserialize<byte[]>()!;
	}

	public async Task<Principal> GetSubnetIdForCanisterAsync(int instanceId, Principal canisterId)
	{
		var request = new JsonObject
		{
			["canister_id"] = Convert.ToBase64String(canisterId.Raw)
		};
		JsonNode? response = await this.PostAsync($"/instances/{instanceId}/read/get_subnet", request);
		byte[] subnetId = response!["subnet_id"].Deserialize<byte[]>()!;
		return Principal.FromBytes(subnetId);
	}

	public async Task<Principal> GetPublicKeyForSubnetAsync(int instanceId, Principal subnetId)
	{
		var request = new JsonObject
		{
			["subnet_id"] = Convert.ToBase64String(subnetId.Raw)
		};
		JsonNode? response = await this.PostAsync($"/instances/{instanceId}/read/pub_key", request);
		byte[] publicKey = response!["public_key"].Deserialize<byte[]>()!;
		return Principal.FromBytes(publicKey);
	}

	public async Task<CandidArg> SubmitIngressMessageAsync(
		int instanceId,
		Principal sender,
		Principal canisterId,
		string method,
		CandidArg request,
		EffectivePrincipal? effectivePrincipal = null)
	{
		return await this.ProcessIngressMessageInternalAsync(
			$"/instances/{instanceId}/update/submit_ingress_message",
			sender,
			canisterId,
			method,
			request,
			effectivePrincipal
		);
	}

	public async Task<CandidArg> ExecuteIngressMessageAsync(
		int instanceId,
		Principal sender,
		Principal canisterId,
		string method,
		CandidArg request,
		EffectivePrincipal? effectivePrincipal = null)
	{
		return await this.ProcessIngressMessageInternalAsync(
			$"/instances/{instanceId}/update/execute_ingress_message",
			sender,
			canisterId,
			method,
			request,
			effectivePrincipal
		);
	}


	private async Task<CandidArg> ProcessIngressMessageInternalAsync(
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

	public async Task AwaitIngressMessageAsync(int instanceId, byte[] messageId, Principal? effectivePrincipal = null)
	{
		var request = new JsonObject
		{
			["message_id"] = Convert.ToBase64String(messageId),
			["effective_principal"] = effectivePrincipal == null ? null : Convert.ToBase64String(effectivePrincipal.Raw)
		};
		JsonNode? response = await this.PostAsync($"/instances/{instanceId}/update/await_ingress_message", request);
		// TODO
	}

	public async Task SetTimeAsync(int instanceId, ICTimestamp timestamp)
	{
		if (!timestamp.NanoSeconds.TryToUInt64(out ulong nanosSinceEpoch))
		{
			throw new ArgumentException("Nanoseconds is too large to convert to ulong");
		}
		var request = new JsonObject
		{
			["nanos_since_epoch"] = nanosSinceEpoch
		};
		await this.PostAsync($"/instances/{instanceId}/update/set_time", request);
	}

	public async Task<ulong> AddCyclesAsync(int instanceId, Principal canisterId, ulong amount)
	{
		var request = new JsonObject
		{
			["canister_id"] = Convert.ToBase64String(canisterId.Raw),
			["amount"] = amount
		};
		JsonNode? response = await this.PostAsync($"/instances/{instanceId}/update/add_cycles", request);
		return response!["cycles"].Deserialize<ulong>()!;
	}

	public async Task SetStableMemoryAsync(int instanceId, Principal canisterId, byte[] memory)
	{
		byte[] blobId = await this.UploadBlobAsync(memory);
		var request = new JsonObject
		{
			["canister_id"] = Convert.ToBase64String(canisterId.Raw),
			["blob_id"] = JsonValue.Create(blobId)
		};
		await this.PostAsync($"/instances/{instanceId}/update/set_stable_memory", request);
	}

	public async Task TickAsync(int instanceId)
	{
		await this.PostAsync($"/instances/{instanceId}/update/tick", null);
	}

	// TODO new routes?

	// public async Task<JsonNode?> MockCanisterHttpAsync(
	// 	int instanceId,
	// 	ulong requestId,
	// 	Principal subnetId,
	// 	CanisterHttpResponse response,
	// 	List<CanisterHttpResponse> additionalResponses
	// )
	// {
	// 	var request = new JsonObject
	// 	{
	// 		["request_id"] = JsonValue.Create(requestId),
	// 		["subnet_id"] = JsonValue.Create(subnetId.Raw),
	// 		["response"] = this.SerializeCanisterHttpResponse(response),
	// 		["additional_responses"] = JsonValue.Create(
	// 			additionalResponses
	// 			.Select(r => this.SerializeCanisterHttpResponse(r))
	// 			.ToArray()
	// 		)
	// 	};
	// 	return await this.PostAsync($"/instances/{instanceId}/update/mock_canister_http", request);
	// }

	// public async Task<JsonNode?> GetCanisterStatusAsync(int instanceId)
	// {
	// 	JsonNode? response = await this.GetAsync(
	// 		$"/instances/{instanceId}/api/v2/status"
	// 	);
	// 	return response;
	// }

	// public async Task<JsonNode?> CallCanisterAsync(int instanceId, string canisterId, byte[] content)
	// {
	// 	JsonObject request = new()
	// 	{
	// 		["content"] = JsonValue.Create(content)
	// 	};
	// 	JsonNode? response = await this.PostAsync(
	// 		$"/instances/{instanceId}/api/v2/canister/{canisterId}/call",
	// 		request
	// 	);
	// 	return response;
	// }

	// public async Task<JsonNode?> QueryCanisterAsync(int instanceId, string canisterId, byte[] content)
	// {
	// 	JsonObject request = new()
	// 	{
	// 		["content"] = JsonValue.Create(content)
	// 	};
	// 	JsonNode? response = await this.PostAsync(
	// 		$"/instances/{instanceId}/api/v2/canister/{canisterId}/query",
	// 		request
	// 	);
	// 	return response;
	// }

	// public async Task<JsonNode?> ReadCanisterStateAsync(int instanceId, string canisterId, byte[] content)
	// {
	// 	JsonObject request = new()
	// 	{
	// 		["content"] = JsonValue.Create(content)
	// 	};
	// 	JsonNode? response = await this.PostAsync(
	// 		$"/instances/{instanceId}/api/v2/canister/{canisterId}/read_state",
	// 		request
	// 	);
	// 	return response;
	// }

	// public async Task<JsonNode?> ReadSubnetStateAsync(int instanceId, string subnetId, byte[] content)
	// {
	// 	JsonObject request = new()
	// 	{
	// 		["content"] = JsonValue.Create(content)
	// 	};
	// 	JsonNode? response = await this.PostAsync(
	// 		$"/instances/{instanceId}/api/v2/subnet/{subnetId}/read_state",
	// 		request
	// 	);
	// 	return response;
	// }

	// public async Task<JsonNode?> CallCanisterV3Async(int instanceId, string canisterId, byte[] content)
	// {
	// 	JsonObject request = new JsonObject
	// 	{
	// 		["content"] = JsonValue.Create(content)
	// 	};
	// 	JsonNode? response = await this.PostAsync(
	// 		$"/instances/{instanceId}/api/v3/canister/{canisterId}/call",
	// 		request
	// 	);
	// 	return response;
	// }

	// public async Task<string> GetDashboardAsync(int instanceId)
	// {
	// 	JsonNode? response = await this.GetAsync($"/instances/{instanceId}/_/dashboard");
	// 	return response!.Deserialize<string>()!;
	// }

	// public async Task<JsonNode?> AutoProgressAsync(int instanceId, ulong? artificialDelayMs = null)
	// {
	// 	var request = new JsonObject();
	// 	if (artificialDelayMs.HasValue)
	// 	{
	// 		request["artificial_delay_ms"] = JsonValue.Create(artificialDelayMs.Value);
	// 	}
	// 	return await this.PostAsync($"/instances/{instanceId}/auto_progress", request);
	// }

	// public async Task StopProgressAsync(int instanceId)
	// {
	// 	await this.PostAsync($"/instances/{instanceId}/stop_progress", null);
	// }

	// public async Task<List<HttpGatewayDetails>> GetHttpGatewayAsync()
	// {
	// 	JsonNode? response = await this.GetAsync("/http_gateway/");
	// 	return response
	// 		?.AsArray()
	// 		.Select(node => new HttpGatewayDetails
	// 		{
	// 			InstanceId = node!["instance_id"]!.Deserialize<uint>()!,
	// 			Port = node!["port"]!.Deserialize<ushort>()!,
	// 			ForwardTo = node!["forward_to"]!.AsObject(),
	// 			Domains = node!["domains"]?.Deserialize<List<string>>(),
	// 			HttpsConfig = node!["https_config"] == null ? null : new HttpsConfig
	// 			{
	// 				CertPath = node!["https_config"]!["cert_path"]!.Deserialize<string>()!,
	// 				KeyPath = node!["https_config"]!["key_path"]!.Deserialize<string>()!
	// 			}
	// 		})
	// 		.ToList() ?? new List<HttpGatewayDetails>();
	// }

	// public async Task<JsonNode?> UpdateHttpGatewayAsync(HttpGatewayConfig config)
	// {
	// 	var requestBody = new JsonObject
	// 	{
	// 		["forward_to"] = config.ForwardTo,
	// 		["domains"] = config.Domains == null ? null : JsonValue.Create(config.Domains),
	// 		["port"] = config.Port == null ? null : JsonValue.Create(config.Port),
	// 		["ip_addr"] = config.IpAddr == null ? null : JsonValue.Create(config.IpAddr),
	// 		["https_config"] = config.HttpsConfig == null ? null : new JsonObject
	// 		{
	// 			["cert_path"] = config.HttpsConfig.CertPath,
	// 			["key_path"] = config.HttpsConfig.KeyPath
	// 		}
	// 	};

	// 	return await this.PostAsync("/http_gateway/", requestBody);
	// }

	// public async Task StopHttpGatewayAsync(uint instanceId)
	// {
	// 	JsonNode request = new JsonObject();
	// 	JsonNode? response = await this.PostAsync(
	// 		$"/http_gateway/{instanceId}/stop",
	// 		request
	// 	);
	// }


	// =======================================

	private JsonNode SerializeCanisterHttpResponse(CanisterHttpResponse response)
	{
		if (response is CanisterHttpReply reply)
		{
			return new JsonObject
			{
				["CanisterHttpReply"] = new JsonObject
				{
					["status"] = JsonValue.Create(reply.Status),
					["headers"] = JsonValue.Create(reply.Headers.Select(h => new { name = h.Name, value = h.Value }).ToArray()),
					["body"] = JsonValue.Create(reply.Body)
				}
			};
		}
		else if (response is CanisterHttpReject reject)
		{
			return new JsonObject
			{
				["CanisterHttpReject"] = new JsonObject
				{
					["reject_code"] = JsonValue.Create(reject.RejectCode),
					["message"] = reject.Message
				}
			};
		}
		throw new ArgumentException("Unknown CanisterHttpResponse type");
	}



	private async Task<JsonNode?> GetAsync(string endpoint)
	{
		HttpResponseMessage response = await this.httpClient.GetAsync($"{this.baseUrl}{endpoint}");
		response.EnsureSuccessStatusCode();
		Stream stream = await response.Content.ReadAsStreamAsync();
		return await JsonNode.ParseAsync(stream)!;
	}

	private async Task<JsonNode?> PostAsync(string endpoint, JsonObject? data)
	{
		string url = $"{this.baseUrl}{endpoint}";
		return await PocketIcHttpClient.PostAsync(this.httpClient, url, data);
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



	private static SubnetTopology MapSubnetTopology(string subnetId, JsonNode value)
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
}
