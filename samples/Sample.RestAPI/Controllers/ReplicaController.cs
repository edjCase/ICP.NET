using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid.Models;
using Microsoft.AspNetCore.Mvc;
using Sample.Shared.Governance;
using Sample.Shared.Governance.Models;
using System.Threading;

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

		[Route("api_boundary_nodes/{nodeId}/domain")]
		[HttpGet]
		public async Task<IActionResult> GetApiBoundaryNodeDomain(string nodeId)
		{
			HttpResponseMessage httpResponse = await this.HttpClient.GetAsync($"/api_boundary_nodes/{nodeId}/domain");
			return this.Ok(await httpResponse.Content.ReadAsStringAsync());
		}
	}
}
