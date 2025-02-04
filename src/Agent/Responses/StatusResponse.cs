using System;
using System.Formats.Cbor;

namespace EdjCase.ICP.Agent.Responses
{
	/// <summary>
	/// The model for response data from a status request
	/// </summary>
	public class StatusResponse
	{
		/// <summary>
		/// Identifies the interface version supported, i.e. the version of the present
		/// document that the internet computer aims to support, e.g. `0.8.1`.
		/// The implementation may also return unversioned to indicate that it does not
		/// comply to a particular version, e.g. in between releases.
		/// </summary>
		public string? ICApiVersion { get; }

		/// <summary>
		/// Optional. Identifies the implementation of the Internet Computer Protocol,
		/// by convention with the canonical location of the source code 
		/// (e.g. https://github.com/dfinity/ic).
		/// </summary>
		public string? ImplementationSource { get; }

		/// <summary>
		///  Optional. If the user is talking to a released version of an Internet Computer Protocol
		///  implementation, this is the version number. For non-released versions, output
		///  of git describe like `0.1.13-13-g2414721` would also be very suitable.
		/// </summary>
		public string? ImplementationVersion { get; }

		/// <summary>
		/// Optional. The precise git revision of the Internet Computer Protocol implementation
		/// </summary>
		public string? ImplementationRevision { get; }

		/// <summary>
		/// The public key (a DER-encoded BLS key) of the root key of this development
		/// instance of the Internet Computer Protocol. This must be present in short-lived
		/// development instances, to allow the agent to fetch the public key. For the
		/// Internet Computer, agents must have an independent trustworthy source for this data,
		/// and must not be tempted to fetch it from this insecure location.
		/// </summary>
		public byte[]? DevelopmentRootKey { get; }

		/// <param name="icApiVersion">Optional. Identifies the interface version supported, i.e. the version of the present
		/// document that the internet computer aims to support, e.g. `0.8.1`.
		/// The implementation may also return unversioned to indicate that it does not
		/// comply to a particular version, e.g. in between releases.</param>
		/// <param name="implementationSource">Optional. Identifies the implementation of the Internet Computer Protocol,
		/// by convention with the canonical location of the source code 
		/// (e.g. https://github.com/dfinity/ic).</param>
		/// <param name="implementationVersion">Optional. If the user is talking to a released version of an Internet Computer Protocol
		///  implementation, this is the version number. For non-released versions, output
		///  of git describe like `0.1.13-13-g2414721` would also be very suitable.</param>
		/// <param name="implementationRevision">Optional. The precise git revision of the Internet Computer Protocol implementation</param>
		/// <param name="developmentRootKey">The public key (a DER-encoded BLS key) of the root key of this development
		/// instance of the Internet Computer Protocol. This must be present in short-lived
		/// development instances, to allow the agent to fetch the public key. For the
		/// Internet Computer, agents must have an independent trustworthy source for this data,
		/// and must not be tempted to fetch it from this insecure location</param>
		public StatusResponse(
			string? icApiVersion,
			string? implementationSource,
			string? implementationVersion,
			string? implementationRevision,
			byte[]? developmentRootKey)
		{
			this.ICApiVersion = icApiVersion;
			this.ImplementationSource = implementationSource;
			this.ImplementationVersion = implementationVersion;
			this.ImplementationRevision = implementationRevision;
			this.DevelopmentRootKey = developmentRootKey;
		}


		internal static StatusResponse ReadCbor(CborReader reader)
		{
			string? icApiVersion = null;
			string? implementationSource = null;
			string? implementationVersion = null;
			string? implementationRevision = null;
			byte[]? developmentRootKey = null;

			if (reader.ReadTag() != CborTag.SelfDescribeCbor)
			{
				throw new CborContentException("Expected self describe tag");
			}

			int? mapSize = reader.ReadStartMap();

			while (reader.PeekState() != CborReaderState.EndMap)
			{
				string key = reader.ReadTextString();
				switch (key)
				{
					case Properties.IC_API_VERSION:
						icApiVersion = reader.ReadTextString();
						break;
					case Properties.IMPLEMENTATION_SOURCE:
						implementationSource = reader.ReadTextString();
						break;
					case Properties.IMPLEMENTATION_VERSION:
						implementationVersion = reader.ReadTextString();
						break;
					case Properties.IMPLEMENTATION_REVISION:
						implementationRevision = reader.ReadTextString();
						break;
					case Properties.ROOT_KEY:
						developmentRootKey = reader.ReadByteString();
						break;
					default:
						//skip
						reader.SkipValue();
						break;
				}
			}

			reader.ReadEndMap();

			return new StatusResponse(icApiVersion, implementationSource, implementationVersion, implementationRevision, developmentRootKey);
		}

		private class Properties
		{
			public const string IC_API_VERSION = "ic_api_version";
			public const string IMPLEMENTATION_SOURCE = "impl_source";
			public const string IMPLEMENTATION_VERSION = "impl_version";
			public const string IMPLEMENTATION_REVISION = "impl_revision";
			public const string ROOT_KEY = "root_key";
		}


	}
}