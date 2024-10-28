using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using EdjCase.ICP.Candid.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace EdjCase.ICP.Candid.Mapping.Mappers
{
	internal class VariantMapper : ICandidValueMapper
	{
		public CandidType CandidType { get; }
		public Type Type { get; }
		public PropertyInfo TypeProperty { get; }
		public PropertyInfo ValueProperty { get; }
		public Dictionary<CandidTag, Option> Options { get; }
		public Dictionary<Enum, CandidTag> EnumMapping { get; }

		public VariantMapper(
			CandidVariantType candidType,
			Type type,
			PropertyInfo typeProperty,
			PropertyInfo valueProperty,
			Dictionary<CandidTag, Option> options)
		{
			this.CandidType = candidType ?? throw new ArgumentNullException(nameof(candidType));
			this.Type = type ?? throw new ArgumentNullException(nameof(type));
			this.TypeProperty = typeProperty;
			this.ValueProperty = valueProperty;
			this.Options = options;
			this.EnumMapping = options.ToDictionary(k => k.Value.EnumValue, k => k.Key);
		}

		public object Map(CandidValue value, CandidConverter converter)
		{
			CandidVariant variant = value.AsVariant();

			if (!this.Options.TryGetValue(variant.Tag, out Option optionInfo))
			{
				throw new Exception($"Could not map candid variant to type '{this.Type}'. Type is missing option '{variant.Tag}'");
			}
			object? variantValue;
			if (typeof(CandidValue).IsAssignableFrom(optionInfo.Type))
			{
				// If the type is a candid value, just set it
				variantValue = variant.Value;
			}
			else
			{
				if (optionInfo.Type != null)
				{
					if (optionInfo.UseOptionalOverride && variant.Value is CandidOptional o)
					{
						// If UseOptionalOverride, then unwrap the inner value
						if (o.Value.IsNull())
						{
							// If null, no need to convert
							variantValue = null;
						}
						else
						{
							Type t = optionInfo.Type;
							if (t.IsGenericType
								&& t.GetGenericTypeDefinition() == typeof(Nullable<>))
							{
								// Get T of Nullable<T>
								t = t.GetGenericArguments()[0];
							}
							// Use inner value
							variantValue = converter.ToObject(t, o.Value);
						}
					}
					else
					{
						// Treat like normal
						variantValue = converter.ToObject(optionInfo.Type, variant.Value);
					}
				}
				else
				{
					variantValue = null;
				}
			}

			object obj = Activator.CreateInstance(this.Type, nonPublic: true);
			this.TypeProperty.SetValue(obj, optionInfo.EnumValue);
			this.ValueProperty.SetValue(obj, variantValue);
			return obj;
		}

		public CandidValue Map(object obj, CandidConverter converter)
		{
			Enum enumValue = (Enum)this.TypeProperty.GetValue(obj);
			CandidTag innerTag = this.EnumMapping[enumValue];
			object? innerObj = this.ValueProperty.GetValue(obj);

			if (!this.Options.TryGetValue(innerTag, out Option optionInfo))
			{
				throw new Exception($"Could not map type '{this.Type}' to a candid variant. Variant is missing option '{innerTag}'");
			}


			CandidValue innerValue;
			if (innerObj is CandidValue value)
			{
				// If the object is a candid value, just use it
				innerValue = value;
			}
			else
			{
				if (optionInfo.Type == null)
				{
					// If typeless, always set to null
					innerValue = CandidValue.Null();
				}
				else if (innerObj == null)
				{
					// If null and has a type, should always be a null opt 
					innerValue = new CandidOptional(null);
				}
				else
				{
					// Convert
					innerValue = converter.FromObject(innerObj);
					if (optionInfo.UseOptionalOverride || optionInfo.IsGenericNullable)
					{
						// Wrap in candid optional
						innerValue = new CandidOptional(innerValue);
					}
				}
			}
			return new CandidVariant(innerTag, innerValue);
		}

		public CandidType? GetMappedCandidType(Type type)
		{
			return this.CandidType;
		}

		public class Option
		{
			public Enum EnumValue { get; }
			public Type? Type { get; }
			public bool UseOptionalOverride { get; }
			public CandidType? CandidType { get; }

			public Option(Enum enumValue, Type? type, bool useOptionalOverride, CandidType? candidType)
			{
				this.EnumValue = enumValue ?? throw new ArgumentNullException(nameof(enumValue));
				this.Type = type;
				this.UseOptionalOverride = useOptionalOverride;
				this.CandidType = candidType;
			}


			private bool? isGenericNullableCache;

			public bool IsGenericNullable
			{
				get
				{
					{
						if (!this.isGenericNullableCache.HasValue)
						{
							this.isGenericNullableCache = this.Type != null && this.Type.IsGenericType
							&& this.Type.GetGenericTypeDefinition() == typeof(Nullable<>);
						}
						return this.isGenericNullableCache.Value;
					}
				}
			}
		}
	}
}
