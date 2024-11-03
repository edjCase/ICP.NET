using System.Text;
using System.Text.Json;
using EdjCase.ICP.Candid.Models;
using System.Text.Json.Nodes;
using System.Net;
using System.Diagnostics;

namespace EdjCase.ICP.PocketIC.Client;

internal class PocketIcHttpClient : IPocketIcHttpClient
{
	private readonly HttpClient httpClient;
	private readonly string baseUrl;
	private const int POLLING_PERIOD_MS = 10;
	private readonly TimeSpan requestTimeout;

	public PocketIcHttpClient(
		HttpClient httpClient,
		string url,
		TimeSpan requestTimeout
	)
	{
		this.httpClient = httpClient;
		this.baseUrl = url;
		this.requestTimeout = requestTimeout;
	}

	public Uri GetServerUrl()
	{
		return new Uri(this.baseUrl);
	}

	public async Task<string> UploadBlobAsync(byte[] blob)
	{
		var content = new ByteArrayContent(blob);
		HttpResponseMessage response = await this.httpClient.PostAsync($"{this.baseUrl}/blobstore", content);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsStringAsync();
	}

	public async Task<byte[]> DownloadBlobAsync(string blobId)
	{
		HttpResponseMessage response = await this.httpClient.GetAsync($"{this.baseUrl}/blobstore/{blobId}");
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsByteArrayAsync();
	}

	public async Task<bool> VerifySignatureAsync(
		byte[] message,
		Principal publicKey,
		Principal rootPublicKey,
		byte[] signature
	)
	{
		// Doesn't use the ApiResponse<T> pattern
		var request = new JsonObject
		{
			["msg"] = JsonValue.Create(message),
			["pubkey"] = JsonValue.Create(publicKey.Raw),
			["root_pubkey"] = JsonValue.Create(rootPublicKey.Raw),
			["sig"] = JsonValue.Create(signature),
		};
		HttpResponseMessage response = await this.MakeHttpRequestAsync(HttpMethod.Post, "/verify_signature", request);
		Stream stream = await response.Content.ReadAsStreamAsync();
		JsonNode? node = await JsonNode.ParseAsync(stream);
		if (node == null)
		{
			throw new Exception("There was no json response from the server");
		}
		if (node["Err"] != null)
		{
			Console.WriteLine("Signature failed to verify: " + node["Err"]!.Deserialize<string>());
			return false;
		}

		return true;
	}

	public async Task<List<Instance>> GetInstancesAsync()
	{
		// Doesn't use the ApiResponse<T> pattern
		HttpResponseMessage response = await this.MakeHttpRequestAsync(HttpMethod.Get, "/instances");
		Stream stream = await response.Content.ReadAsStreamAsync();
		JsonNode? node = await JsonNode.ParseAsync(stream);
		if (node == null)
		{
			throw new Exception("There was no json response from the server");
		}
		return node
		!.AsArray()
		.Select((s, i) => new Instance { Id = i, Status = Enum.Parse<InstanceStatus>(s!.Deserialize<string>()!) }).ToList();
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
		// Doesn't use the ApiResponse<T> pattern
		HttpResponseMessage response = await this.MakeHttpRequestAsync(HttpMethod.Post, "/instances", request);
		Stream stream = await response.Content.ReadAsStreamAsync();
		JsonNode? node = await JsonNode.ParseAsync(stream);
		if (node == null)
		{
			throw new Exception("There was no json response from the server");
		}

		if (node["Error"] != null)
		{
			string message = node!["error"]!["message"]!.Deserialize<string>()!;
			throw new Exception($"Failed to create PocketIC instance: {message}");
		}
		JsonObject? created = node["Created"]?.AsObject();
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
		await this.DeleteAsync($"/instances/{id}");
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
		JsonNode? response = await this.GetJsonAsync($"/instances/{instanceId}/read/topology");
		if (response == null)
		{
			throw new Exception("There was no json response from the server");
		}
		return response
			.AsObject()
			?.Deserialize<Dictionary<string, JsonNode>>()
			?.Select(kv => MapSubnetTopology(kv.Key, kv.Value))
			?.ToList()
			?? [];
	}

	public async Task<ICTimestamp> GetTimeAsync(int instanceId)
	{
		JsonNode? response = await this.GetJsonAsync($"/instances/{instanceId}/read/get_time");
		if (response == null)
		{
			throw new Exception("There was no json response from the server");
		}
		return ICTimestamp.FromNanoSeconds(response!["nanos_since_epoch"].Deserialize<ulong>()!);
	}

