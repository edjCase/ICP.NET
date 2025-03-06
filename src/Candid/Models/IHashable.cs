using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EdjCase.ICP.Candid.Crypto;

namespace EdjCase.ICP.Candid.Models
{
	/// <summary>
	/// An interface to specify if a class can be hashed by the `IHashFunction`
	/// </summary>
	public interface IHashable
	{
		/// <summary>
		/// Computes the hash for the object using on the hash function
		/// </summary>
		/// <param name="hashFunction">A hash function algorithm to use to hash the object</param>
		/// <returns>A byte array of the hash value</returns>
		byte[] ComputeHash(IHashFunction hashFunction);
	}

	/// <summary>
	/// An interface to specify a representation independent model that can be hashed
	/// </summary>
	public interface IRepresentationIndependentHashItem
	{
		/// <summary>
		/// Builds a mapping of fields to hashable values
		/// </summary>
		/// <returns>Dictionary of field name to hashable field value</returns>
		Dictionary<string, IHashable> BuildHashableItem();
	}

	/// <summary>
	/// A helper class to turn a dictionary into a `IHashable`
	/// </summary>
	internal class HashableObject : IHashable, IEnumerable<KeyValuePair<string, IHashable>>
	{
		/// <summary>
		/// The mapping of property name to hashable value to hash
		/// </summary>
		public Dictionary<string, IHashable> Properties { get; }

		/// <param name="properties">The mapping of property name to hashable value to hash</param>
		public HashableObject(Dictionary<string, IHashable> properties)
		{
			this.Properties = properties ?? throw new ArgumentNullException(nameof(properties));
		}

		/// <inheritdoc />
		public byte[] ComputeHash(IHashFunction hashFunction)
		{
			RequestId requestId = RequestId.FromObject(this.Properties, hashFunction);
			return requestId.RawValue;
		}

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<string, IHashable>> GetEnumerator()
		{
			return this.Properties.GetEnumerator();
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.Properties.GetEnumerator();
		}
	}

	internal class HashComparer : IComparer<byte[]>
	{
		public int Compare(byte[] x, byte[] y)
		{
			if (x.Length != y.Length)
			{
				return x.Length - y.Length;
			}
			for (int i = 0; i < x.Length; i++)
			{
				if (x[i] != y[i])
				{
					return x[i] - y[i];
				}
			}
			return 0;
		}
	}

	internal class HashableArray : IHashable
	{
		public HashableArray(List<IHashable>? items = null)
		{
			this.Items = items ?? new List<IHashable>();
		}

		public List<IHashable> Items { get; }

		public byte[] ComputeHash(IHashFunction hashFunction)
		{
			return hashFunction.ComputeHash(this.Items
				.SelectMany(i => i.ComputeHash(hashFunction))
				.ToArray());
		}
	}

	internal abstract class HashableValue : IHashable
	{
		public abstract byte[] GetRawValue();

		public byte[] ComputeHash(IHashFunction hashFunction)
		{
			byte[] raw = this.GetRawValue();
			return hashFunction.ComputeHash(raw);
		}
	}

	internal class HashableString : HashableValue
	{
		public string Value { get; }

		public HashableString(string value)
		{
			this.Value = value ?? throw new ArgumentNullException(nameof(value));
		}

		public static implicit operator HashableString(string value)
		{
			return new HashableString(value);
		}

		public static implicit operator string(HashableString value)
		{
			return value.Value;
		}

		public override byte[] GetRawValue()
		{
			return Encoding.UTF8.GetBytes(this.Value);
		}
		public override string ToString()
		{
			return this.Value;
		}
	}

	internal class HashableBytes : HashableValue
	{
		public byte[] Value { get; }

		public HashableBytes(byte[] value)
		{
			this.Value = value ?? throw new ArgumentNullException(nameof(value));
		}

		public override byte[] GetRawValue()
		{
			return this.Value;
		}
	}



	internal static class HashableExtensions
	{
		public static HashableArray ToHashable<T>(this IEnumerable<T> items)
			where T : IHashable
		{
			return ToHashable(items, i => i);
		}

		public static HashableArray ToHashable<T>(this IEnumerable<T> items, Func<T, IHashable> converter)
		{
			return new HashableArray(items.Select(converter).ToList());
		}

		public static HashableString ToHashable(this string value)
		{
			return new HashableString(value);
		}

		public static HashableBytes ToHashable(this byte[] value)
		{
			return new HashableBytes(value);
		}

		public static HashableObject ToHashable<T>(this Dictionary<string, T> value)
			where T : IHashable
		{
			return new HashableObject(value.ToDictionary(v => v.Key, v => (IHashable)v.Value));
		}
	}
}
