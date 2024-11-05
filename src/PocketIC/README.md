# PocketIC

Library containing the [pocket-ic server](https://github.com/dfinity/pocketic) runtime and clients for interfacing for canister testing on the internet computer

- Nuget: [`EdjCase.ICP.PocketIC`](https://www.nuget.org/packages/EdjCase.ICP.PocketIC)

## Supported PocketIC Server Runtimes

The pocket-ic server binary is only compatible with some operating systems:

- linux-x64
- osx-x64

## Quick Start

### Basic usage

```cs
// Start server
await using PocketIcServer server = await PocketIcServer.Start();
string pocketIcServerUrl = server.GetUrl();

// Create a new PocketIC instance with a default subnets
await using PocketIc pocketIc = await PocketIc.CreateAsync(pocketIcServerUrl);

// Create a new canister
CreateCanisterResponse response = await pocketIc.CreateCanisterAsync();
Principal canisterId = response.CanisterId;

// Install WASM module
byte[] wasmModule = File.ReadAllBytes("path/to/my_canister.wasm");
await pocketIc.InstallCodeAsync(
    canisterId: canisterId,
    wasmModule: wasmModule,
    arg: CandidArg.FromCandid(),
    mode: InstallCodeMode.Install
);

// Start the canister
await pocketIc.StartCanisterAsync(canisterId);

// Make calls to the canister
UnboundedUInt counter = await pocketIc.QueryCallAsync<UnboundedUInt>(
    Principal.Anonymous(),
    canisterId,
    "my_method"
);

```

### HTTP Gateway usage

```cs

await using HttpGateway httpGateway = await pocketIc.RunHttpGatewayAsync()

// Create an HttpAgent to interact with canisters through the gateway
HttpAgent agent = httpGateway.BuildHttpAgent();

// Make calls using the agent
QueryResponse response = await agent.QueryAsync(canisterId, "my_method", CandidArg.Empty());
CandidArg reply = response.ThrowOrGetReply();

```

## Subnet Configuration

PocketIC instances can be created with various subnet configurations:

```cs
await PocketIc.CreateAsync(
    pocketIcServerUrl,
    applicationSubnets: [SubnetConfig.New()],  // Application subnets
    nnsSubnet: SubnetConfig.New(),            // Network Nervous System subnet
    iiSubnet: SubnetConfig.New(),             // Internet Identity subnet
    bitcoinSubnet: SubnetConfig.New(),        // Bitcoin subnet
    snsSubnet: SubnetConfig.New(),            // Service Nervous System subnet
    systemSubnets: [SubnetConfig.New()]       // System subnets
);
```

## Auto Time Progression

PocketIC provides control over the IC time for testing:

```cs

// Enable automatic time progression
await using (await pocketIc.AutoProgressTimeAsync())
{
    // Time will progress automatically
}
// Disposing will stop auto progress
```

## XUnit usage

```cs
// Fixture to create and run server for all the tests, disposing only after all tests are complete
public class PocketIcServerFixture : IDisposable
{
	public PocketIcServer Server { get; private set; }

	public PocketIcServerFixture()
	{
		// Start the server for all tests
		this.Server = PocketIcServer.Start(showRuntimeLogs: true).GetAwaiter().GetResult();
	}

	public void Dispose()
	{
		// Stop the server after all tests
		if (this.Server != null)
		{
			this.Server.DisposeAsync().GetAwaiter().GetResult();
		}
	}
}

// Unit tests injecting the fixture
public class PocketIcTests : IClassFixture<PocketIcServerFixture>
{
	private readonly PocketIcServerFixture fixture;
	private string url => this.fixture.Server.GetUrl();

	public PocketIcTests(PocketIcServerFixture fixture)
	{
		this.fixture = fixture;
	}

	[Fact]
	public async Task Test()
	{
		// Create new pocketic instance for test, then dispose it on completion
		await using (PocketIc pocketIc = await PocketIc.CreateAsync(this.url));

		// run test here

	}
}
```

## Call Types

### Query Calls

```cs
// No arguments
TResponse result = await pocketIc.QueryCallAsync<TResponse>(
    sender,
    canisterId,
    "method"
);

// With arguments
TResponse result = await pocketIc.QueryCallAsync<T1, TResponse>(
    sender,
    canisterId,
    "method",
    arg1
);

// Raw candid
CandidArg response = await pocketIc.QueryCallRawAsync(
    sender,
    canisterId,
    "method",
    CandidArg.FromCandid()
);
```

### Update Calls

```cs
// No arguments
TResponse result = await pocketIc.UpdateCallAsync<TResponse>(
    sender,
    canisterId,
    "method"
);

// With arguments
TResponse result = await pocketIc.UpdateCallAsync<T1, TResponse>(
    sender,
    canisterId,
    "method",
    arg1
);

// No response
await pocketIc.UpdateCallNoResponseAsync(
    sender,
    canisterId,
    "method"
);

// Raw candid
CandidArg response = await pocketIc.UpdateCallRawAsync(
    sender,
    canisterId,
    "method",
    CandidArg.FromCandid()
);
```
