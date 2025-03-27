using EdjCase.ICP.Candid.Models.Types;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace EdjCase.ICP.Candid.Models.Values
{
	/// <summary>
	/// The options for candid value types
	/// </summary>
	public enum CandidValueType
	{
		/// <summary>
		/// Primitive/simple candid types like nat, int, null, etc...
		/// </summary>
		Primitive,
		/// <summary>
		/// An array of values
		/// </summary>
		Vector,
		/// <summary>
		/// A value with a set of fields, each with a name/id and value
		/// </summary>
		Record,
		/// <summary>
		/// A value with a chosen option name/id and value
		/// </summary>
		Variant,
		/// <summary>
		/// A function located in a service
		/// </summary>
		Func,
		/// <summary>
		/// A location with a set of functions to call
		/// </summary>
		Service,
		/// <summary>
		/// A value that is either null or a value
		/// </summary>
		Optional,
		/// <summary>
		/// An identifier value used for canister ids and identity ids
		/// </summary>
		Principal
	}

	/// <summary>
	/// The base class for all candid value
	/// </summary>
	public abstract class CandidValue : IEquatable<CandidValue>
	{
		/// <summary>
		/// The type of candid value is implemented
		/// </summary>
		public abstract CandidValueType Type { get; }

		internal abstract void EncodeValue(CandidType type, Func<CandidId, CandidCompoundType> getReferencedType, IBufferWriter<byte> destination);

		/// <inheritdoc />
		public abstract override int GetHashCode();

		/// <inheritdoc />
		public abstract bool Equals(CandidValue? other);

		/// <inheritdoc />
		public abstract override string ToString();


		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			if (obj is CandidValue v)
			{
				return this.Equals(v);
			}
			return false;
		}

		/// <inheritdoc />
		public static bool operator ==(CandidValue v1, CandidValue v2)
		{
			return v1.Equals(v2);
		}

		/// <inheritdoc />
		public static bool operator !=(CandidValue v1, CandidValue v2)
		{
			return !v1.Equals(v2);
		}

		/// <summary>
		/// Casts the candid value to a primitive type. If the type is not a primitive, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a primitive</exception>
		/// <returns>A primitive value</returns>
		public CandidPrimitive AsPrimitive()
		{
			return this.As<CandidPrimitive>(CandidValueType.Primitive);
		}

		private CandidPrimitive AsPrimitiveSafeError(PrimitiveType primitiveType)
		{
			CandidPrimitive? p = this.AsSafe<CandidPrimitive>(CandidValueType.Primitive);
			if (p is null)
			{
				throw new InvalidOperationException($"Cannot convert candid type '{this.Type}' to candid primitive type '{primitiveType}'");
			}
			return p;
		}

		/// <summary>
		/// Casts the candid value to a vector type. If the type is not a vector, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a vector</exception>
		/// <returns>A vector value</returns>
		public CandidVector AsVector()
		{
			return this.As<CandidVector>(CandidValueType.Vector);
		}

		/// <summary>
		/// Casts the candid value to a vector type and maps it to a List. If the type is not a vector,
		/// will throw an exception
		/// </summary>
		/// <typeparam name="T">Type to convert the candid value to</typeparam>
		/// <param name="converter">The conversion function from candid value to T</param>
		/// <exception cref="InvalidOperationException">Throws if the type is not a vector</exception>
		/// <returns>A list form of the vector</returns>
		public List<T> AsVectorAsList<T>(Func<CandidValue, T> converter)
		{
			CandidVector vector = this.AsVector();
			return vector.Values
				.Select(v => converter(v))
				.ToList();
		}


		/// <summary>
		/// Casts the candid value to a vector type and maps it to an array. If the type is not a vector,
		/// will throw an exception
		/// </summary>
		/// <param name="converter">The conversion function from candid value to T</param>
		/// <exception cref="InvalidOperationException">Throws if the type is not a vector</exception>
		/// <returns>An array form of the vector</returns>
		public T[] AsVectorAsArray<T>(Func<CandidValue, T> converter)
		{
			CandidVector vector = this.AsVector();
			return vector.Values
				.Select(v => converter(v))
				.ToArray();
		}

		/// <summary>
		/// Checks if the value is null
		/// </summary>
		/// <returns>Returns true if the value is null, otherwise false</returns>
		public bool IsNull()
		{
			return this is CandidPrimitive p && p.ValueType == PrimitiveType.Null;
		}

		/// <summary>
		/// Casts the candid value to a record type. If the type is not a record, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a record</exception>
		/// <returns>A record value</returns>
		public CandidRecord AsRecord()
		{
			return this.As<CandidRecord>(CandidValueType.Record);
		}


		/// <summary>
		/// Casts the candid value to a record type and maps to a generic type. If the type is not a record,
		/// will throw an exception
		/// </summary>
		/// <typeparam name="T">Type to convert the candid value to</typeparam>
		/// <param name="converter">The conversion function from candid record to T</param>
		/// <exception cref="InvalidOperationException">Throws if the type is not a record</exception>
		/// <returns>A generic value</returns>
		public T AsRecord<T>(Func<CandidRecord, T> converter)
		{
			CandidRecord record = this.AsRecord();
			return converter(record);
		}

		/// <summary>
		/// Casts the candid value to a variant type. If the type is not a variant, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a variant</exception>
		/// <returns>A variant value</returns>
		public CandidVariant AsVariant()
		{
			return this.As<CandidVariant>(CandidValueType.Variant);
		}

		/// <summary>
		/// Casts the candid value to a variant type and maps to a generic type. If the type is not a variant,
		/// will throw an exception
		/// </summary>
		/// <typeparam name="T">Type to convert the candid value to</typeparam>
		/// <param name="converter">The conversion function from candid variant to T</param>
		/// <exception cref="InvalidOperationException">Throws if the type is not a variant</exception>
		/// <returns>A generic value</returns>
		public T AsVariant<T>(Func<CandidVariant, T> converter)
		{
			CandidVariant v = this.AsVariant();
			return converter(v);
		}

		/// <summary>
		/// Casts the candid value to a func type. If the type is not a func, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a func</exception>
		/// <returns>A func value</returns>
		public CandidFunc AsFunc()
		{
			return this.As<CandidFunc>(CandidValueType.Func);
		}

		/// <summary>
		/// Casts the candid value to a service type. If the type is not a service, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a service</exception>
		/// <returns>A service value</returns>
		public CandidService AsService()
		{
			return this.As<CandidService>(CandidValueType.Service);
		}

		/// <summary>
		/// Casts the candid value to an optional. If the type is not an optional, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not an optional</exception>
		/// <returns>An optional value</returns>
		public CandidOptional AsOptional()
		{
			return this.As<CandidOptional>(CandidValueType.Optional);
		}

		/// <summary>
		/// Casts the candid value to an opt type and maps to a generic type. If the type is not an opt,
		/// will throw an exception
		/// </summary>
		/// <typeparam name="T">Type to convert the candid value to</typeparam>
		/// <exception cref="InvalidOperationException">Throws if the type is not an opt</exception>
		/// <returns>A generic value wrapped in an `OptionalValue`</returns>
		public OptionalValue<T> AsOptional<T>(Func<CandidValue, T> valueConverter)
		{
			CandidOptional? optional = this.AsOptional();
			if (optional.Value is CandidPrimitive p && p.ValueType == PrimitiveType.Null)
			{
				return OptionalValue<T>.NoValue();
			}
			T value = valueConverter(optional.Value);
			return OptionalValue<T>.WithValue(value);
		}

		/// <summary>
		/// Casts the candid value to a text type. If the type is not text, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not text</exception>
		/// <returns>A text value</returns>
		public string? AsText()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Text).AsText();
		}

		/// <summary>
		/// Casts the candid value to a nat type. If the type is not a nat, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a nat</exception>
		/// <returns>An unbounded nat value</returns>
		public UnboundedUInt AsNat()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Nat).AsNat();
		}

		/// <summary>
		/// Casts the candid value to a nat8 type. If the type is not a nat8, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a nat8</exception>
		/// <returns>A nat8 value</returns>
		public byte AsNat8()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Nat8).AsNat8();
		}

		/// <summary>
		/// Casts the candid value to a nat16 type. If the type is not a nat16, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a nat16</exception>
		/// <returns>A nat16 value</returns>
		public ushort AsNat16()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Nat16).AsNat16();
		}

		/// <summary>
		/// Casts the candid value to a nat32 type. If the type is not a nat32, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a nat32</exception>
		/// <returns>A nat32 value</returns>
		public uint AsNat32()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Nat32).AsNat32();
		}

		/// <summary>
		/// Casts the candid value to a nat64 type. If the type is not a nat64, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a nat64</exception>
		/// <returns>A nat64 value</returns>
		public ulong AsNat64()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Nat64).AsNat64();
		}

		/// <summary>
		/// Casts the candid value to an int type. If the type is not an int, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not an int</exception>
		/// <returns>An unbounded int value</returns>
		public UnboundedInt AsInt()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Int).AsInt();
		}

		/// <summary>
		/// Casts the candid value to an int8 type. If the type is not an int8, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not an int8</exception>
		/// <returns>An int8 value</returns>
		public sbyte AsInt8()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Int8).AsInt8();
		}


		/// <summary>
		/// Casts the candid value to an int16 type. If the type is not an int16, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not an int16</exception>
		/// <returns>An int16 value</returns>
		public short AsInt16()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Int16).AsInt16();
		}


		/// <summary>
		/// Casts the candid value to an int32 type. If the type is not an int32, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not an int32</exception>
		/// <returns>An int32 value</returns>
		public int AsInt32()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Int32).AsInt32();
		}


		/// <summary>
		/// Casts the candid value to an int64 type. If the type is not an int64, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not an int64</exception>
		/// <returns>An int64 value</returns>
		public long AsInt64()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Int64).AsInt64();
		}

		/// <summary>
		/// Casts the candid value to a float32 type. If the type is not a float32, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a float32</exception>
		/// <returns>A float32 value</returns>
		public float AsFloat32()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Float32).AsFloat32();
		}

		/// <summary>
		/// Casts the candid value to a float64 type. If the type is not a float64, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a float64</exception>
		/// <returns>A float64 value</returns>
		public double AsFloat64()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Float64).AsFloat64();
		}

		/// <summary>
		/// Casts the candid value to a bool type. If the type is not a bool, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a bool</exception>
		/// <returns>A bool value</returns>
		public bool AsBool()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Bool).AsBool();
		}

		/// <summary>
		/// Casts the candid value to a principal type. If the type is not a principal, will throw an exception
		/// </summary>
		/// <exception cref="InvalidOperationException">Throws if the type is not a principal</exception>
		/// <returns>A principal value</returns>
		public Principal? AsPrincipal()
		{
			return this.AsPrimitiveSafeError(PrimitiveType.Principal).AsPrincipal();
		}

		/// <summary>
		/// Helper method to create a text value from a string
		/// </summary>
		/// <param name="value">A string value to convert to a candid text value</param>
		/// <returns>Candid text value</returns>
		public static CandidPrimitive Text(string value)
		{
			return new CandidPrimitive(PrimitiveType.Text, value);
		}

		/// <summary>
		/// Helper method to create a nat value from an unbounded usigned integer
		/// </summary>
		/// <param name="value">A unbounded usigned integer value to convert to a candid nat value</param>
		/// <returns>Candid nat value</returns>
		public static CandidPrimitive Nat(UnboundedUInt value)
		{
			return new CandidPrimitive(PrimitiveType.Nat, value);
		}

		/// <summary>
		/// Helper method to create a nat8 value from a byte
		/// </summary>
		/// <param name="value">A byte value to convert to a candid nat8</param>
		/// <returns>Candid nat8 value</returns>
		public static CandidPrimitive Nat8(byte value)
		{
			return new CandidPrimitive(PrimitiveType.Nat8, value);
		}

		/// <summary>
		/// Helper method to create a nat16 value from a unsigned short integer
		/// </summary>
		/// <param name="value">A unsigned short integer value to convert to a candid nat16</param>
		/// <returns>Candid nat16 value</returns>
		public static CandidPrimitive Nat16(ushort value)
		{
			return new CandidPrimitive(PrimitiveType.Nat16, value);
		}

		/// <summary>
		/// Helper method to create a nat32 value from a unsigned integer
		/// </summary>
		/// <param name="value">A unsigned integer value to convert to a candid nat32</param>
		/// <returns>Candid nat32 value</returns>
		public static CandidPrimitive Nat32(uint value)
		{
			return new CandidPrimitive(PrimitiveType.Nat32, value);
		}

		/// <summary>
		/// Helper method to create a nat64 value from a unsigned long integer
		/// </summary>
		/// <param name="value">A unsigned long integer value to convert to a candid nat64</param>
		/// <returns>Candid nat64 value</returns>
		public static CandidPrimitive Nat64(ulong value)
		{
			return new CandidPrimitive(PrimitiveType.Nat64, value);
		}

		/// <summary>
		/// Helper method to create a int value from an unbounded integer
		/// </summary>
		/// <param name="value">A unbounded integer value to convert to a candid int value</param>
		/// <returns>Candid int value</returns>
		public static CandidPrimitive Int(UnboundedInt value)
		{
			return new CandidPrimitive(PrimitiveType.Int, value);
		}

		/// <summary>
		/// Helper method to create a int8 value from a signed byte
		/// </summary>
		/// <param name="value">A signed byte value to convert to a candid int8</param>
		/// <returns>Candid int8 value</returns>
		public static CandidPrimitive Int8(sbyte value)
		{
			return new CandidPrimitive(PrimitiveType.Int8, value);
		}

		/// <summary>
		/// Helper method to create a int16 value from a short integer
		/// </summary>
		/// <param name="value">A short integer value to convert to a candid int16</param>
		/// <returns>Candid int16 value</returns>
		public static CandidPrimitive Int16(short value)
		{
			return new CandidPrimitive(PrimitiveType.Int16, value);
		}

		/// <summary>
		/// Helper method to create a int32 value from an integer
		/// </summary>
		/// <param name="value">An integer value to convert to a candid int32</param>
		/// <returns>Candid int32 value</returns>
		public static CandidPrimitive Int32(int value)
		{
			return new CandidPrimitive(PrimitiveType.Int32, value);
		}

		/// <summary>
		/// Helper method to create a int64 value from an long integer
		/// </summary>
		/// <param name="value">An long integer value to convert to a candid int64</param>
		/// <returns>Candid int64 value</returns>
		public static CandidPrimitive Int64(long value)
		{
			return new CandidPrimitive(PrimitiveType.Int64, value);
		}

		/// <summary>
		/// Helper method to create a float32 value from a float
		/// </summary>
		/// <param name="value">An float value to convert to a candid float32</param>
		/// <returns>Candid float32 value</returns>
		public static CandidPrimitive Float32(float value)
		{
			return new CandidPrimitive(PrimitiveType.Float32, value);
		}

		/// <summary>
		/// Helper method to create a float64 value from a double
		/// </summary>
		/// <param name="value">An double value to convert to a candid float64</param>
		/// <returns>Candid float64 value</returns>
		public static CandidPrimitive Float64(double value)
		{
			return new CandidPrimitive(PrimitiveType.Float64, value);
		}

		/// <summary>
		/// Helper method to create a bool value from a bool
		/// </summary>
		/// <param name="value">An bool value to convert to a candid bool</param>
		/// <returns>Candid bool value</returns>
		public static CandidPrimitive Bool(bool value)
		{
			return new CandidPrimitive(PrimitiveType.Bool, value);
		}

		/// <summary>
		/// Helper method to create a principal value from a principal
		/// </summary>
		/// <param name="value">An principal value to convert to a candid principal</param>
		/// <returns>Candid principal value</returns>
		public static CandidPrimitive Principal(Principal value)
		{
			return new CandidPrimitive(PrimitiveType.Principal, value);
		}

		/// <summary>
		/// Helper method to create a blob/vec nat8 value from a byte array
		/// </summary>
		/// <param name="value">A byte array to convert to a blob/vec nat8</param>
		/// <returns>Candid vector value of nat8's</returns>
		public static CandidVector Blob(byte[] value)
		{
			return new CandidVector(
				value
				.Select(CandidValue.Nat8)
				.ToArray()
			);
		}


		/// <summary>
		/// Helper method to create a null value
		/// </summary>
		/// <returns>Candid null value</returns>
		public static CandidPrimitive Null()
		{
			return new CandidPrimitive(PrimitiveType.Null, null);
		}

		/// <summary>
		/// Helper method to create a reserved value
		/// </summary>
		/// <returns>Candid reserved value</returns>
		public static CandidPrimitive Reserved()
		{
			return new CandidPrimitive(PrimitiveType.Reserved, null);
		}

		/// <summary>
		/// Helper method to create an empty value
		/// </summary>
		/// <returns>Candid empty value</returns>
		public static CandidPrimitive Empty()
		{
			return new CandidPrimitive(PrimitiveType.Empty, null);
		}

		internal static T DereferenceType<T>(
			CandidType type,
			Func<CandidId, CandidCompoundType> getReferencedType)
			where T : CandidType
		{
			if (type is CandidReferenceType r)
			{
				type = getReferencedType(r.Id);
			}
			if (type is T t)
			{
				return t;
			}
			throw new Exception($"Expected type {typeof(T)} but got {type}. Candid value and type definition are incompatible.");
		}

		private T As<T>(CandidValueType type)
			where T : CandidValue
		{
			T? value = this.AsSafe<T>(type);
			if (value is null)
			{
				throw new InvalidOperationException($"Cannot convert candid type '{this.Type}' to candid type '{type}'");
			}
			return value;
		}

		private T? AsSafe<T>(CandidValueType type)
			where T : CandidValue
		{
			if (this.Type != type)
			{
				if (this.Type == CandidValueType.Optional)
				{
					// If opt X, coerce to just X
					CandidOptional o = this.AsOptional();

					return o.Value.As<T>(type);
				}
				return null;
			}
			return (T)this;
		}
	}
}
