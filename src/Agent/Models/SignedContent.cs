using EdjCase.ICP.Candid.Models;
using System;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.Linq;

namespace EdjCase.ICP.Agent.Models
{
	/// <summary>
	/// A model containing content and the signature information of it
	/// </summary>
	public class SignedContent<TRequest> : IRepresentationIndependentHashItem
		where TRequest : IRepresentationIndependentHashItem
	{
		/// <summary>
		/// The hash of the content used for request identification
		/// </summary>
		public RequestId RequestId { get; }

		/// <summary>
		/// The request that is signed
		/// </summary>
		public TRequest Request { get; }

		/// <summary>
		/// Public key used to authenticate this request, unless anonymous, then null
		/// </summary>
		public SubjectPublicKeyInfo? SenderPublicKey { get; }

		/// <summary>
		/// Optional. A chain of delegations, starting with the one signed by sender_pubkey
		/// and ending with the one delegating to the key relating to sender_sig.
		/// </summary>
		public List<SignedDelegation>? SenderDelegations { get; }

		/// <summary>
		/// Signature to authenticate this request, unless anonymous, then null
		/// </summary>
		public byte[]? SenderSignature { get; }

		/// <param name="requestId">The hash of the content used for request identification</param>
		/// <param name="request">The content that is signed in the form of key value pairs</param>
		/// <param name="senderPublicKey">Public key used to authenticate this request, unless anonymous, then null</param>
		/// <param name="delegations">Optional. A chain of delegations, starting with the one signed by sender_pubkey 
		/// and ending with the one delegating to the key relating to sender_sig.</param>
		/// <param name="senderSignature">Signature to authenticate this request, unless anonymous, then null</param>
		public SignedContent(
			RequestId requestId,
			TRequest request,
			SubjectPublicKeyInfo? senderPublicKey,
			List<SignedDelegation>? delegations,
			byte[]? senderSignature
		)
		{
			this.RequestId = requestId ?? throw new ArgumentNullException(nameof(requestId));
			this.Request = request ?? throw new ArgumentNullException(nameof(request));
			this.SenderPublicKey = senderPublicKey;
			this.SenderDelegations = delegations;
			this.SenderSignature = senderSignature;
		}

		/// <inheritdoc />
		public Dictionary<string, IHashable> BuildHashableItem()
		{
			var properties = new Dictionary<string, IHashable>
			{
				{Properties.CONTENT, this.Request.BuildHashableItem().ToHashable()}
			};
			if (this.SenderPublicKey != null)
			{
				properties.Add(Properties.SENDER_PUBLIC_KEY, this.SenderPublicKey.ToDerEncoding().ToHashable());
			}
			if (this.SenderSignature != null)
			{
				properties.Add(Properties.SENDER_SIGNATURE, this.SenderSignature.ToHashable());
			}
			if (this.SenderDelegations != null)
			{
				properties.Add(Properties.SENDER_DELEGATION, this.SenderDelegations.ToHashable());
			}
			return properties;
		}


		public byte[] ToCborBytes()
		{
			CborWriter writer = new ();
			writer.WriteTag(CborTag.SelfDescribeCbor);
			this.WriteCbor(writer);
			return writer.Encode();
		}

		internal void WriteCbor(CborWriter writer)
		{
			writer.WriteStartMap(null);
			writer.WriteTextString(Properties.CONTENT);
			Dictionary<string, IHashable> hashableContent = this.Request.BuildHashableItem();
			writer.WriteStartMap(hashableContent.Count);
			foreach ((string key, IHashable value) in hashableContent)
			{
				writer.WriteTextString(key);
				writer.WriteHashableValue(value);
			}
			writer.WriteEndMap();
			if (this.SenderPublicKey != null)
			{
				writer.WriteTextString(Properties.SENDER_PUBLIC_KEY);
				writer.WriteByteString(this.SenderPublicKey.ToDerEncoding());
			}
			if (this.SenderDelegations?.Any() == true)
			{
				writer.WriteTextString(Properties.SENDER_DELEGATION);
				writer.WriteStartArray(this.SenderDelegations.Count);
				foreach (IHashable value in this.SenderDelegations)
				{
					writer.WriteHashableValue(value);
				}
				writer.WriteEndArray();
			}
			if (this.SenderSignature != null)
			{
				writer.WriteTextString(Properties.SENDER_SIGNATURE);
				writer.WriteByteString(this.SenderSignature);
			}
			writer.WriteEndMap();
		}

		internal class Properties
		{
			public const string CONTENT = "content";
			public const string SENDER_PUBLIC_KEY = "sender_pubkey";
			public const string SENDER_SIGNATURE = "sender_sig";
			public const string SENDER_DELEGATION = "sender_delegation";
		}
	}
}
