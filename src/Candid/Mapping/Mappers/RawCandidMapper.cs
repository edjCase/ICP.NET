using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using System;

namespace EdjCase.ICP.Candid.Mapping.Mappers
{
	internal class RawCandidMapper : ICandidValueMapper
	{
		public CandidType CandidType { get; }

		public RawCandidMapper(CandidType candidType)
		{
			this.CandidType = candidType ?? throw new ArgumentNullException(nameof(candidType));
		}

		public object Map(CandidValue value, CandidConverter converter)
		{
			return value;
		}

		public CandidValue Map(object value, CandidConverter converter)
		{
			return (CandidValue)value;
		}

		public CandidType? GetMappedCandidType(Type type)
		{
			return this.CandidType;
		}
	}
}