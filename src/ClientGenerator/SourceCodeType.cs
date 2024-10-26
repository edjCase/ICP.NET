using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.ClientGenerator;
using System;
using System.Collections.Generic;

namespace EdjCase.ICP.ClientGenerator
{
	internal abstract class SourceCodeType
	{
		public abstract bool IsPredefinedType { get; }

	}

	internal class RawCandidType : SourceCodeType
	{
		public Candid.Models.Types.CandidType CandidType { get; }
		public override bool IsPredefinedType { get; } = true;

		public RawCandidType(Candid.Models.Types.CandidType candidType)
		{
			this.CandidType = candidType ?? throw new ArgumentNullException(nameof(candidType));
		}
	}

	internal class NonGenericSourceCodeType : SourceCodeType
	{
		public Type Type { get; }
		public override bool IsPredefinedType { get; } = true;

		public NonGenericSourceCodeType(Type primitiveType)
		{
			this.Type = primitiveType;
		}
	}

	internal class ArraySourceCodeType : SourceCodeType
	{
		public SourceCodeType? ElementType { get; }

		public override bool IsPredefinedType { get; } = true;

		public ArraySourceCodeType(SourceCodeType? elementType)
		{
			this.ElementType = elementType;
		}
	}

	internal class ListSourceCodeType : SourceCodeType
	{
		public SourceCodeType ElementType { get; }

		public override bool IsPredefinedType { get; }

		public ListSourceCodeType(SourceCodeType genericType, bool usePredefined)
		{
			this.ElementType = genericType;
			this.IsPredefinedType = usePredefined;
		}
	}

	internal class OptionalValueSourceCodeType : SourceCodeType
	{
		public SourceCodeType GenericType { get; }

		public override bool IsPredefinedType { get; }

		public OptionalValueSourceCodeType(SourceCodeType genericType, bool usePredefined)
		{
			this.GenericType = genericType;
			this.IsPredefinedType = usePredefined;
		}
	}

	internal class DictionarySourceCodeType : SourceCodeType
	{
		public SourceCodeType KeyType { get; }
		public SourceCodeType ValueType { get; }

		public override bool IsPredefinedType { get; }

		public DictionarySourceCodeType(SourceCodeType keyType, SourceCodeType valueType, bool usePredefined)
		{
			this.KeyType = keyType;
			this.ValueType = valueType;
			this.IsPredefinedType = usePredefined;
		}
	}

	internal class ReferenceSourceCodeType : SourceCodeType
	{
		public CandidId Id { get; }

		public override bool IsPredefinedType { get; } = true;
		public ReferenceSourceCodeType(CandidId id)
		{
			this.Id = id ?? throw new ArgumentNullException(nameof(id));
		}


	}

	internal class TupleSourceCodeType : SourceCodeType
	{
		public List<SourceCodeType> Fields { get; }

		public override bool IsPredefinedType { get; } = true;

		public TupleSourceCodeType(List<SourceCodeType> fields)
		{
			this.Fields = fields ?? throw new ArgumentNullException(nameof(fields));
		}
	}


	internal class RecordSourceCodeType : SourceCodeType
	{
		public List<RecordField> Fields { get; }

		public override bool IsPredefinedType { get; } = false;

		public RecordSourceCodeType(List<RecordField> fields)
		{
			this.Fields = fields ?? throw new ArgumentNullException(nameof(fields));
		}

		public class RecordField
		{
			public ResolvedName Tag { get; }
			public SourceCodeType Type { get; }
			public bool OptionalOverridden { get; }
			public RecordField(ResolvedName tag, SourceCodeType type, bool optionalOverridden)
			{
				this.Tag = tag ?? throw new ArgumentNullException(nameof(tag));
				this.Type = type ?? throw new ArgumentNullException(nameof(type));
				this.OptionalOverridden = optionalOverridden;
			}
		}
	}

	internal class VariantSourceCodeType : SourceCodeType
	{
		public List<VariantOption> Options { get; }
		public override bool IsPredefinedType { get; } = false;

		public VariantSourceCodeType(List<VariantOption> options)
		{
			this.Options = options ?? throw new ArgumentNullException(nameof(options));
		}


		public class VariantOption
		{
			public ResolvedName Tag { get; }
			public SourceCodeType? Type { get; }
			public bool OptionalOverridden { get; }
			public VariantOption(ResolvedName tag, SourceCodeType? type, bool optionalOverridden)
			{
				this.Tag = tag ?? throw new ArgumentNullException(nameof(tag));
				this.Type = type;
				this.OptionalOverridden = optionalOverridden;
			}
		}
	}

	internal class ServiceSourceCodeType : SourceCodeType
	{
		public List<(string CsharpName, string CandidName, Func FuncInfo)> Methods { get; set; }
		public override bool IsPredefinedType { get; } = false;
		public ServiceSourceCodeType(List<(string CsharpName, string CandidName, Func FuncInfo)> methods)
		{
			this.Methods = methods ?? throw new ArgumentNullException(nameof(methods));
		}

		public class Func
		{
			public List<(ResolvedName Name, SourceCodeType Type)> ArgTypes { get; internal set; }
			public List<(ResolvedName Name, SourceCodeType Type)> ReturnTypes { get; internal set; }
			public bool IsOneway { get; set; }
			public bool IsQuery { get; set; }

			public Func(
				List<(ResolvedName Name, SourceCodeType Type)> argTypes,
				List<(ResolvedName Name, SourceCodeType Type)> returnTypes,
				bool isOneway,
				bool isQuery
			)
			{
				this.ArgTypes = argTypes ?? throw new ArgumentNullException(nameof(argTypes));
				this.ReturnTypes = returnTypes ?? throw new ArgumentNullException(nameof(returnTypes));
				this.IsOneway = isOneway;
				this.IsQuery = isQuery;
			}
		}
	}
}
