using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EdjCase.ICP.Agent.Identities
{
	/// <summary>
	/// Identity to use for requests to Internet Computer canisters
	/// </summary>
	public interface IIdentity
	{
		/// <summary>
		/// Returns the public key of the identity
		/// </summary>
		/// <returns>Public key of the identity</returns>
		public SubjectPublicKeyInfo GetPublicKey();

		/// <summary>
		/// Returns the principal of the identity
		/// </summary>
		/// <returns>Principal of the identity</returns>
		public Principal GetPrincipal() => this.GetPublicKey().ToPrincipal();

		/// <summary>
		/// Gets the signed delegations for the identity.
		/// Delegations will exist if the identity is a delegated identity
		/// instead of having the raw keys. This is used in Internet Identity
		/// </summary>
		/// <returns>The signed delegations, otherwise an empty list</returns>
		public List<SignedDelegation>? GetSenderDelegations();

		/// <summary>
		/// Signs the specified bytes with the identity key
		/// </summary>
		/// <param name="data">The byte data to sign</param>
		/// <returns>The signature bytes of the specified data bytes</returns>
		public byte[] Sign(byte[] data);


		/// <summary>
		/// Signs the hashable content
		/// </summary>
		/// <param name="content">The data that needs to be signed</param>
		/// <returns>The content with signature(s) from the identity</returns>
		public SignedContent<TRequest> SignContent<TRequest>(TRequest content)
			where TRequest : IRepresentationIndependentHashItem
		{
			SubjectPublicKeyInfo senderPublicKey = this.GetPublicKey();
			var sha256 = SHA256HashFunction.Create();
			byte[] contentHash = content.BuildHashableItem().ToHashable().ComputeHash(sha256);
			RequestId requestId = RequestId.FromBytes(contentHash);
			byte[] domainSeparator = Encoding.UTF8.GetBytes("\x0Aic-request");
			byte[] senderSignature = this.Sign(domainSeparator.Concat(contentHash).ToArray());
			List<SignedDelegation>? senderDelegations = this.GetSenderDelegations();
			return new SignedContent<TRequest>(requestId, content, senderPublicKey, senderDelegations, senderSignature);
		}
	}
}
