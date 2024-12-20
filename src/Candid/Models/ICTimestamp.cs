using System;
using System.Diagnostics;
using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Encodings;
using EdjCase.ICP.Candid.Models.Values;

namespace EdjCase.ICP.Candid.Models
{
	/// <summary>
	/// Helper class to wrap around an unbounded uint to represent the nanoseconds since 1970-01-01
	/// </summary>
	public class ICTimestamp : IHashable
	{
		private const int REPLICA_PERMITTED_DRIFT_MILLISECONDS = 60_000;

		private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		/// <summary>
		/// The nanoseconds since 1970-01-01
		/// </summary>
		public UnboundedUInt NanoSeconds { get; }

		/// <param name="nanoSeconds">The nanoseconds since 1970-01-01</param>
		public ICTimestamp(UnboundedUInt nanoSeconds)
		{
			this.NanoSeconds = nanoSeconds ?? throw new ArgumentNullException(nameof(nanoSeconds));
		}

		/// <summary>
		/// Helper method to convert nanoseconds from 1970-01-01 to an ICTimestamp
		/// </summary>
		/// <param name="nanosecondsSinceEpoch">Nanoseconds since 1970-01-01</param>
		/// <returns>An ICTimestamp based on the nanoseconds</returns>
		public static ICTimestamp FromNanoSeconds(UnboundedUInt nanosecondsSinceEpoch)
		{
			return new ICTimestamp(nanosecondsSinceEpoch);
		}
		/// <summary>
		/// Helper method to convert nanoseconds from 1970-01-01 to an ICTimestamp
		/// </summary>
		/// <param name="timespan">Time since 1970-01-01</param>
		/// <returns>An ICTimestamp based on the nanoseconds</returns>
		public static ICTimestamp From(TimeSpan timespan)
		{
			return new ICTimestamp(GetNanosecondsFromTimeSpan(timespan));
		}

		/// <summary>
		/// Helper method to get the current timestamp
		/// </summary>
		/// <returns>A timestamp of the current time</returns>
		public static ICTimestamp Now()
		{
			return FutureInNanoseconds(0);
		}

		/// <summary>
		/// Helper method to get a timestamp for X nanoseconds in the future from NOW instead of 1970-01-01
		/// </summary>
		/// <param name="nanosecondsFromNow">The nanoseconds from now in the future</param>
		/// <returns>A timestamp of the nanoseconds from now</returns>
		public static ICTimestamp FutureInNanoseconds(UnboundedUInt nanosecondsFromNow)
		{
			UnboundedUInt futureDate = EpochNowInNanoseconds() + nanosecondsFromNow;
			return new ICTimestamp(futureDate);
		}

		/// <summary>
		/// Helper method to get a timestamp for X time in the future from NOW instead of 1970-01-01
		/// </summary>
		/// <param name="timeFromNow">The time from now in the future</param>
		/// <returns>A timestamp of the time from now</returns>
		public static ICTimestamp Future(TimeSpan timeFromNow)
		{
			return FutureInNanoseconds(GetNanosecondsFromTimeSpan(timeFromNow));
		}

		/// <inheritdoc />
		public byte[] ComputeHash(IHashFunction hashFunction)
		{
			return hashFunction.ComputeHash(LEB128.EncodeUnsigned(this.NanoSeconds));
		}

		/// <summary>
		/// Converts the nanoseconds to a candid Nat value
		/// </summary>
		/// <returns>Candid nat value of the nanoseconds</returns>
		public CandidValue ToCandid()
		{
			return CandidValue.Nat(this.NanoSeconds);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			TimeSpan offset = TimeSpan.FromMilliseconds((double)this.NanoSeconds.ToBigInteger() / 1000000);
			return epoch
				.Add(offset)
				.ToString("s");
		}

		/// <inheritdoc />
		public static bool operator >=(ICTimestamp a, ICTimestamp b)
		{
			return a.NanoSeconds >= b.NanoSeconds;
		}

		/// <inheritdoc />
		public static bool operator >(ICTimestamp a, ICTimestamp b)
		{
			return a.NanoSeconds > b.NanoSeconds;
		}

		/// <inheritdoc />
		public static bool operator <=(ICTimestamp a, ICTimestamp b)
		{
			return a.NanoSeconds <= b.NanoSeconds;
		}

		/// <inheritdoc />
		public static bool operator <(ICTimestamp a, ICTimestamp b)
		{
			return a.NanoSeconds < b.NanoSeconds;
		}

		/// <inheritdoc />
		public static ICTimestamp operator +(ICTimestamp timestamp, TimeSpan timeSpan)
		{
			return new ICTimestamp(timestamp.NanoSeconds + GetNanosecondsFromTimeSpan(timeSpan));
		}

		/// <inheritdoc />
		public static ICTimestamp operator -(ICTimestamp timestamp, TimeSpan timeSpan)
		{
			return new ICTimestamp(timestamp.NanoSeconds - GetNanosecondsFromTimeSpan(timeSpan));
		}

		private static UnboundedUInt EpochNowInNanoseconds()
		{
			ulong nanoseconds = (ulong)((DateTime.UtcNow - epoch).TotalMilliseconds * 1_000_000);
			return UnboundedUInt.FromUInt64(nanoseconds);
		}

		private static UnboundedUInt GetNanosecondsFromTimeSpan(TimeSpan timespanFromNow)
		{
			return (ulong)(timespanFromNow.TotalMilliseconds * 1_000_000);
		}
	}
}