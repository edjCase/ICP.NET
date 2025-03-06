using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Responses;
using Microsoft.AspNetCore.Mvc;

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
	}

}
