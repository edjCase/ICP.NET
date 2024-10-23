# Client Generator

- Library of generating C# client code from \*.did files
- Nuget: [`EdjCase.ICP.ClientGenerator`](https://www.nuget.org/packages/EdjCase.ICP.ClientGenerator)

## Usage (dotnet tool)

### Install with dotnet tools

```
dotnet tool install -g EdjCase.ICP.ClientGenerator
```

### Run tool

(First run only) Initialize config file and update generated file

```
candid-client-generator init ./
```

Creates `candid-client.toml` file to update in specified directory

Example:

```
namespace = "My.Namespace"
output-directory = "./Clients"
no-folders = false

[[clients]]
name = "Dex"
type = "file"
file-path = "./ServiceDefinitionFiles/Dex.did"

[[clients]]
name = "Governance"
type = "canister"
canister-id = "rrkah-fqaaa-aaaaa-aaaaq-cai"
```

### Generate clients

```
candid-client-generator ./
```

or

```
candid-client-generator gen ./
```

### Config file options

#### Top Level:

- `namespace` - (Text) REQUIRED. The base namespace used in all C# files generated.
  Files generated in a sub-folder will have a more specific namespace to match. This namespace can be overidden per client.
- `output-directory` - (Text) OPTIONAL. Directory to put all generated files. Each client will have a sub-folder within the output directory that will match the client name. If not specified, the working directory will be used
- `no-folders` - (Bool) OPTIONAL. If true, no sub-folders will be generated for the clients or the models within the clients. All generated files will be in a flat structure. Defaults to false
- `url` - (Text) OPTIONAL. Sets the boundry node url to use for making calls to canisters on the IC. Can be set to a local developer instance/localhost. Defaults to 'https://ic0.app/'. This setting is only useful for clients of generation type `canister`
- `feature-nullable` - (Bool) Optional. Sets whether to use the C# nullable feature when generating the client (like `object?`). Defaults to true
- `variants-use-properties` - (Bool) Optional. If true, the generated variant classes will use properties instead of methods for data access. Defaults to false
- `keep-candid-case` - (Bool) Optional. If true, the names of properties and methods will keep the raw candid name. Otherwise they will be converted to something prettier. Defaults to false
- `override-optional-value` - (Bool) Optional. If false, opt Candid types will be represented as OptionalValue\<T\>, otherwise will just use T? (where possible). Defaults to false. NOTE: this feature is not recommended for type and performance reasons and should be considered experimental
#### Client Level:

- `name` - (Text) REQUIRED. The name of the sub-folder put the client files and the prefix to the client class name.
- `type` - (Text) REQUIRED. An enum value to indicate what type of client generation method to use. Each enum value also has associated configuration settings. Options:
  - `file` - Will create a client based on a service definition file (`*.did`)
    - `file-path` - (Text) REQUIRED. The file path to the `*.did` file to generate from
  - `canister` - Creates a client based on a canister id
    - `canister-id` - (Text) REQUIRED. The principal id of the canister to generate a client for
- `output-directory` - (Text) OPTIONAL. Directory to put all generated client files. Overrides the top level `output-directory`. NOTE: this does not create a sub-folder based on the client name like the top level `output-directory` does
- `no-folders` - (Bool) OPTIONAL. If true, no sub-folders will be generated for the client. All generated files will be in a flat structure. Defaults to false. Overrides the top level `no-folders`
- `feature-nullable` - (Bool) Optional. Sets whether to use the C# nullable feature when generating the client (like `object?`). Defaults to true. Overrides the top level `feature-nullable`
- `keep-candid-case` - (Bool) Optional. If true, the names of properties and methods will keep the raw candid name. Otherwise they will be converted to something prettier. Defaults to false. Overrides the top level `keep-candid-case`
- `override-optional-value` - (Bool) Optional. If false, opt Candid types will be represented as OptionalValue\<T\>, otherwise will just use T? (where possible). Defaults to false. NOTE: this feature is not recommended for type and performance reasons and should be considered experimental
#### Type Customization:

##### All Types

`[clients.types.{CandidTypeId}]`

`{CandidTypeId}` is any named type in the candid definition

- `name` - Optional. (Text) Overrides the name of the C# type/alias generated

---

##### Record Types Options

- `[clients.types.{CandidTypeId}.fields.{CandidFieldId}]`

  `{CandidFieldId}` is a record field in the record type `{CandidTypeId}`

  Uses the same options as the top level `[clients.types.{CandidTypeId}]` section

---

##### Variant Types Options

- `representation` - Optional (Text) Sets what C# type should be generated. (Defaults to Dictionary if possible, otherwise a List)

  - `ClassWithMethods` - (Default) Uses a C# class with method accessors
  - `ClassWithProperties` - Uses a C# class with property accessors

- `[clients.types.{CandidTypeId}.fields.{CandidOptionId}]`

  `{CandidOptionId}` is a variant option in the variant type `{CandidTypeId}`

  Uses the same options as the top level `[clients.types.{CandidTypeId}]` section

---

##### Vec Types Options

- `representation` - Optional (Text) Sets what C# type should be generated. (Defaults to Dictionary if possible, otherwise a List)

  - `Array` - Uses a C# array
  - `Dictionary` - (Default if applicable) Uses a C# `Dictionary<TKey, TValue>`. Only works if the `vec` contains a `record` with 2 unamed fields (tuple). The first will be the key, the second will be the value
  - `List` - (Default if not Dictionary) Uses a C# `List<T>`

- `[clients.types.{CandidTypeId}.innerType]`

  Uses the same options as the top level `[clients.types.{CandidTypeId}]` section, but `name` is not supported

##### Opt Types Options

- `[clients.types.{CandidTypeId}.innerType]`

  Uses the same options as the top level `[clients.types.{CandidTypeId}]` section, but `name` is not supported

##### Type Customization Example:

```
[clients]
...

[clients.types.AccountIdentifier]
name = "AccountId"
[clients.types.AccountIdentifier.fields.hash]
name = "Hash"
representation = "Array"

[clients.types.Action.fields.RegisterKnownNeuron]
name = "RegisterNeuron" # Rename RegisterKnownNeuron -> RegisterNeuron
[clients.types.Action.fields.RegisterKnownNeuron.fields.id] # Update the type's field `id`
name = "ID" # Rename id -> ID
[clients.types.Action.fields.RegisterKnownNeuron.fields.id.innerType.fields.id] # Update the opt's inner record type's field `id`
name = "ID" # Rename id -> ID
```

# Custom Client Generators via Code

Due to the complexity of different use cases, custom tweaks to the output of the client generators might be helpful. This process
can be handled with calling the `ClientCodeGenerator` manually and using the .NET `CSharpSyntaxRewriter` (tutorial can be found [HERE](https://joshvarty.com/2014/08/15/learn-roslyn-now-part-5-csharpsyntaxrewriter/))

```cs
var options = new ClientGenerationOptions(
	name: "MyClient",
	@namespace: "My.Namespace",
	noFolders: false,
	featureNullable: true,
	keepCandidCase: false
);
ClientSyntax syntax = await ClientCodeGenerator.GenerateClientFromCanisterAsync(canisterId, options);
var rewriter = new MyCustomCSharpSyntaxRewriter();
syntax = syntax.Rewrite(rewriter);
(string clientFile, List<(string Name, string Contents)> typeFiles) = syntax.GenerateFileContents();
// Write string contents to files...
```
