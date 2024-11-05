using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Encodings;
using EdjCase.ICP.Candid.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EdjCase.ICP.Candid.Models
{
	/// <summary>
	/// Subtypes of what a hash tree can be
	/// </summary>
	public enum HashTreeType
	{
		/// <summary>
		/// An empty branch with no data
		/// </summary>
		Empty,
		/// <summary>
		/// Left and right branching trees
		/// </summary>
		Fork,
		/// <summary>
		/// A branch that is labeled with its own subtree
		/// </summary>
		Labeled,
		/// <summary>
		/// A branch with data and no subtree
		/// </summary>
		Leaf,
		/// <summary>
		/// A branch that has been trimmed where its data is the hash of the subtree
		/// </summary>
		Pruned
	}

	/// <summary>
	/// A variant model representing a hash tree where values can be pruned and labeled in the tree
	/// </summary>
	public class HashTree : IEquatable<HashTree>
	{
		/// <summary>
		/// The type the tree node is
		/// </summary>
		public HashTreeType Type { get; }
		private object? value { get; }

		private HashTree(HashTreeType type, object? value)
		{
			this.Type = type;
			this.value = value;
		}

		/// <summary>
		/// Casts the tree to a left and right fork. If the node type is not a fork, then will throw an exception
		/// </summary>
		/// <returns>Left and right trees</returns>
		/// <exception cref="InvalidOperationException">Throws if tree is not a fork</exception>
		public (HashTree Left, HashTree Right) AsFork()
		{
			this.ValidateType(HashTreeType.Fork);
			return ((HashTree, HashTree))this.value!;
		}

		/// <summary>
		/// Casts the tree to a label and a tree. If the node type is not labeled, then will throw an exception
		/// </summary>
		/// <returns>Label of the node and subtree</returns>
		/// <exception cref="InvalidOperationException">Throws if tree is not labeled</exception>
		public (EncodedValue Label, HashTree Tree) AsLabeled()
		{
			this.ValidateType(HashTreeType.Labeled);
			return ((EncodedValue, HashTree))this.value!;
		}

		/// <summary>
		/// Casts the tree to an encoded value. If the node type is not a leaf, then will throw an exception
		/// </summary>
		/// <returns>Encoded value of the leaf</returns>
		/// <exception cref="InvalidOperationException">Throws if tree is not a leaf</exception>
		public EncodedValue AsLeaf()
		{
			this.ValidateType(HashTreeType.Leaf);
			return (EncodedValue)this.value!;
		}

		/// <summary>
		/// Casts the tree to a pruned hash value. If the node type is not pruned, then will throw an exception
		/// </summary>
		/// <returns>Byte array hash value of the pruned subtree</returns>
		/// <exception cref="InvalidOperationException">Throws if tree is not pruned</exception>
		public byte[] AsPruned()
		{
			this.ValidateType(HashTreeType.Pruned);
			return (byte[])this.value!;
		}

		/// <summary>
		/// Helper method to create an empty tree
		/// </summary>
		/// <returns>An empty hash tree</returns>
		public static HashTree Empty()
		{
			return new HashTree(HashTreeType.Empty, null);
		}

		/// <summary>
		/// Helper method to create a forked tree
		/// </summary>
		/// <param name="left">The branch to the left</param>
		/// <param name="right">The branch to the right</param>
		/// <returns>An forked hash tree</returns>
		public static HashTree Fork(HashTree left, HashTree right)
		{
			return new HashTree(HashTreeType.Fork, (left, right));
		}

		/// <summary>
		/// Helper method to create a labeled tree
		/// </summary>
		/// <param name="label">The label for the tree</param>
		/// <param name="tree">The subtree for the label</param>
		/// <returns>An labeled hash tree</returns>
		public static HashTree Labeled(EncodedValue label, HashTree tree)
		{
			return new HashTree(HashTreeType.Labeled, (label, tree));
		}

		/// <summary>
		/// Helper method to create a leaf tree
		/// </summary>
		/// <param name="value">The value to store in the leaf</param>
		/// <returns>An leaf hash tree</returns>
		public static HashTree Leaf(EncodedValue value)
		{
			return new HashTree(HashTreeType.Leaf, value);
		}

		/// <summary>
		/// Helper method to create a pruned tree
		/// </summary>
		/// <param name="treeHash">The hash of the tree that was pruned</param>
		/// <returns>An pruned hash tree</returns>
		public static HashTree Pruned(byte[] treeHash)
		{
			return new HashTree(HashTreeType.Pruned, treeHash);
		}

		/// <summary>
		/// Gets the value of the subtree specified by the path, returns null if not found
		/// </summary>
		/// <param name="path">The path segment to get a value from</param>
		/// <returns>A hash tree from the path, or null if not found</returns>
		public HashTree? GetValueOrDefault(params StatePathSegment[] path)
		{
			return this.GetValueOrDefault(new StatePath(path.ToList()));
		}

		/// <summary>
		/// Gets the value of the subtree specified by the path, returns null if not found
		/// </summary>
		/// <param name="path">The path to get a value from</param>
		/// <returns>A hash tree from the path, or null if not found</returns>
		public HashTree? GetValueOrDefault(StatePath path)
		{
			if (!path.Segments.Any())
			{
				return this;
			}
			HashTree currentTree = this;
			for (int i = 0; i < path.Segments.Count; i++)
			{
				StatePathSegment segment = path.Segments[i];
				HashTree newTree;
				switch (currentTree.Type)
				{
					case HashTreeType.Leaf:
						return null;
					case HashTreeType.Labeled:
						(EncodedValue label, HashTree tree) = currentTree.AsLabeled();
						bool areEqual = label == segment.Value;
						if (!areEqual)
						{
							return null;
						}
						newTree = tree;
						break;
					case HashTreeType.Pruned:
						return null;
					case HashTreeType.Empty:
						return null;
					case HashTreeType.Fork:
						(HashTree left, HashTree right) = currentTree.AsFork();
						var remainingPath = new StatePath(path.Segments.Skip(i).ToList());
						HashTree? l = left.GetValueOrDefault(remainingPath);
						if (l != null)
						{
							return l;
						}
						HashTree? r = right.GetValueOrDefault(remainingPath);
						if (r != null)
						{
							return r;
						}
						return null;
					default:
						throw new NotImplementedException();

				}
				currentTree = newTree;
			}
			return currentTree;
		}

		/// <summary>
		/// Computes the root SHA256 hash of the tree based on the IC certificate spec
		/// </summary>
		/// <returns>A byte array of the hash digest</returns>
		public byte[] BuildRootHash()
		{
			/*
				reconstruct(Empty)       = H(domain_sep("ic-hashtree-empty"))
				reconstruct(Fork t1 t2)  = H(domain_sep("ic-hashtree-fork") · reconstruct(t1) · reconstruct(t2))
				reconstruct(Labeled l t) = H(domain_sep("ic-hashtree-labeled") · l · reconstruct(t))
				reconstruct(Leaf v)      = H(domain_sep("ic-hashtree-leaf") · v)
				reconstruct(Pruned h)    = h

				domain_sep(s) = byte(|s|) · s
			 */
			SHA256HashFunction hashFunction = new(SHA256.Create());
			return this.BuildHashInternal(hashFunction);
		}


		/// <inheritdoc />
		public override string ToString()
		{
			switch (this.Type)
			{
				case HashTreeType.Empty:
					return "Empty";
				case HashTreeType.Pruned:
					return "Pruned: " + ByteUtil.ToHexString(this.AsPruned());
				case HashTreeType.Leaf:
					return "Leaf: " + ByteUtil.ToHexString(this.AsLeaf());
				case HashTreeType.Fork:
					(HashTree left, HashTree right) = this.AsFork();
					return $"Fork: {{ Left: {left}, Right: {right} }}";
				case HashTreeType.Labeled:
					(EncodedValue label, HashTree tree) = this.AsLabeled();
					return $"Labeled: {label} {tree}";
				default:
					throw new NotImplementedException();
			}
		}

		/// <summary>
		/// A helper class that wraps around a byte array, giving functions to convert 
		/// to common types like text and numbers
		/// </summary>
		public class EncodedValue : IEquatable<EncodedValue>
		{
			/// <summary>
			/// The raw value
			/// </summary>
			public byte[] Value { get; }

			/// <param name="value">The raw value</param>
			public EncodedValue(byte[] value)
			{
				this.Value = value;
			}

			/// <summary>
			/// The raw value converted to UTF-8 encoded string
			/// </summary>
			/// <returns>A UTF-8 string of the value</returns>
			public string AsUtf8()
			{
				return Encoding.UTF8.GetString(this.Value);
			}

			/// <summary>
			/// The raw value converted to a LEB128 encoded number
			/// </summary>
			/// <returns>A unbounded uint of the value</returns>
			public UnboundedUInt AsNat()
			{
				return LEB128.DecodeUnsigned(this.Value);
			}

			private static Encoding utf8Encoding = new UTF8Encoding(false, true);

			/// <inheritdoc />
			public override string ToString()
			{
				try
				{
					return utf8Encoding.GetString(this.Value);
				}
				catch (DecoderFallbackException)
				{
					// If the string is not valid utf8, then return the hex string
					return ByteUtil.ToHexString(this.Value);
				}
			}

			/// <inheritdoc />
			public override bool Equals(object? obj)
			{
				if (obj is EncodedValue b)
				{
					return this.Equals(b);
				}
				if (obj is string s)
				{
					return this.AsUtf8() == s;
				}
				if (obj is byte[] by)
				{
					return this.Equals(by);
				}
				return false;
			}

			/// <inheritdoc />
			public bool Equals(EncodedValue? other)
			{
				return this.Equals(other?.Value);
			}

			/// <inheritdoc />
			public bool Equals(byte[]? other)
			{
				if (ReferenceEquals(other, null))
				{
					return false;
				};
				return this.Value.AsSpan().SequenceEqual(other);
			}

			/// <inheritdoc />
			public override int GetHashCode()
			{
				return this.Value.GetHashCode();
			}

			/// <inheritdoc />
			public static bool operator ==(EncodedValue? v1, EncodedValue? v2)
			{
				if (ReferenceEquals(v1, null))
				{
					return ReferenceEquals(v2, null);
				}
				return v1.Equals(v2);
			}

			/// <inheritdoc />
			public static bool operator !=(EncodedValue? v1, EncodedValue? v2)
			{
				if (ReferenceEquals(v1, null))
				{
					return ReferenceEquals(v2, null);
				}
				return !v1.Equals(v2);
			}

			/// <summary>
			/// A helper method to implicitly convert an encoded value to a byte array
			/// </summary>
			/// <param name="value">The encoded value to get the raw value from</param>
			public static implicit operator byte[](EncodedValue value)
			{
				return value.Value;
			}

			/// <summary>
			/// A helper method to implicitly convert an byte array to an encoded value
			/// </summary>
			/// <param name="bytes">The raw value to use with the encoded value</param>
			public static implicit operator EncodedValue(byte[] bytes)
			{
				return new EncodedValue(bytes);
			}

			/// <summary>
			/// A helper method to implicitly convert an encoded value to a UTF8 string
			/// </summary>
			/// <param name="value">The encoded value to get the raw value from</param>
			public static implicit operator string(EncodedValue value)
			{
				return value.AsUtf8();
			}

			/// <summary>
			/// A helper method to implicitly convert a UTF8 string to an encoded value
			/// </summary>
			/// <param name="utf8Value">The UTF8 string value to use with the encoded value</param>
			public static implicit operator EncodedValue(string utf8Value)
			{
				return EncodedValue.Utf8Value(utf8Value);
			}

			/// <summary>
			/// Creates an encoded value from a utf8 string value
			/// </summary>
			/// <param name="value">UTF8 encoded string</param>
			/// <returns>UTF8 encoded value</returns>
			public static EncodedValue Utf8Value(string value)
			{
				return Encoding.UTF8.GetBytes(value);
			}


			internal static byte[] WithDomainSeperator(string value, params byte[][] encodedValues)
			{
				// domain_sep(s) = byte(|s|) · s
				byte[] textBytes = Encoding.UTF8.GetBytes(value);
				byte[] bytes = new byte[textBytes.Length + 1 + encodedValues.Sum(v => v.Length)];
				bytes[0] = (byte)textBytes.Length; // First byte is the seperator length
				int index = 1;
				textBytes.CopyTo(bytes, index);
				index += textBytes.Length;
				// Append the other bytes
				foreach (byte[] encodedValue in encodedValues)
				{
					encodedValue.CopyTo(bytes, index);
					index += encodedValue.Length;
				}
				return bytes;
			}
		}

		private byte[] BuildHashInternal(SHA256HashFunction hashFunction)
		{
			byte[] encodedValue;
			switch (this.Type)
			{
				case HashTreeType.Empty:
					encodedValue = EncodedValue.WithDomainSeperator("ic-hashtree-empty");
					break;
				case HashTreeType.Fork:
					(HashTree left, HashTree right) = this.AsFork();
					byte[] leftHash = left.BuildHashInternal(hashFunction);
					byte[] rightHash = right.BuildHashInternal(hashFunction);
					encodedValue = EncodedValue.WithDomainSeperator("ic-hashtree-fork", leftHash, rightHash);
					break;
				case HashTreeType.Labeled:
					(EncodedValue label, HashTree tree) = this.AsLabeled();
					byte[] treeHash = tree.BuildHashInternal(hashFunction);
					encodedValue = EncodedValue.WithDomainSeperator("ic-hashtree-labeled", label.Value, treeHash);
					break;
				case HashTreeType.Leaf:
					EncodedValue leaf = this.AsLeaf();
					encodedValue = EncodedValue.WithDomainSeperator("ic-hashtree-leaf", leaf.Value);
					break;
				case HashTreeType.Pruned:
					return this.AsPruned(); // Dont hash, its already a hash
				default:
					throw new NotImplementedException("Node type: " + this.Type);
			}
			return hashFunction.ComputeHash(encodedValue);
		}



		private void ValidateType(HashTreeType nodeType)
		{
			if (this.Type != nodeType)
			{
				throw new InvalidOperationException($"Node type '{this.Type}' cannot be cast as '{nodeType}'");
			}
		}

		/// <inheritdoc />
		public bool Equals(HashTree? other)
		{
			if (ReferenceEquals(other, null))
			{
				return false;
			};
			if (this.Type != other.Type)
			{
				return false;
			}
			switch (this.Type)
			{
				case HashTreeType.Labeled:
					(EncodedValue label, HashTree innerTree) = this.AsLabeled();
					(EncodedValue otherLabel, HashTree otherInnerTree) = other.AsLabeled();
					return label == otherLabel && innerTree == otherInnerTree;
				case HashTreeType.Leaf:
					return this.AsLeaf() == other.AsLeaf();
				case HashTreeType.Pruned:
					return this.AsPruned().SequenceEqual(other.AsPruned());
				case HashTreeType.Empty:
					return true;
				case HashTreeType.Fork:
					(HashTree left, HashTree right) = this.AsFork();
					(HashTree otherLeft, HashTree otherRight) = this.AsFork();
					return left == otherLeft && right == otherRight;
				default:
					throw new NotImplementedException();

			}
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			if (obj is not HashTree t)
			{
				return false;
			}
			return this.Equals(t);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return this.ToString().GetHashCode();
		}

		/// <inheritdoc />
		public static bool operator ==(HashTree? v1, HashTree? v2)
		{
			if (ReferenceEquals(v1, null))
			{
				return ReferenceEquals(v2, null);
			}
			return v1.Equals(v2);
		}

		/// <inheritdoc />
		public static bool operator !=(HashTree? v1, HashTree? v2)
		{
			return !(v1 == v2);
		}
	}
}
