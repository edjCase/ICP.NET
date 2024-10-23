using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Candid.Encodings;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Utilities;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.Linq;
using System.Xml.Linq;

namespace EdjCase.ICP.Agent.Responses
{
	/// <summary>
	/// A model representing the response data in the form of a variant
	/// </summary>
	public class QueryResponse
	{
		/// <summary>
		/// The type of response returned. Can be used to call the right method
		/// to extract the variant data
		/// </summary>
		public QueryResponseType Type { get; }
		/// <summary>
		/// Signatures from the replica node
		/// </summary>
		public List<NodeSignature> Signatures { get; }
		private readonly object value;

		private QueryResponse(QueryResponseType type, List<NodeSignature> signatures, object value)
		{
			this.Type = type;
			this.Signatures = signatures;
			this.value = value;
		}

		/// <summary>
		/// Gets the reply data IF the response type is 'replied'. Otherwise will throw exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Will throw if not of type 'replied'</exception>
		/// <returns>Reply data</returns>
		public CandidArg AsReplied()
		{
			this.ThrowIfWrongType(QueryResponseType.Replied);
			return (CandidArg)this.value;
		}

		/// <summary>
		/// Gets the reply data IF the response type is 'rejected'. Otherwise will throw exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Will throw if not of type 'rejected'</exception>
		/// <returns>Reject error information</returns>
		public QueryRejectInfo AsRejected()
		{
			this.ThrowIfWrongType(QueryResponseType.Rejected);
			return (QueryRejectInfo)this.value;
		}


		private void ThrowIfWrongType(QueryResponseType type)
		{
			if (this.Type != type)
			{
				throw new InvalidOperationException($"Unable to parse '{this.Type}' query response as type '{type}'");
			}
		}

		/// <summary>
		/// Helper method to either get the reply data or throw an exception
		/// that formats the 'rejected' data
		/// </summary>
		/// <returns>Query reply data</returns>
		/// <exception cref="QueryRejectedException">Throws if 'rejected'</exception>
		public CandidArg ThrowOrGetReply()
		{
			if (this.Type == QueryResponseType.Rejected)
			{
				QueryRejectInfo rejectInfo = this.AsRejected();
				throw new QueryRejectedException(rejectInfo);
			}
			return this.AsReplied();
		}

		internal static QueryResponse Rejected(RejectCode code, List<NodeSignature> signatures, string? message, string? errorCode)
		{
			return new QueryResponse(QueryResponseType.Rejected, signatures, new QueryRejectInfo(code, message, errorCode));
		}

		internal static QueryResponse Replied(CandidArg reply, List<NodeSignature> signatures)
		{
			return new QueryResponse(QueryResponseType.Replied, signatures, reply);
		}

		internal static QueryResponse ReadCbor(CborReader reader)
		{
			string? status = null;
			byte[]? replyArg = null;
			UnboundedUInt? rejectCode = null;
			string? rejectMessage = null;
			string? errorCode = null;
			List<NodeSignature> signatures = new ();

			if (reader.ReadTag() != CborTag.SelfDescribeCbor)
			{
				throw new CborContentException("Expected self describe tag");
			}
			reader.ReadStartMap();
			while (reader.PeekState() != CborReaderState.EndMap)
			{
				string name = reader.ReadTextString();
				switch (name)
				{
					case "status":
						status = reader.ReadTextString();
						break;
					case "reply":
						reader.ReadStartMap();

						while (reader.PeekState() != CborReaderState.EndMap)
						{
							string replyField = reader.ReadTextString();
							switch (replyField)
							{
								case "arg":
									replyArg = reader.ReadByteString();
									break;
							}
						}
						reader.ReadEndMap();
						break;
					case "reject_code":
						rejectCode = CborUtil.ReadNat(reader);
						break;
					case "reject_message":
						rejectMessage = reader.ReadTextString();
						break;
					case "error_code":
						errorCode = reader.ReadTextString();
						break;
					case "signatures":
						reader.ReadStartArray();
						while (reader.PeekState() != CborReaderState.EndArray)
						{
							signatures.Add(NodeSignature.ReadCbor(reader));
						}
						reader.ReadEndArray();
						break;
					default:
#if DEBUG
						throw new NotImplementedException($"Cannot deserialize query response. Unknown field '{name}'");
#else
						reader.SkipValue();
						break;
#endif

				}
			}
			reader.ReadEndMap();

			if (status == null)
			{
				throw new CborContentException("Missing field: status");
			}

			switch (status)
			{
				case "replied":
					if (replyArg == null)
					{
						throw new CborContentException("Missing field: reply");
					}
#if DEBUG
					string argHex = ByteUtil.ToHexString(replyArg!);
#endif
					var arg = CandidArg.FromBytes(replyArg!);
					return QueryResponse.Replied(arg, signatures);
				case "rejected":
					if (rejectCode == null)
					{
						throw new CborContentException("Missing field: reject_code");
					}
					RejectCode code = (RejectCode)(ulong)rejectCode!;
					return QueryResponse.Rejected(code, signatures, rejectMessage, errorCode);
				default:
					throw new NotImplementedException($"Cannot deserialize query response with status '{status}'");
			}
		}
	}

	/// <summary>
	/// The variant options for a query response
	/// </summary>
	public enum QueryResponseType
	{
		/// <summary>
		/// When the canister replies to a query request with no errors
		/// </summary>
		Replied,
		/// <summary>
		/// When the canister request has errors to query request
		/// </summary>
		Rejected
	}

	/// <summary>
	/// Data from a query response that has been rejected
	/// </summary>
	public class QueryRejectInfo
	{
		/// <summary>
		/// The type of query reject
		/// </summary>
		public RejectCode Code { get; }

		/// <summary>
		/// Optional. A human readable message about the rejection
		/// </summary>
		public string? Message { get; }

		/// <summary>
		/// Optional. A specific error id for the reject
		/// </summary>
		public string? ErrorCode { get; }

		internal QueryRejectInfo(RejectCode code, string? message, string? errorCode)
		{
			this.Code = code;
			this.Message = message;
			this.ErrorCode = errorCode;
		}
	}
}
