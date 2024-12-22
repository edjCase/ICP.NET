using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace EdjCase.ICP.Candid.Models
{
	/// <summary>
	/// A helper class to wrap around the request id byte array that identifies a request
	/// </summary>
	public class RequestId : IHashable
	{
		/// <summary>
		/// The raw id value
		/// </summary>
		public byte[] RawValue { get; }

		/// <param name="rawValue">The raw id value</param>
		public RequestId(byte[] rawValue)
		{
			this.RawValue = rawValue;
		}

		/// <summary>
		/// Creates a request id from raw bytes, same as the constructor
		/// </summary>
		/// <param name="bytes">Raw request id bytes</param>
		/// <returns>A request id object</returns>
		public static RequestId FromBytes(byte[] bytes)
		{
			return new RequestId(bytes);
		}

		/// <summary>
		/// Converts a hashable object into a request id
		/// </summary>
		/// <param name="properties">The properties of the object to hash</param>
		/// <param name="hashFunction">The hash function to use to generate the hash</param>
		/// <returns>A request id object</returns>
		public static RequestId FromObject(IDictionary<string, IHashable> properties, IHashFunction hashFunction)
		{
			var orderedProperties = properties
				.Where(o => o.Value != null) // Remove empty/null ones
				.Select(o =>
				{
					byte[] keyDigest = o.Key.ToHashable().ComputeHash(hashFunction);
					byte[] valueDigest = o.Value!.ComputeHash(hashFunction);

					return (KeyHash: keyDigest, ValueHash: valueDigest);
				}) // Hash key and value bytes
				.OrderBy(o => o.KeyHash, new HashComparer()); // Keys in order
			byte[] bytes = orderedProperties
				.SelectMany(o => o.KeyHash.Concat(o.ValueHash))
				.ToArray(); // Create single byte[] by concatinating them all together
			return new RequestId(hashFunction.ComputeHash(bytes));
		}

		/// <inheritdoc />
		public byte[] ComputeHash(IHashFunction hashFunction)
		{
			return hashFunction.ComputeHash(this.RawValue);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return ByteUtil.ToHexString(this.RawValue);
		}
	}
}
