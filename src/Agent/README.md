# ICP.NET Agent

- Library to communicate to and from the Internet Computer
- PreGenerated ICRC1 Client
- Nuget: [`EdjCase.ICP.Agent`](https://www.nuget.org/packages/EdjCase.ICP.Agent)

## Usage (Manual)

- Dont define any types and use CandidValue and CandidType
- Call functions using Candid objects

```cs
// Create http agent with anonymous identity
IAgent agent = new HttpAgent();

// Create Candid arg to send in request
ulong proposalId = 1234;
CandidArg arg = CandidArg.FromCandid(
	CandidTypedValue.Nat64(proposalId) // Candid type with no conversion
);

// Make request to IC
string method = "get_proposal_info";
Principal governanceCanisterId = Principal.FromText("rrkah-fqaaa-aaaaa-aaaaq-cai");
QueryResponse response = await agent.QueryAsync(governanceCanisterId, method, arg);

CandidArg reply = response.ThrowOrGetReply();
// Convert to custom class
OptionalValue<ProposalInfo> info = reply.Arg.ToObjects<OptionalValue<ProposalInfo>>();
```

## Usage (Self Defined Types)

- Declare types of api models
- Call functions and use custom object converters

```cs
// Create http agent with anonymous identity
IAgent agent = new HttpAgent();

// Create Candid arg to send in request
ulong proposalId = 1234;
CandidArg arg = CandidArg.FromCandid(
    CandidTypedValue.FromObject(proposalId) // Conversion can be C# or custom types
);

// Make request to IC
string method = "get_proposal_info";
Principal governanceCanisterId = Principal.FromText("rrkah-fqaaa-aaaaa-aaaaq-cai");
QueryResponse response = await agent.QueryAsync(governanceCanisterId, method, arg);

CandidArg reply = response.ThrowOrGetReply();
// Convert to custom class
OptionalValue<ProposalInfo> info = reply.Arg.ToObjects<OptionalValue<ProposalInfo>>(); // Conversion to custom or C# types
```

## Usage (w/ Client Generator)

- Run Client Generator on `*.did` file (see Client Generator below)
- Use generated client and models to call function

```cs
// Create http agent with anonymous identity
IAgent agent = new HttpAgent();

// Create new instance of client generated by `Client Generator` (this is using Governance.did for the NNS)
var client = new GovernanceApiClient(agent, Principal.FromText("rrkah-fqaaa-aaaaa-aaaaq-cai"));

// Make request
OptionalValue<ProposalInfo> info = await client.GetProposalInfoAsync(62143);
```

## Using the ICRC1 PreGenerated Client

Instantiate an ICRC1Client by passing the HttpAgent instance and the canister ID of the ICRC1 canister as parameters:

```cs
IAgent agent = new HttpAgent(identity);
Principal canisterId = Principal.FromText("<canister_id>");
ICRC1Client client = new ICRC1Client(agent, canisterId);
```

Use the methods of the ICRC1Client to communicate with the ICRC1 canister:

```cs
// Get the name of the token
string name = await client.Name();

// Get the balance of a specific account
Account account = new Account
{
    Id = Principal.FromText("<account_id>")
};
UnboundedUInt balance = await client.BalanceOf(account);

// Transfer tokens from one account to another
TransferArgs transferArgs = new TransferArgs
{
    To = new Account
    {
        Id = Principal.FromText("<to_account_id>")
    },
    Amount = 1,
    Memo = "<memo>"
};
TransferResult transferResult = await client.Transfer(transferArgs);
```