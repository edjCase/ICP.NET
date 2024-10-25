using EdjCase.ICP.Candid.Encodings;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace EdjCase.ICP.Candid.Models.Types
{
	/// <summary>
	/// A candid type that is not primitive or a reference. These types are considered
	/// more complex and have multiple data structures within them
	/// </summary>
	public abstract class CandidCompoundType : CandidKnownType
	{
		/// <summary>
		/// Optional. Used if this type can be referenced by an inner type recursively.
		/// The inner type will use `CandidReferenceType` with this id
		/// </summary>
		public CandidId? RecursiveId { get; set; }

		/// <param name="recursiveId">Optional. Used if this type can be referenced by an inner type recursively.
		/// The inner type will use `CandidReferenceType` with this id</param>
		public CandidCompoundType(CandidId? recursiveId)
		{
			this.RecursiveId = recursiveId;
		}


		internal abstract void EncodeInnerTypes(CompoundTypeTable compoundTypeTable, IBufferWriter<byte> destination);

		internal abstract IEnumerable<CandidType> GetInnerTypes();

		internal override void Encode(CompoundTypeTable compoundTypeTable, IBufferWriter<byte> destination)
		{
			UnboundedUInt index = compoundTypeTable.GetOrThrow(this);
			LEB128.EncodeSigned(index, destination); // Encode index on where it is in the type table
		}
	}

	/// <summary>
	/// A shared class for candid records and variants. Both have a mapping of 
	/// keys with associated types
	/// </summary>
	public abstract class CandidRecordOrVariantType : CandidCompoundType
	{
		/// <inheritdoc />
		public override abstract CandidTypeCode Type { get; }

		/// <summary>
		/// The string name of the parent type (record or variant)
		/// </summary>
		protected abstract string TypeString { get; }

		/// <summary>
		/// Gets the record fields or variant options to be used for encoding
		/// </summary>
		/// <returns></returns>
		protected abstract Dictionary<CandidTag, CandidType> GetFieldsOrOptions();

		/// <inheritdoc />
		protected CandidRecordOrVariantType(CandidId? recursiveId) : base(recursiveId)
		{

		}

		internal override void EncodeInnerTypes(CompoundTypeTable compoundTypeTable, IBufferWriter<byte> destination)
		{
			Dictionary<CandidTag, CandidType> fieldsOrOptions = this.GetFieldsOrOptions();

			LEB128.EncodeSigned(fieldsOrOptions.Count, destination); // Encode field/option count

			foreach (var f in fieldsOrOptions.OrderBy(f => f.Key))
			{
				LEB128.EncodeUnsigned(f.Key.Id, destination); // Encode key
				f.Value.Encode(compoundTypeTable, destination); // Encode value
			}
		}

		internal override IEnumerable<CandidType> GetInnerTypes()
		{
			return this.GetFieldsOrOptions().Values;
		}

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			Dictionary<CandidTag, CandidType> fieldsOrOptions = this.GetFieldsOrOptions();
			bool exactType = this.GetType() == obj?.GetType();
			if (exactType && obj is CandidRecordOrVariantType def)
			{
				Dictionary<CandidTag, CandidType> otherFieldsOrOptions = def.GetFieldsOrOptions();
				if (fieldsOrOptions.Count != otherFieldsOrOptions.Count)
				{
					return false;
				}
				foreach ((CandidTag fLabel, CandidType fDef) in fieldsOrOptions)
				{
					if (!otherFieldsOrOptions.TryGetValue(fLabel, out CandidType? otherFDef))
					{
						return false;
					}
					if (fDef != otherFDef)
					{
						return false;
					}
				}
				return true;
			}
			return false;
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			Dictionary<CandidTag, CandidType> fieldsOrOptions = this.GetFieldsOrOptions();
			return HashCode.Combine(this.Type, fieldsOrOptions);
		}
	}
}
