using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EdjCase.ICP.Candid.Models
{
	/// <summary>
	/// A model representing a list of path segments, used to navigate a state hash tree
	/// </summary>
	public class StatePath : IHashable
	{
		/// <summary>
		/// The list of segments making up the path
		/// </summary>
		public List<StatePathSegment> Segments { get; }

		/// <param name="segments">The list of segments making up the path</param>
		public StatePath(List<StatePathSegment> segments)
		{
			this.Segments = segments;
		}

		/// <summary>
		/// Helper method to create a path from a path segment array
		/// </summary>
		/// <param name="segments">An array of segments that make up the path</param>
		/// <returns>A path object</returns>
		public static StatePath FromSegments(params StatePathSegment[] segments)
		{
			return new StatePath(segments.ToList());
		}

		/// <inheritdoc />
		public byte[] ComputeHash(IHashFunction hashFunction)
		{
			return this.Segments
				.ToHashable()
				.ComputeHash(hashFunction);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return string.Join("/", this.Segments.Select(s => s.ToString()));
		}
	}

	/// <summary>
	/// A model representing a segment of a path for a state hash tree
	/// </summary>
	public class StatePathSegment : IHashable
	{
		/// <summary>
		/// The raw value of the path segment
		/// </summary>
		public byte[] Value { get; }

		/// <param name="value">The raw value of the path segment</param>
		public StatePathSegment(byte[] value)
		{
			if (value == null || value.Length < 1)
			{
				throw new ArgumentException("Path segment bytes cannot be empty", nameof(value));
			}
			this.Value = value ?? throw new ArgumentNullException(nameof(value));
		}

		/// <inheritdoc />
		public byte[] ComputeHash(IHashFunction hashFunction)
		{
			return hashFunction.ComputeHash(this.Value);
		}

		/// <summary>
		/// Creates a path segment from a string value by converting the string into UTF-8 bytes
		/// </summary>
		/// <param name="segment">The path segment to use</param>
		/// <returns>A path segment</returns>
		public static StatePathSegment FromString(string segment)
		{
			if (string.IsNullOrEmpty(segment))
			{
				throw new ArgumentException("Segment string value must have a value", nameof(segment));
			}
			return new StatePathSegment(Encoding.UTF8.GetBytes(segment));
		}

		/// <summary>
		/// A helper method to implicitly convert a path segment to its raw byte array
		/// </summary>
		/// <param name="value">The segment to convert</param>
		public static implicit operator byte[](StatePathSegment value)
		{
			return value.Value;
		}

		/// <summary>
		/// A helper method to implicitly convert a raw byte array to a path segment
		/// </summary>
		/// <param name="value">Byte array to convert</param>
		public static implicit operator StatePathSegment(byte[] value)
		{
			return new StatePathSegment(value);
		}

		/// <summary>
		/// A helper method to implicitly convert a string to a path segment
		/// </summary>
		/// <param name="value">String value to convert</param>
		public static implicit operator StatePathSegment(string value)
		{
			return FromString(value);
		}


		private static UTF8Encoding utf8Encoding = new(false, true); // Throw on invalid bytes

		/// <inheritdoc />
		public override string ToString()
		{
			try
			{
				return utf8Encoding.GetString(this.Value);
			}
			catch (DecoderFallbackException)
			{
				// If not valid UTF-8, return hex string
				return ByteUtil.ToHexString(this.Value);
			}
		}
	}
}