	public async Task<CanisterHttpRequest> GetCanisterHttpAsync(int instanceId)
	{
		JsonNode? response = await this.GetJsonAsync($"/instances/{instanceId}/read/get_canister_http");

		if (response == null)
		{
			throw new Exception("There was no json response from the server");
		}
		return new CanisterHttpRequest
		{
			Body = response!["body"].Deserialize<byte[]>()!,
			Headers = response!["headers"]!.AsObject()!.Select(kv => new CanisterHttpHeader
			{
				Name = kv.Key,
				Value = kv.Value.Deserialize<string>()!
			}).ToList(),
			Url = response!["url"].Deserialize<string>()!,
			SubnetId = Principal.FromBytes(response!["subnet_id"].Deserialize<byte[]>()!),
			HttpMethod = Enum.Parse<CanisterHttpMethod>(response!["http_method"].Deserialize<string>()!),
			MaxResponseBytes = response!["max_response_bytes"].Deserialize<ulong?>()!,
			RequestId = response!["request_id"].Deserialize<ulong>()!
		};
	}

	public async Task<ulong> GetCyclesBalanceAsync(int instanceId, Principal canisterId)
	{
		var request = new JsonObject
		{
			["canister_id"] = Convert.ToBase64String(canisterId.Raw)
		};
		JsonNode? response = await this.PostJsonAsync($"/instances/{instanceId}/read/get_cycles", request);
		if (response == null)
		{
			throw new Exception("There was no json response from the server");
		}
		return response!["cycles"].Deserialize<ulong>()!;
	}

	public async Task<byte[]> GetStableMemoryAsync(int instanceId, Principal canisterId)
	{
		var request = new JsonObject
		{
			["canister_id"] = Convert.ToBase64String(canisterId.Raw)
		};
		JsonNode? response = await this.PostJsonAsync($"/instances/{instanceId}/read/get_stable_memory", request);
		if (response == null)
		{
			throw new Exception("There was no json response from the server");
		}
		return response!["blob"].Deserialize<byte[]>()!;
	}

	public async Task<Principal> GetSubnetIdForCanisterAsync(int instanceId, Principal canisterId)
	{
		var request = new JsonObject
		{
			["canister_id"] = Convert.ToBase64String(canisterId.Raw)
		};
		JsonNode? response = await this.PostJsonAsync($"/instances/{instanceId}/read/get_subnet", request);
		if (response == null)
		{
			throw new Exception("There was no json response from the server");
		}
		byte[] subnetId = response!["subnet_id"].Deserialize<byte[]>()!;
		return Principal.FromBytes(subnetId);
	}

	public async Task<Principal> GetPublicKeyForSubnetAsync(int instanceId, Principal subnetId)
	{
		var request = new JsonObject
		{
			["subnet_id"] = Convert.ToBase64String(subnetId.Raw)
		};
		JsonNode? response = await this.PostJsonAsync($"/instances/{instanceId}/read/pub_key", request);
		if (response == null)
		{
			throw new Exception("There was no json response from the server");
		}
		byte[] publicKey = response!.AsArray().Select(r => r.Deserialize<byte>()!).ToArray();
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
		JsonNode? response = await this.PostJsonAsync(route, options);
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
		await this.PostJsonAsync($"/instances/{instanceId}/update/await_ingress_message", request);
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
		await this.PostJsonAsync($"/instances/{instanceId}/update/set_time", request);
	}

	public async Task<ulong> AddCyclesAsync(int instanceId, Principal canisterId, ulong amount)
	{
		var request = new JsonObject
		{
			["canister_id"] = Convert.ToBase64String(canisterId.Raw),
			["amount"] = amount
		};
		JsonNode? response = await this.PostJsonAsync($"/instances/{instanceId}/update/add_cycles", request);
		if (response == null)
		{
			throw new Exception("There was no json response from the server");
		}
		return response["cycles"].Deserialize<ulong>()!;
	}

	public async Task SetStableMemoryAsync(int instanceId, Principal canisterId, byte[] memory)
	{
		string blobId = await this.UploadBlobAsync(memory);
		var request = new JsonObject
		{
			["canister_id"] = Convert.ToBase64String(canisterId.Raw),
			["blob_id"] = JsonValue.Create(Convert.FromHexString(blobId))
		};
		await this.PostJsonAsync($"/instances/{instanceId}/update/set_stable_memory", request);
	}

	public async Task TickAsync(int instanceId)
	{
		await this.PostJsonAsync($"/instances/{instanceId}/update/tick", null);
	}

