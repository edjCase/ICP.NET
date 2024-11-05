using System;
using System.Diagnostics;
using System.Formats.Cbor;
using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Candid.Models;

namespace EdjCase.ICP.Agent.Responses
{
	/// <summary>
	/// Model for a reponse to a read state request
	/// </summary>
	public class V3CallResponse
	{
		/// <summary>
		/// The status of the call ('replied', 'rejected', 'done)
		/// </summary>
		public string Status { get; }
		/// <summary>
		/// The certificate data of the current canister state
		/// </summary>
		public Certificate Certificate { get; }

		/// <param name="status">The status of the call ('replied', 'rejected', 'done)</param>
		/// <param name="certificate">The certificate data of the current canister state</param>
		/// <exception cref="ArgumentNullException"></exception>
		public V3CallResponse(string status, Certificate certificate)
		{
			this.Status = status ?? throw new ArgumentNullException(nameof(status));
			this.Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
		}


		internal static V3CallResponse ReadCbor(CborReader reader)
		{
			if (reader.ReadTag() != CborTag.SelfDescribeCbor)
			{
				throw new CborContentException("Expected self describe tag");
			}
			Certificate? certificate = null;
			string? status = null;
			reader.ReadStartMap();
			while (reader.PeekState() != CborReaderState.EndMap)
			{
				string field = reader.ReadTextString();
				switch (field)
				{
					case "certificate":
						var certReader = new CborReader(reader.ReadByteString());
						certificate = Certificate.FromCbor(certReader);
						break;
					case "status":
						status = reader.ReadTextString();
						break;
					default:
						Debug.WriteLine($"Unknown field '{field}' in v3 call response");
						reader.SkipValue();
						break;
				}
			}
			reader.ReadEndMap();

			if (status == null)
			{
				throw new CborContentException("Missing field: status");
			}

			if (certificate == null)
			{
				throw new CborContentException("Missing field: certificate");
			}

			return new V3CallResponse(status, certificate);
		}
	}
}