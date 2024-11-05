using EdjCase.ICP.Candid.Models;
using Microsoft.AspNetCore.Mvc;
using Sample.Shared.Governance;
using Sample.Shared.Governance.Models;

namespace Sample.RestAPI.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class GovernanceController : ControllerBase
	{
		public GovernanceApiClient Client { get; }
		public GovernanceController(GovernanceApiClient client)
		{
			this.Client = client;
		}

		[Route("proposals/{id}")]
		[HttpGet]
		public async Task<IActionResult> GetProposalInfo(ulong id)
		{
			OptionalValue<ProposalInfo> info = await this.Client.GetProposalInfo(id);
			return this.Ok(info);
		}

		[Route("rewards")]
		[HttpGet]
		public async Task<IActionResult> GetRewards()
		{
			Result4 rewards = await this.Client.GetMonthlyNodeProviderRewards();
			return this.Ok(rewards);
		}
	}
}