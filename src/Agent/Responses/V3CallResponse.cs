using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;
using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Candid.Models;

namespace EdjCase.ICP.Agent.Responses
{
	/// <summary>
	/// Model for a reponse to a read state request
	/// </summary>
	internal class V3CallResponse
	{
		public enum StatusType
		{
			/// <summary>
			/// The request has been processed and it has reply data
			/// </summary>
			Replied,
			NonReplicatedRejection
		}


		/// <summary>
		/// The status of the call ('replied', 'non_replicated_rejection')
		/// </summary>
		public StatusType Status { get; }

		private object? value { get; }

		/// <param name="status">The status of the call ('replied', 'non_replicated_rejection')</param>
		/// <param name="value">The certificate data of the current canister state</param>
		private V3CallResponse(StatusType status, object? value)
		{
			this.Status = status;
			this.value = value;
		}

		/// <summary>
		///	Returns the 'replied' certificate IF the status is 'replied', otherwise throws exception
		/// </summary>
		public Certificate AsReplied()
		{
			this.ValidateType(StatusType.Replied);
			return (Certificate)this.value!;
		}

		public CallRejectedResponse AsNonReplicatedRejection()
		{
			this.ValidateType(StatusType.NonReplicatedRejection);
			return (CallRejectedResponse)this.value!;
		}

		private void ValidateType(StatusType type)
		{
			if (this.Status != type)
			{
				throw new InvalidOperationException($"Expected status '{type}' but was '{this.Status}'");
			}
		}

		internal static V3CallResponse ReadCbor(CborReader reader)
		{
			if (reader.ReadTag() != CborTag.SelfDescribeCbor)
			{
				throw new CborContentException("Expected self describe tag");
			}
			Dictionary<string, object> map = new ();
			reader.ReadStartMap();
			while (reader.PeekState() != CborReaderState.EndMap)
			{
				string field = reader.ReadTextString();
				switch (field)
				{
					case "certificate":
						var certReader = new CborReader(reader.ReadByteString());
						map["certificate"] = Certificate.FromCbor(certReader);
						break;
					case "status":
						map["status"] = reader.ReadTextString();
						break;
					case "reject_code":
						map["reject_code"] = (RejectCode)reader.ReadUInt64();
						break;
					case "reject_message":
						map["reject_message"] = reader.ReadTextString();
						break;
					case "error_code":
						map["error_code"] = reader.ReadTextString();
						break;
					default:
						Debug.WriteLine($"Unknown field '{field}' in v3 call response");
						reader.SkipValue();
						break;
				}
			}
			reader.ReadEndMap();

			if (map["status"] == null)
			{
				throw new CborContentException("Missing field: status");
			}
			StatusType status = Enum.Parse<StatusType>((string)map["status"], true);

			switch (status)
			{
				case StatusType.Replied:
					Certificate? certificate = map["certificate"] as Certificate;
					if (certificate == null)
					{
						throw new CborContentException("Missing field: certificate");
					}
					return new V3CallResponse(status, certificate);
				case StatusType.NonReplicatedRejection:
					RejectCode rejectCode = (RejectCode)(map["reject_code"] ?? throw new CborContentException("Missing field: reject_code"));
					string message = (string)(map["reject_message"] ?? throw new CborContentException("Missing field: reject_message"));
					string? errorCode = (string?)map["error_code"];
					CallRejectedResponse? rejectedResponse = new CallRejectedResponse(rejectCode, message, errorCode);
					return new V3CallResponse(status, rejectedResponse);
				default:
					throw new NotImplementedException($"Unknown status '{status}' in v3 call response");
			}
		}
	}
}
