using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid;
using System.Threading.Tasks;
using Sample.Shared.Dex;
using System.Collections.Generic;
using EdjCase.ICP.Agent.Responses;
using Token = EdjCase.ICP.Candid.Models.Principal;
using OrderId = System.UInt32;

namespace Sample.Shared.Dex
{
	public class DexApiClient
	{
		public IAgent Agent { get; }

		public Principal CanisterId { get; }

		public CandidConverter? Converter { get; }

		public DexApiClient(IAgent agent, Principal canisterId, CandidConverter? converter = default)
		{
			this.Agent = agent;
			this.CanisterId = canisterId;
			this.Converter = converter;
		}

		public async Task<Models.CancelOrderReceipt> CancelOrder(OrderId arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "cancelOrder", arg);
			return reply.ToObjects<Models.CancelOrderReceipt>(this.Converter);
		}

		public async Task Clear()
		{
			CandidArg arg = CandidArg.FromCandid();
			await this.Agent.CallAsync(this.CanisterId, "clear", arg);
		}

		public async Task Credit(Principal arg0, Token arg1, UnboundedUInt arg2)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter), CandidTypedValue.FromObject(arg1, this.Converter), CandidTypedValue.FromObject(arg2, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "credit", arg);
		}

		public async Task<Models.DepositReceipt> Deposit(Token arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "deposit", arg);
			return reply.ToObjects<Models.DepositReceipt>(this.Converter);
		}

		public async Task<List<Models.Balance>> GetAllBalances()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "getAllBalances", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<List<Models.Balance>>(this.Converter);
		}

		public async Task<UnboundedUInt> GetBalance(Token arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "getBalance", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<UnboundedUInt>(this.Converter);
		}

		public async Task<List<Models.Balance>> GetBalances()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "getBalances", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<List<Models.Balance>>(this.Converter);
		}

		public async Task<List<byte>> GetDepositAddress()
		{
			CandidArg arg = CandidArg.FromCandid();
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "getDepositAddress", arg);
			return reply.ToObjects<List<byte>>(this.Converter);
		}

		public async Task<OptionalValue<Models.Order>> GetOrder(OrderId arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "getOrder", arg);
			return reply.ToObjects<OptionalValue<Models.Order>>(this.Converter);
		}

		public async Task<List<Models.Order>> GetOrders()
		{
			CandidArg arg = CandidArg.FromCandid();
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "getOrders", arg);
			return reply.ToObjects<List<Models.Order>>(this.Converter);
		}

		public async Task<string> GetSymbol(Token arg0)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "getSymbol", arg);
			return reply.ToObjects<string>(this.Converter);
		}

		public async Task<Models.OrderPlacementReceipt> PlaceOrder(Token arg0, UnboundedUInt arg1, Token arg2, UnboundedUInt arg3)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter), CandidTypedValue.FromObject(arg1, this.Converter), CandidTypedValue.FromObject(arg2, this.Converter), CandidTypedValue.FromObject(arg3, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "placeOrder", arg);
			return reply.ToObjects<Models.OrderPlacementReceipt>(this.Converter);
		}

		public async Task<Principal> Whoami()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "whoami", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Principal>(this.Converter);
		}

		public async Task<Models.WithdrawReceipt> Withdraw(Token arg0, UnboundedUInt arg1, Principal arg2)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(arg0, this.Converter), CandidTypedValue.FromObject(arg1, this.Converter), CandidTypedValue.FromObject(arg2, this.Converter));
			CandidArg reply = await this.Agent.CallAsync(this.CanisterId, "withdraw", arg);
			return reply.ToObjects<Models.WithdrawReceipt>(this.Converter);
		}
	}
}