	public async Task MockCanisterHttpResponseAsync(
		int instanceId,
		ulong requestId,
		Principal subnetId,
		CanisterHttpResponse response,
		List<CanisterHttpResponse> additionalResponses
	)
	{
		var request = new JsonObject
		{
			["request_id"] = JsonValue.Create(requestId),
			["subnet_id"] = JsonValue.Create(subnetId.Raw),
			["response"] = PocketIcHttpClient.SerializeCanisterHttpResponse(response),
			["additional_responses"] = JsonValue.Create(
				additionalResponses
				.Select(r => PocketIcHttpClient.SerializeCanisterHttpResponse(r))
				.ToArray()
			)
		};
		await this.PostJsonAsync($"/instances/{instanceId}/update/mock_canister_http", request);
	}

	public async Task<Uri> AutoProgressAsync(int instanceId, ulong? artificialDelayMs = null)
	{
		var request = new JsonObject();
		if (artificialDelayMs.HasValue)
		{
			request["artificial_delay_ms"] = JsonValue.Create(artificialDelayMs.Value);
		}
		JsonNode? response = await this.PostJsonAsync($"/instances/{instanceId}/auto_progress", request);
		if (response == null)
		{
			throw new Exception("There was no json response from the server");
		}
		return new Uri(response!["url"].Deserialize<string>()!);
	}

	public async Task StopProgressAsync(int instanceId)
	{
		await this.PostJsonAsync($"/instances/{instanceId}/stop_progress", null);
	}

	public async Task<Uri> StartHttpGatewayAsync(
		int instanceId,
		int? port = null,
		List<string>? domains = null,
		HttpsConfig? httpsConfig = null
	)
	{
		var request = new JsonObject
		{
			["forward_to"] = new JsonObject
			{
				["PocketIcInstance"] = JsonValue.Create(instanceId)
			}
		};
		if (port != null)
		{
			request["port"] = JsonValue.Create(port.Value);
		}
		if (domains != null)
		{
			request["domains"] = new JsonArray(domains.Select(d => JsonValue.Create(d)).ToArray());
		}
		if (httpsConfig != null)
		{
			request["https_config"] = new JsonObject
			{
				["cert_path"] = httpsConfig.CertPath,
				["key_path"] = httpsConfig.KeyPath
			};
		}
		HttpResponseMessage response = await this.MakeHttpRequestAsync(HttpMethod.Post, "/http_gateway", request);
		response.EnsureSuccessStatusCode();
		Stream stream = await response.Content.ReadAsStreamAsync();
		JsonNode? node = await JsonNode.ParseAsync(stream);
		if (node == null)
		{
			throw new Exception("There was no json response from the server");
		}
		if (node["Error"] != null)
		{
			string message = node!["Error"]!["message"]!.Deserialize<string>()!;
			throw new Exception($"Failed to start HTTP gateway: {message}");
		}


		int actualPort = node["Created"]!["port"].Deserialize<int>()!;
		string protocol = httpsConfig != null ? "https" : "http";
		string domain = domains?.Any() == true ? domains.First() : "localhost";
		string url = $"{protocol}://{domain}:{actualPort}/";
		return new Uri(url);
	}

	public async Task StopHttpGatewayAsync(int instanceId)
	{
		HttpResponseMessage response = await this.MakeHttpRequestAsync(HttpMethod.Post,
			$"/http_gateway/{instanceId}/stop"
		);
		response.EnsureSuccessStatusCode();
	}


	// =======================================

	private static JsonObject SerializeCanisterHttpResponse(CanisterHttpResponse response)
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

	private async Task<JsonNode?> DeleteAsync(string endpoint)
	{
		return await this.MakeJsonRequestAsync(HttpMethod.Delete, endpoint);
	}

	private async Task<JsonNode?> GetJsonAsync(string endpoint)
	{
		return await this.MakeJsonRequestAsync(HttpMethod.Get, endpoint);
	}

	private async Task<JsonNode?> PostJsonAsync(string endpoint, JsonObject? data = null)
	{
		return await this.MakeJsonRequestAsync(HttpMethod.Post, endpoint, data);
	}

