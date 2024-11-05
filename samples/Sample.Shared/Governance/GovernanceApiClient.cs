using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid;
using System.Threading.Tasks;
using Sample.Shared.Governance;
using System.Collections.Generic;
using EdjCase.ICP.Agent.Responses;

namespace Sample.Shared.Governance
{
	public class GovernanceApiClient
	{
		public IAgent Agent { get; }

		public Principal CanisterId { get; }

		public CandidConverter? Converter { get; }

		public GovernanceApiClient(IAgent agent, Principal canisterId, CandidConverter? converter = default)
		{
			this.Agent = agent;
			this.CanisterId = canisterId;
			this.Converter = converter;
		}

		public async Task<Models.Result> ClaimGtcNeurons(Principal arg0, List<Models.NeuronId> arg1)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter), CandidTypedValue.FromObject(arg1, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "claim_gtc_neurons", arg);
			return reply.ToObjects<Models.Result>(this.Converter);
		}

		public async Task<Models.ClaimOrRefreshNeuronFromAccountResponse> ClaimOrRefreshNeuronFromAccount(Models.ClaimOrRefreshNeuronFromAccount arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "claim_or_refresh_neuron_from_account", arg);
			return reply.ToObjects<Models.ClaimOrRefreshNeuronFromAccountResponse>(this.Converter);
		}

		public async Task<string> GetBuildMetadata()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_build_metadata", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<string>(this.Converter);
		}

		public async Task<Models.Result2> GetFullNeuron(ulong arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_full_neuron", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.Result2>(this.Converter);
		}

		public async Task<Models.Result2> GetFullNeuronByIdOrSubaccount(Models.NeuronIdOrSubaccount arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_full_neuron_by_id_or_subaccount", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.Result2>(this.Converter);
		}

		public async Task<Models.RewardEvent> GetLatestRewardEvent()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_latest_reward_event", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.RewardEvent>(this.Converter);
		}

		public async Task<Models.Result3> GetMetrics()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_metrics", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.Result3>(this.Converter);
		}

		public async Task<Models.Result4> GetMonthlyNodeProviderRewards()
		{
			CandidArg arg = CandidArg.FromCandid();
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "get_monthly_node_provider_rewards", arg);
			return reply.ToObjects<Models.Result4>(this.Converter);
		}

		public async Task<OptionalValue<Models.MostRecentMonthlyNodeProviderRewards>> GetMostRecentMonthlyNodeProviderRewards()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_most_recent_monthly_node_provider_rewards", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<OptionalValue<Models.MostRecentMonthlyNodeProviderRewards>>(this.Converter);
		}

		public async Task<Models.NetworkEconomics> GetNetworkEconomicsParameters()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_network_economics_parameters", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.NetworkEconomics>(this.Converter);
		}

		public async Task<List<ulong>> GetNeuronIds()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_neuron_ids", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<List<ulong>>(this.Converter);
		}

		public async Task<Models.Result5> GetNeuronInfo(ulong arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_neuron_info", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.Result5>(this.Converter);
		}

		public async Task<Models.Result5> GetNeuronInfoByIdOrSubaccount(Models.NeuronIdOrSubaccount arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_neuron_info_by_id_or_subaccount", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.Result5>(this.Converter);
		}

		public async Task<Models.Result6> GetNodeProviderByCaller(NullValue arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_node_provider_by_caller", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.Result6>(this.Converter);
		}

		public async Task<List<Models.ProposalInfo>> GetPendingProposals()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_pending_proposals", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<List<Models.ProposalInfo>>(this.Converter);
		}

		public async Task<OptionalValue<Models.ProposalInfo>> GetProposalInfo(ulong arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_proposal_info", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<OptionalValue<Models.ProposalInfo>>(this.Converter);
		}

		public async Task<Models.ListKnownNeuronsResponse> ListKnownNeurons()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "list_known_neurons", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.ListKnownNeuronsResponse>(this.Converter);
		}

		public async Task<Models.ListNeuronsResponse> ListNeurons(Models.ListNeurons arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "list_neurons", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.ListNeuronsResponse>(this.Converter);
		}

		public async Task<Models.ListNodeProvidersResponse> ListNodeProviders()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "list_node_providers", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.ListNodeProvidersResponse>(this.Converter);
		}

		public async Task<Models.ListProposalInfoResponse> ListProposals(Models.ListProposalInfo arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "list_proposals", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Models.ListProposalInfoResponse>(this.Converter);
		}

		public async Task<Models.ManageNeuronResponse> ManageNeuron(Models.ManageNeuron arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "manage_neuron", arg);
			return reply.ToObjects<Models.ManageNeuronResponse>(this.Converter);
		}

		public async Task<Models.Result> SettleCommunityFundParticipation(Models.SettleCommunityFundParticipation arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "settle_community_fund_participation", arg);
			return reply.ToObjects<Models.Result>(this.Converter);
		}

		public async Task<Models.ManageNeuronResponse> SimulateManageNeuron(Models.ManageNeuron arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "simulate_manage_neuron", arg);
			return reply.ToObjects<Models.ManageNeuronResponse>(this.Converter);
		}

		public async Task<Models.Result> TransferGtcNeuron(Models.NeuronId arg0, Models.NeuronId arg1)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter), CandidTypedValue.FromObject(arg1, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "transfer_gtc_neuron", arg);
			return reply.ToObjects<Models.Result>(this.Converter);
		}

		public async Task<Models.Result> UpdateNodeProvider(Models.UpdateNodeProvider arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "update_node_provider", arg);
			return reply.ToObjects<Models.Result>(this.Converter);
		}
	}
}