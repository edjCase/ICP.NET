using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Utilities;
using System;
using System.Buffers;

namespace EdjCase.ICP.Candid.Models.Values
{
	/// <summary>
	/// A model to represent the value of a candid func
	/// </summary>
	public class CandidFunc : CandidValue
	{
		/// <inheritdoc />
		public override CandidValueType Type { get; } = CandidValueType.Func;

		/// <summary>
		/// True if the candid func definition is an opaque (non standard/system specific definition),
		/// otherwise false
		/// </summary>
		public bool IsOpaqueReference { get; }

		/// <summary>
		/// Specifies the service and method of the func if is not an opaque reference, otherwise will be null
		/// </summary>
		public (CandidService Service, string Method)? ServiceInfo { get; }

		/// <param name="service">The candid service definition the function lives in</param>
		/// <param name="name">The name of the function</param>
		public CandidFunc(CandidService service, string name)
		{
			this.IsOpaqueReference = false;
			this.ServiceInfo = (service, name);
		}


		private CandidFunc()
		{
			this.IsOpaqueReference = true;
			this.ServiceInfo = null;
		}

		/// <inheritdoc />
		internal override void EncodeValue(CandidType type, Func<CandidId, CandidCompoundType> getReferencedType,
			IBufferWriter<byte> destination)
		{
			if (this.IsOpaqueReference)
			{
				destination.WriteOne<byte>(0);
				return;
			}

			CandidFuncType t = DereferenceType<CandidFuncType>(type, getReferencedType);
			(CandidService service, string method) = this.ServiceInfo!.Value;
			destination.WriteOne<byte>(1); // Encode bit to indicate it is not opaque
			service.EncodeValue(t, getReferencedType, destination); // Encode value
			destination.WriteUtf8LebAndValue(method); // Encode method name
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return HashCode.Combine(this.IsOpaqueReference, this.ServiceInfo);
		}

		/// <inheritdoc />
		public override bool Equals(CandidValue? other)
		{
			if (other is CandidFunc f)
			{
				if (this.IsOpaqueReference != f.IsOpaqueReference)
				{
					return false;
				}

				if (this.IsOpaqueReference)
				{
					// TODO can we tell if they are equal?
					return false;
				}

				return this.ServiceInfo == f.ServiceInfo;
			}

			return false;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			if (this.IsOpaqueReference)
			{
				return "(Opaque Reference)";
			}

			(CandidService service, string method) = this.ServiceInfo!.Value;
			return $"(Method: {method}, Service: {service})";
		}

		/// <summary>
		/// Creates an opaque reference to a function that is defined by the system
		/// vs being defined in candid
		/// </summary>
		/// <returns>A opaque candid func</returns>
		public static CandidFunc OpaqueReference()
		{
			return new CandidFunc();
		}
	}
}