	private async Task<JsonNode?> MakeJsonRequestAsync(
		HttpMethod method,
		string endpoint,
		JsonObject? data = null
	)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (true)
		{
			ApiResponse<JsonNode?> response = await this.MakeApiRequestAsync(method, endpoint, data);
			switch (response.Type)
			{
				case ApiResponseType.Error:
					throw new Exception(response.AsError());
				case ApiResponseType.Success:
					return response.AsSuccess();
				case ApiResponseType.Started:
					{
						(string stateLabel, string opId) = response.AsStartedOrBusy();
						return await this.WaitForRequestAsync(stateLabel, opId, stopwatch);
					}
				case ApiResponseType.Busy:
					{
						(string stateLabel, string opId) = response.AsStartedOrBusy();
						Console.WriteLine($"Instance is busy. state_label: {stateLabel}, op_id: {opId}");
						break;
					}
				default:
					throw new Exception("Unexpected response type: " + response.Type);
			}
			await Task.Delay(POLLING_PERIOD_MS);
		}
	}

	private async Task<JsonNode?> WaitForRequestAsync(
		string stateLabel,
		string opId,
		Stopwatch stopwatch
	)
	{
		while (true)
		{
			await Task.Delay(POLLING_PERIOD_MS);
			ApiResponse<JsonNode?> response = await this.MakeApiRequestAsync(HttpMethod.Get, $"/read_graph/{stateLabel}/{opId}");
			switch (response.Type)
			{
				case ApiResponseType.Error:
					Console.WriteLine($"Polling failure, trying again. Error: {response.AsError()}");
					break;
				case ApiResponseType.Success:
					return response.AsSuccess();
				case ApiResponseType.Started:
					Console.WriteLine($"Unexpected 'started' response while polling, trying again. state_label: {stateLabel}, op_id: {opId}");
					break;
				case ApiResponseType.Busy:
					Console.WriteLine($"Unexpected 'started' response while polling, trying again. state_label: {stateLabel}, op_id: {opId}");
					break;
				default:
					throw new Exception("Unexpected response type: " + response.Type);
			}
			if (this.requestTimeout > TimeSpan.Zero && stopwatch.Elapsed > this.requestTimeout)
			{
				throw new Exception("Request timed out while waiting for completion");
			}
		}
	}

	private async Task<HttpResponseMessage> MakeHttpRequestAsync(HttpMethod method, string endpoint, JsonObject? data = null)
	{
		HttpContent? content;
		switch (method.Method)
		{
			case "GET":
			case "DELETE":
				content = null;
				if (data != null)
				{
					throw new ArgumentException("GET and DELETE requests cannot have a body");
				}
				break;
			case "POST":
				var json = data?.ToJsonString() ?? "";
				content = new StringContent(json, Encoding.UTF8, "application/json");
				break;
			default:
				throw new Exception($"Unsupported HTTP method: {method}");
		}
		string url = $"{this.baseUrl}{endpoint}";
		var request = new HttpRequestMessage(method, url)
		{
			Content = content
		};
		return await this.httpClient.SendAsync(request);
	}

	private async Task<ApiResponse<JsonNode?>> MakeApiRequestAsync(
		HttpMethod method,
		string endpoint,
		JsonObject? data = null
	)
	{
		HttpResponseMessage response = await this.MakeHttpRequestAsync(method, endpoint, data);
		Stream stream = await response.Content.ReadAsStreamAsync();
		JsonNode? node = null;
		if (stream.Length > 0)
		{
			node = await JsonNode.ParseAsync(stream);
		}
		switch (response.StatusCode)
		{
			case HttpStatusCode.OK:
				{
					return new ApiResponse<JsonNode?>(ApiResponseType.Success, node);
				}
			case HttpStatusCode.Accepted:
			case HttpStatusCode.Conflict:
				{
					if (node == null)
					{
						throw new Exception("There was no json response from the server");
					}
					string stateLabel = node["state_label"].Deserialize<string>()!;
					string opId = node["op_id"].Deserialize<string>()!;
					ApiResponseType type = response.StatusCode == HttpStatusCode.Accepted
						? ApiResponseType.Started
						: ApiResponseType.Busy;
					return new ApiResponse<JsonNode?>(type, (stateLabel, opId));
				}
			default:
				{
					if (node == null)
					{
						throw new Exception("There was no json response from the server");
					}
					string message = node["message"].Deserialize<string>()!;
					return new ApiResponse<JsonNode?>(ApiResponseType.Error, message);
				}
		}
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

	internal class ApiResponse<T>
	{
		public ApiResponseType Type { get; }
		public object? Data { get; }

		public ApiResponse(ApiResponseType type, object? data)
		{
			this.Type = type;
			this.Data = data;
		}

		public T AsSuccess()
		{
			return (T)this.Data!;
		}

		public string AsError()
		{
			return (string)this.Data!;
		}

		public (string StateLabel, string OpId) AsStartedOrBusy()
		{
			return ((string, string))this.Data!;
		}
	}

	internal enum ApiResponseType
	{
		Error,
		Success,
		Started,
		Busy
	}
}
