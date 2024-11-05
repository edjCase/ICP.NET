using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid;
using System.Threading.Tasks;
using Sample.Shared.AddressBook;
using EdjCase.ICP.Agent.Responses;
using Dict = System.Collections.Generic.Dictionary<System.String, EdjCase.ICP.Candid.Models.UnboundedUInt>;

namespace Sample.Shared.AddressBook
{
	public class AddressBookApiClient
	{
		public IAgent Agent { get; }

		public Principal CanisterId { get; }

		public CandidConverter? Converter { get; }

		public AddressBookApiClient(IAgent agent, Principal canisterId, CandidConverter? converter = default)
		{
			this.Agent = agent;
			this.CanisterId = canisterId;
			this.Converter = converter;
		}

		public async Task _set_address(string name, Address addr)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(name, this.Converter), CandidTypedValue.FromObject(addr, this.Converter));
			await this.Agent.CallAsync(this.CanisterId, "set_address", arg);
		}

		public async Task<OptionalValue<Address>> _get_address(string name)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(name, this.Converter));
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_address", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<OptionalValue<Address>>(this.Converter);
		}

		public async Task<SelfRef> _get_self()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_self", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<SelfRef>(this.Converter);
		}

		public async Task<Dict> _get_dict()
		{
			CandidArg arg = CandidArg.FromCandid();
			QueryResponse response = await this.Agent.QueryAsync(this.CanisterId, "get_dict", arg);
			CandidArg reply = response.ThrowOrGetReply();
			return reply.ToObjects<Dict>(this.Converter);
		}
	}
}