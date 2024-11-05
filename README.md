# ICP.NET

Collection of Internet Computer Protocol (ICP) libraries for .NET/Blazor/Unity

- [Agent](src/Agent/README.md) - Library to communicate to and from the Internet Computer

- [Candid](src/Candid/README.md) - Library of Candid Encoding, Models and Helpers

- [Client Generator](src/ClientGenerator/README.md) - Client source code generator for ICP canisters

- [PocketIC](src/PocketIC/README.md) - [PocketIC Server](https://github.com/dfinity/pocketic) client and runner for automating tests for canisters

## See each individual project README for more in depth guides

# 🎮 Unity Integration

- Download latest `agent.unitypackage` from: https://github.com/edjCase/ICP.NET/releases
- Import `agent.unitypackage` into your unity project
- If using WebGL, follow the additional WebGL instructions in the [Agent Docs](src/Agent/README.md)
- If generating a client (see below), place the generated files into the scripts folder: `Assets/scripts/MyClient`
- Start coding 💻

# 📡 Generating a client for a canister

You can specify all the models and api calls yourself, but this is a tool to automatically generate a client and models based on the canister or .did file

- Prerequisite: Have .Net 6 installed (https://dotnet.microsoft.com/en-us/download/dotnet)
- Navigate to directory of .Net project
  ```
  cd {path/to/project}
  ```
- Add Agent nuget package to project
  ```
  dotnet add package EdjCase.ICP.Agent
  ```
- Install ClientGenerator

  ```
  dotnet tool install -g EdjCase.ICP.ClientGenerator
  ```

  This will allow a client to be automatically be generated for a canister. See [ClientGenerator README](src/ClientGenerator/README.md) for more details and advanced config

- Initialize ClientGenerator config (first run only)
  ```
  candid-client-generator init
  ```
  This will create a TOML config file in the directory that can be changed for more advanced options
- Update created config file `candid-client.toml`

  If using a canister id:

  ```toml
  namespace = "ProjectGovernance" # Base namespace to use
  output-directory = "./Clients" # Output directory

  [[clients]]
  name = "Governance" # Label of client to use
  type = "canister" # Indicates to make client from a canister id
  canister-id = "rrkah-fqaaa-aaaaa-aaaaq-cai" # Canister id to make client for
  ```

  If using a service definition file (.did)

  ```toml
  namespace = "ProjectGovernance" # Base namespace to use
  output-directory = "./Clients" # Output directory

  [[clients]]
  name = "Governance" # Label of client to use
  type = "file" # Indicates to make client from a service definition file
  file-path = "Governance.did" # File to use
  ```

  For all configuration options see [ClientGenerator README](src/ClientGenerator/README.md) for more details

- Generate Client
  ```
  candid-client-generator gen
  ```
  Will output C# file to the output directory specified in the config
- Use client in code
  ```cs
  var agent = new HttpAgent();
  Principal canisterId = Principal.FromText("rrkah-fqaaa-aaaaa-aaaaq-cai");
  var client = new GovernanceApiClient(agent, canisterId);
  OptionalValue<Sample.Shared.Governance.Models.ProposalInfo> proposalInfo = await client.GetProposalInfo(110174);
  ...
  ```
- SHIP IT! 🚀

# Breaking change migrations

## 3.x.x => 4.x.x

The big change here was around variant classes and their attributes. Before the option types were defined by the attribute on each enum member, but in 4.x.x it changed to using method return types and having not type information in attributes. Also the VariantAttribute now gets the enum type from the Tag property vs the attribute

### Version 3

```
[Variant(typeof(MyVariantTag))] // Required to flag as variant and define options with enum
public class MyVariant
{
    [VariantTagProperty] // Flag for tag/enum property, not required if name is `Tag`
    public MyVariantTag Tag { get; set; }
    [VariantValueProperty] // Flag for value property, not required if name is `Value`
    public object? Value { get; set; }
}

public enum MyVariantTag
{
    [CandidName("o1")] // Used to override name for candid
    Option1,
    [CandidName("o2")]
    [VariantType(typeof(string))] // Used to specify if the option has a value associated
    Option2
}
```

### Version 4

```
[Variant] // Required to flag as variant
public class MyVariant
{
	[VariantTagProperty] // Flag for tag/enum property, not required if name is `Tag`
	public MyVariantTag Tag { get; set; }
	[VariantValueProperty] // Flag for value property, not required if name is `Value`
	public object? Value { get; set; }


	// This method is used to specify if the option has a type/value associated
	[VariantOption("o2")] // Specify the candid tag if different than 'As{CandidTag}' like 'Option2' here
	public string AsOption2()
	{
		return (string)this.Value!;
	}
}

public enum MyVariantTag
{
    [CandidName("o1")] // Used to override name for candid
    Option1,
    [CandidName("o2")]
    Option2
}
```

# Candid Related Links

- [IC Http Interface Spec](https://smartcontracts.org/docs/current/references/ic-interface-spec)
- [Candid Spec](https://github.com/dfinity/candid/blob/master/spec/Candid.md)
- [Candid Decoder](https://fxa77-fiaaa-aaaae-aaana-cai.raw.ic0.app/explain)
- [Candid UI Tester](https://a4gq6-oaaaa-aaaab-qaa4q-cai.raw.ic0.app)
