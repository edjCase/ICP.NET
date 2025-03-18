using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid.Models;
using Microsoft.AspNetCore.Mvc;
using Sample.Shared.Governance.Models;

namespace Sample.RestAPI.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class ReplicaController : ControllerBase
	{
		public IAgent Agent { get; }
		public HttpClient HttpClient { get; }
		public ReplicaController(IAgent agent)
		{
			this.Agent = agent;
			this.HttpClient = new HttpClient
			{
				BaseAddress = new Uri("https://icp-api.io/")
			};
		}

		[Route("status")]
		[HttpGet]
		public async Task<IActionResult> GetRewards()
		{
			StatusResponse status = await this.Agent.GetReplicaStatusAsync();
			return this.Ok(status);
		}

		[Route("canisterState/{canisterIdText}")]
		[HttpGet]
		public async Task<IActionResult> GetA(string canisterIdText)
		{
			Principal canisterId = Principal.FromText(canisterIdText);
			var candidServicePath = StatePath.FromSegments("canister", canisterId.Raw, "metadata", "candid:service");
			var paths = new List<StatePath>
			{
				candidServicePath
			};
			ReadStateResponse response = await this.Agent.ReadStateAsync(canisterId, paths);
			return this.Ok(response);
		}
	}

}
