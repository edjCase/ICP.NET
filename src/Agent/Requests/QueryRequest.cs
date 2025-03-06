using EdjCase.ICP.Candid.Models;
using System;
using System.Collections.Generic;
using System.Formats.Cbor;

namespace EdjCase.ICP.Agent.Requests
{
	/// <summary>
	/// A model for making query requests to a canister
	/// </summary>
	public class QueryRequest : IRepresentationIndependentHashItem
	{
		/// <summary>
		/// The type of request to send. Will always be 'query'
		/// </summary>
		public string RequestType { get; } = "query";

		/// <summary>
		/// The principal of the canister to call
		/// </summary>
		public Principal CanisterId { get; }

		/// <summary>
		/// Name of the canister method to call
		/// </summary>
		public string Method { get; }

		/// <summary>
		/// Arguments to pass to the canister method
		/// </summary>
		public CandidArg Arg { get; }

		/// <summary>
		/// The user who issued the request
		/// </summary>
		public Principal Sender { get; }

		/// <summary>
		/// An upper limit on the validity of the request, expressed in nanoseconds since 1970-01-01
		/// </summary>
		public ICTimestamp IngressExpiry { get; }

		/// <summary>
		/// Optional. If specified will make the request unique even with the same arguments
		/// </summary>
		public byte[]? Nonce { get; }

		/// <param name="canisterId">The principal of the canister to call</param>
		/// <param name="method">Name of the canister method to call</param>
		/// <param name="arg">Arguments to pass to the canister method</param>
		/// <param name="sender">The user who issued the request</param>
		/// <param name="ingressExpiry"> The expiration of the request to avoid replay attacks</param>
		/// <param name="nonce">Optional. If specified will make the request unique even with the same arguments</param>
		public QueryRequest(
			Principal canisterId,
			string method,
			CandidArg arg,
			Principal sender,
			ICTimestamp ingressExpiry,
			byte[]? nonce = null
		)
		{
			this.CanisterId = canisterId ?? throw new ArgumentNullException(nameof(canisterId));
			this.Method = method ?? throw new ArgumentNullException(nameof(method));
			this.Arg = arg ?? throw new ArgumentNullException(nameof(arg));
			this.Sender = sender ?? throw new ArgumentNullException(nameof(sender));
			this.IngressExpiry = ingressExpiry ?? throw new ArgumentNullException(nameof(ingressExpiry));
			this.Nonce = nonce ?? throw new ArgumentNullException(nameof(nonce));
		}

		/// <inheritdoc />
		public Dictionary<string, IHashable> BuildHashableItem()
		{
			var properties = new Dictionary<string, IHashable>
			{
				{Properties.REQUEST_TYPE, this.RequestType.ToHashable()},
				{Properties.CANISTER_ID, this.CanisterId},
				{Properties.METHOD_NAME, this.Method.ToHashable()},
				{Properties.ARG, this.Arg},
				{Properties.SENDER, this.Sender},
				{Properties.INGRESS_EXPIRY, this.IngressExpiry}
			};
			if (this.Nonce != null)
			{
				properties.Add(Properties.NONCE, this.Nonce.ToHashable());
			}
			return properties;
		}

		internal void WriteCbor(CborWriter writer)
		{
			writer.WriteStartMap(null);

			writer.WriteTextString(Properties.REQUEST_TYPE);
			writer.WriteTextString(this.RequestType);

			writer.WriteTextString(Properties.CANISTER_ID);
			writer.WritePrincipal(this.CanisterId);

			writer.WriteTextString(Properties.METHOD_NAME);
			writer.WriteTextString(this.Method);

			writer.WriteTextString(Properties.ARG);
			writer.WriteByteString(this.Arg.Encode());

			writer.WriteTextString(Properties.SENDER);
			writer.WritePrincipal(this.Sender);

			writer.WriteTextString(Properties.INGRESS_EXPIRY);
			writer.WriteTimestamp(this.IngressExpiry);

			if (this.Nonce != null)
			{
				writer.WriteTextString(Properties.NONCE);
				writer.WriteByteString(this.Nonce);
			}

			writer.WriteEndMap();
		}


		private static class Properties
		{
			public const string REQUEST_TYPE = "request_type";
			public const string CANISTER_ID = "canister_id";
			public const string METHOD_NAME = "method_name";
			public const string ARG = "arg";
			public const string SENDER = "sender";
			public const string INGRESS_EXPIRY = "ingress_expiry";
			public const string NONCE = "nonce";
		}
	}
}
