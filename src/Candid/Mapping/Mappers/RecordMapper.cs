using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using EdjCase.ICP.Candid.Models;
using System;
using System.Collections.Generic;

namespace EdjCase.ICP.Candid.Mapping.Mappers
{

	internal class RecordMapper : ICandidValueMapper
	{
		public CandidRecordType CandidType { get; }
		public Type Type { get; }
		public Dictionary<CandidTag, PropertyMetaData> Properties { get; }

		public RecordMapper(CandidRecordType candidType, Type type, Dictionary<CandidTag, PropertyMetaData> properties)
		{
			this.CandidType = candidType ?? throw new ArgumentNullException(nameof(candidType));
			this.Type = type ?? throw new ArgumentNullException(nameof(type));
			this.Properties = properties;
		}

		public object Map(CandidValue value, CandidConverter converter)
		{
			CandidRecord record = value.AsRecord();
			object obj = Activator.CreateInstance(this.Type);
			foreach ((CandidTag tag, PropertyMetaData property) in this.Properties)
			{
				object? fieldValue;
				if (record.Fields.TryGetValue(tag, out CandidValue fieldCandidValue))
				{
					Type t = property.PropertyInfo.PropertyType;
					if (typeof(CandidValue).IsAssignableFrom(t))
					{
						// If the property is a candid value, just set it
						fieldValue = fieldCandidValue;
					}
					else
					{
						if (property.UseOptionalOverride
							&& fieldCandidValue is CandidOptional o
							&& o.Value.IsNull())
						{
							// If not using OptionalValue and the value is opt null,
							// that is the same as setting to null, so just skip
							fieldValue = null;
						}
						else
						{
							fieldValue = converter.ToObject(t, fieldCandidValue);
						}
					}

					if (fieldValue != null)
					{
						property.PropertyInfo.SetValue(obj, fieldValue);
					}
				}
				else
				{
					if (!this.CandidType.Fields.TryGetValue(tag, out CandidType fieldType))
					{
						// Record has property that is not in the candid type, skip
						continue;
					}
					if (fieldType is not CandidOptionalType && !property.UseOptionalOverride)
					{
						// Only throw if fieldType is not an opt value since the value is unset (null)
						// or has an extra
						throw new Exception($"Could not map candid record to type '{this.Type}'. Record is missing field '{tag}'");
					}
					// Don't set if the value is unspecified
				}
			}
			return obj;
		}

		public CandidValue Map(object value, CandidConverter converter)
		{
			Dictionary<CandidTag, CandidValue> fields = new();
			foreach ((CandidTag tag, PropertyMetaData property) in this.Properties)
			{
				object? propValue = property.PropertyInfo.GetValue(value);
				CandidValue v; if (propValue is CandidValue cv)
				{
					// If the property is a candid value, just set it
					v = cv;
				}
				else
				{
					if (propValue == null)
					{
						v = new CandidOptional(null);
					}
					else
					{
						v = converter.FromObject(propValue);
						if (property.UseOptionalOverride || property.IsGenericNullable)
						{
							// Wrap in candid optional if has override [CandidOptional]
							// or is Nullable<> (Nullable<T> gives just T if setting the value)
							v = new CandidOptional(v);
						}
					}
				}
				fields.Add(tag, v);
			}
			return new CandidRecord(fields);
		}

		public CandidType? GetMappedCandidType(Type type)
		{
			return this.CandidType;
		}
	}
}
