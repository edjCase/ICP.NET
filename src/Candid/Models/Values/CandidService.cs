using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Utilities;
using System;
using System.Buffers;
using System.Linq;

namespace EdjCase.ICP.Candid.Models.Values
{
	/// <summary>
	/// A model that represents a candid service value
	/// </summary>
	public class CandidService : CandidValue
	{
		/// <inheritdoc />
		public override CandidValueType Type { get; } = CandidValueType.Service;

		/// <summary>
		/// True if the candid func definition is an opaque (non standard/system specific definition),
		/// otherwise false
		/// </summary>
		public bool IsOpqaueReference { get; }

		private readonly Principal? principalId;

		/// <param name="principalId">The id of the canister where the service lives</param>
		public CandidService(Principal principalId)
		{
			this.principalId = principalId ?? throw new ArgumentNullException(nameof(principalId));
			this.IsOpqaueReference = false;
		}

		private CandidService()
		{
			this.principalId = null;
			this.IsOpqaueReference = true;
		}

		/// <summary>
		/// Gets the prinicipal of the candid service. If it is an opaque reference, then an exception will
		/// be thrown
		/// </summary>
		/// <returns>Pricipal of the candid service</returns>
		/// <exception cref="InvalidOperationException">Throws if service is an opaque reference</exception>
		public Principal GetPrincipal()
		{
			if (this.IsOpqaueReference)
			{
				throw new InvalidOperationException("Cannot get principal from opaque service reference.");
			}
			return this.principalId!;
		}

		/// <inheritdoc />
		internal override void EncodeValue(CandidType type, Func<CandidId, CandidCompoundType> getReferencedType, IBufferWriter<byte> destination)
		{
			if (this.IsOpqaueReference)
			{
				destination.WriteOne<byte>(0);
				return;
			}
			CandidValue.Principal(this.principalId!).EncodeValue(CandidType.Principal(), getReferencedType, destination);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return HashCode.Combine(this.IsOpqaueReference, this.principalId);
		}

		/// <inheritdoc />
		public override bool Equals(CandidValue? other)
		{
			if (other is CandidService s)
			{
				if (this.IsOpqaueReference != s.IsOpqaueReference)
				{
					return false;
				}
				if (this.IsOpqaueReference)
				{
					// TODO can we ever tell if they are the same? do we care?
					return false;
				}
				return this.principalId == s.principalId;
			}
			return false;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return this.IsOpqaueReference
				? "(Opaque Reference)"
				: this.principalId!.ToString();
		}

		/// <summary>
		/// Helper method to create an opaque service reference where the id/location 
		/// of the service is non-standard/system specific
		/// </summary>
		/// <returns></returns>
		public static CandidService OpaqueReference()
		{
			return new CandidService();
		}
	}
}