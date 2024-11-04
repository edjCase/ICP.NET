using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Standards.ICRC1.Models;
using EdjCase.ICP.Candid.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EdjCase.ICP.Agent.Standards.ICRC1
{
	/// <summary>
	/// A pre-generated client for the ICRC1 standard
	/// </summary>
	public class ICRC1Client
	{
		/// <summary>
		/// Agent to use to make requests to the IC
		/// </summary>
		public IAgent Agent { get; }

		/// <summary>
		/// The id of the canister to make requests to
		/// </summary>
		public Principal CanisterId { get; }

		/// <summary>
		/// Primary constructor
		/// </summary>
		/// <param name="agent">Agent to use to make requests to the IC</param>
		/// <param name="canisterId">The id of the canister to make requests to</param>
		public ICRC1Client(IAgent agent, Principal canisterId)
		{
			this.Agent = agent;
			this.CanisterId = canisterId;
		}

		/// <summary>
		/// Returns the list of metadata entries for this ledger. See the "Metadata" section below.
		/// </summary>
		/// <returns></returns>
		public async Task<List<MetaData>> MetaData()
		{
			CandidArg arg = CandidArg.FromCandid();
			Responses.QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "icrc1_meta_data", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<List<MetaData>>();
		}

		/// <summary>
		/// Returns the name of the token (e.g., MyToken).
		/// </summary>
		/// <returns>The name of the token (e.g., MyToken).</returns>
		public async Task<string> Name()
		{
			CandidArg arg = CandidArg.FromCandid();
			Responses.QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "icrc1_name", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<string>();
		}

		/// <summary>
		/// Returns the symbol of the token (e.g., ICP).
		/// </summary>
		/// <returns>The symbol of the token (e.g., ICP).</returns>
		public async Task<string> Symbol()
		{
			CandidArg arg = CandidArg.FromCandid();
			Responses.QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "icrc1_symbol", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<string>();
		}

		/// <summary>
		/// Returns the number of decimals the token uses (e.g., 8 means to divide the token amount by 100000000 to get its user representation).
		/// </summary>
		/// <returns>The number of decimals the token uses (e.g., 8 means to divide the token amount by 100000000 to get its user representation).</returns>
		public async Task<int> Decimals()
		{
			CandidArg arg = CandidArg.FromCandid();
			Responses.QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "icrc1_decimals", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<byte>();
		}

		/// <summary>
		/// Returns the default transfer fee.
		/// </summary>
		/// <returns>The default transfer fee.</returns>
		public async Task<UnboundedUInt> Fee()
		{
			CandidArg arg = CandidArg.FromCandid();
			Responses.QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "icrc1_fee", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<UnboundedUInt>();
		}

		/// <summary>
		/// Returns the total number of tokens on all accounts except for the minting account.
		/// </summary>
		/// <returns>The total number of tokens on all accounts except for the minting account.</returns>
		public async Task<UnboundedUInt> TotalSupply()
		{
			CandidArg arg = CandidArg.FromCandid();
			Responses.QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "icrc1_total_supply", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<UnboundedUInt>();
		}

		/// <summary>
		/// Returns the minting account if this ledger supports minting and burning tokens.
		/// </summary>
		/// <returns>The minting account if this ledger supports minting and burning tokens.</returns>
		public async Task<OptionalValue<Account>> MintingAccount()
		{
			CandidArg arg = CandidArg.FromCandid();
			Responses.QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "icrc1_minting_account", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<OptionalValue<Account>>();
		}

		/// <summary>
		/// Returns the balance of the account given as an argument.
		/// </summary>
		/// <param name="account">Account to check balance for</param>
		/// <returns>The balance of the account given as an argument.</returns>
		public async Task<UnboundedUInt> BalanceOf(Account account)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(account));
			Responses.QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "icrc1_balance_of", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<UnboundedUInt>();
		}

		/// <summary>
		/// Transfers amount of tokens from account record { of = caller; subaccount = from_subaccount } to the to account. The caller pays fee tokens for the transfer.
		/// </summary>
		/// <param name="args">Arguments for the transfer</param>
		/// <returns>The result information from the transfer</returns>
		public async Task<TransferResult> Transfer(TransferArgs args)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(args));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "icrc1_transfer", arg);
			return reply.ToObjects<TransferResult>();
		}

		/// <summary>
		/// Returns the list of standards this ledger implements
		/// </summary>
		/// <returns>The list of standards this ledger implements</returns>
		public async Task<List<SupportedStandard>> SupportedStandards()
		{
			CandidArg arg = CandidArg.FromCandid();
			Responses.QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "icrc1_supported_standards", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<List<SupportedStandard>>();
		}
	}
}