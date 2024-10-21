using EdjCase.ICP.Candid;
using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdjCase.ICP.ClientGenerator
{
	/// <summary>
	/// Options for generating a client
	/// </summary>
	public class ClientGenerationOptions
	{
		/// <summary>
		/// The name of the client class and file to use
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// If true, will treat `FilePathOrCanisterId` as a canister id and get the definition from the canister. Otherwise will treat it as a file path and get the definition from the file
		/// </summary>
		public bool GetDefinitionFromCanister { get; }

		/// <summary>
		/// The file path to a local *.did file to get definition from or the canister id, depending on `GetDefinitionFromCanister` value
		/// </summary>
		public string FilePathOrCanisterId { get; }

		/// <summary>
		/// If true, removes all files in the output directory before regeneration, otherwise does nothing. Defaults to true
		/// </summary>
		public bool PurgeOutputDirectory { get; }

		/// <summary>
		/// The base namespace to use in the generated files
		/// </summary>
		public string Namespace { get; }

		/// <summary>
		/// If true, there will be no folders, all files will be in the same directory
		/// </summary>
		public string OutputDirectory { get; }

		/// <summary>
		/// If true, there will be no folders, all files will be in the same directory
		/// </summary>
		public bool NoFolders { get; }

		/// <summary>
		/// If true, the nullable C# feature will be used
		/// </summary>
		public bool FeatureNullable { get; }

		/// <summary>
		/// If true, variant classes will be generated with properties instead of methods
		/// </summary>
		public bool VariantsUseProperties { get; }

		/// <summary>
		/// If true, the names of properties and methods will keep the raw candid name.
		/// Otherwise they will be converted to something prettier
		/// </summary>
		public bool KeepCandidCase { get; }

		/// <summary>
		/// If true, OptionalValue will be used for opt values
		/// Otherwise will use just the nullable class values or nullable struct
		/// Defaults to true
		/// </summary>
		public bool OverrideOptionalValue { get; }

		/// <summary>
		/// Optional. The url of the boundry node for the internet computer. Defaults to ic0.app
		/// </summary>
		public Uri? BoundryNodeUrl { get; }

		/// <summary>
		/// Optional. Specifies options for each candid type in the definition.
		/// Only supports named types, no anonymous types
		/// </summary>
		public Dictionary<string, NamedTypeOptions> Types { get; }


		/// <param name="name">The name of the client class and file to use</param>
		/// <param name="namespace">The base namespace to use in the generated files</param>
		/// <param name="getDefinitionFromCanister">If true, will treat <paramref name="filePathOrCandidId"/> as a canister id and get the definition from the canister. Otherwise will treat it as a file path and get the definition from the file</param>
		/// <param name="filePathOrCandidId">The file path to a local *.did file to get definition from or the canister id, depending on <paramref name="getDefinitionFromCanister"/> value</param>
		/// <param name="outputDirectory">The output directory to generate the client files</param>
		/// <param name="purgeOutputDirectory">If true, removes all files in the output directory before regeneration. Defaults to true</param>
		/// <param name="noFolders">If true, there will be no folders, all files will be in the same directory</param>
		/// <param name="featureNullable">If true, the nullable C# feature will be used</param>
		/// <param name="variantsUseProperties">If true, variant classes will be generated with properties instead of methods</param>
		/// <param name="keepCandidCase">If true, the names of properties and methods will keep the raw candid name. Otherwise they will be converted to something prettier</param>
		/// <param name="overrideOptionalValue">If false, OptionalValue will be used for opt values, Otherwise will use just the nullable class values or nullable struct where possible. Defaults to false</param>
		/// <param name="boundryNodeUrl">Optional. The url of the boundry node for the internet computer. Defaults to ic0.app</param>
		/// <param name="types">Optional. Specifies options for each candid type in the definition</param>
		public ClientGenerationOptions(
			string name,
			string @namespace,
			bool getDefinitionFromCanister,
			string filePathOrCandidId,
			string outputDirectory,
			bool purgeOutputDirectory = true,
			bool noFolders = false,
			bool featureNullable = false,
			bool variantsUseProperties = false,
			bool keepCandidCase = false,
			bool overrideOptionalValue = false,
			Uri? boundryNodeUrl = null,
			Dictionary<string, NamedTypeOptions>? types = null
		)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentNullException(nameof(name));
			}

			// Trim whitespace and replace spaces with underscores
			this.Name = StringUtil.ToPascalCase(name.Trim().Replace(' ', '_'));
			this.Namespace = @namespace ?? throw new ArgumentNullException(nameof(@namespace));
			this.GetDefinitionFromCanister = getDefinitionFromCanister;
			this.FilePathOrCanisterId = filePathOrCandidId;
			this.OutputDirectory = outputDirectory;
			this.PurgeOutputDirectory = purgeOutputDirectory;
			this.NoFolders = noFolders;
			this.FeatureNullable = featureNullable;
			this.VariantsUseProperties = variantsUseProperties;
			this.KeepCandidCase = keepCandidCase;
			this.OverrideOptionalValue = overrideOptionalValue;
			this.BoundryNodeUrl = boundryNodeUrl;
			this.Types = types ?? new Dictionary<string, NamedTypeOptions>();
		}
	}


	/// <summary>
	/// Type options for a record field or variant option
	/// </summary>
	public class NamedTypeOptions
	{
		/// <summary>
		/// Optional. The C# type name to use instead of the default
		/// </summary>
		public string? NameOverride { get; }

		/// <summary>
		/// Optional. The field or option type information
		/// </summary>
		public TypeOptions? TypeOptions { get; }

		/// <param name="nameOverride">Optional. The C# type name to use instead of the default</param>
		/// <param name="typeOptions">Optional. The field or option type information</param>
		public NamedTypeOptions(
			string? nameOverride = null,
			TypeOptions? typeOptions = null
		)
		{
			this.NameOverride = nameOverride;
			this.TypeOptions = typeOptions;
		}
	}

	/// <summary>
	/// Interface to specify generation options for specific types in the candid
	/// </summary>
	public class TypeOptions
	{
		/// <summary>
		/// Optional. The type options for each of the records fields or variant options
		/// </summary>
		public Dictionary<string, NamedTypeOptions> Fields { get; }

		/// <summary>
		/// Optional. The type options for the sub type of a vec or opt
		/// </summary>
		public TypeOptions? InnerType { get; }

		/// <summary>
		/// Optional. How the type should be represented in C#
		/// </summary>
		public string? Representation { get; }

		/// <param name="fields">Optional. The type options for each of the records fields or variant options</param>
		/// <param name="innerType">Optional. The type options for the sub type of a vec or opt</param>
		/// <param name="representation">Optional. How the type should be represented in C#</param>
		/// <exception cref="ArgumentNullException"></exception>
		public TypeOptions(
			Dictionary<string, NamedTypeOptions>? fields,
			TypeOptions? innerType,
			string? representation
		)
		{
			this.Fields = fields ?? new Dictionary<string, NamedTypeOptions>();
			this.InnerType = innerType;
			this.Representation = representation;
		}
	}
}
