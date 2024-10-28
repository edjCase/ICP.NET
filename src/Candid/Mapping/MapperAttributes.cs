using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Parsers;
using System;

namespace EdjCase.ICP.Candid.Mapping
{
	/// <summary>
	/// An attribute to specify a candid name to use for serialization. If unspecified 
	/// the serializers will use the property names
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class CandidNameAttribute : CandidTagAttribute
	{

		/// <param name="name">The name to use for serialization of candid values</param>
		public CandidNameAttribute(string name) : base(name)
		{
		}
	}
	/// <summary>
	/// An attribute to specify a candid tag to use for serialization. If unspecified 
	/// the serializers will use the property names
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class CandidTagAttribute : Attribute
	{
		/// <summary>
		/// The tag to use for serialization of candid values
		/// </summary>
		public CandidTag Tag { get; }

		/// <param name="tag">The tag to use for serialization of candid values</param>
		public CandidTagAttribute(CandidTag tag)
		{
			this.Tag = tag;
		}

		/// <param name="id">The tag id (name hash) to use for serialization of candid values</param>
		public CandidTagAttribute(uint id)
		{
			this.Tag = id;
		}

		/// <param name="name">The tag name to use for serialization of candid values</param>
		public CandidTagAttribute(string name)
		{
			this.Tag = name;
		}
	}

	/// <summary>
	/// An attribute to ignore a property/field of a class during serialization
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class CandidIgnoreAttribute : Attribute
	{
	}

	/// <summary>
	/// An attribute to use the raw nullable value vs OptionalValue type
	/// E.g. OptionalValue of string, can be a string with this attribute
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method)]
	public class CandidOptionalAttribute : Attribute
	{

	}

	/// <summary>
	/// An attribute to put on a class to identify it as a variant type for serialization.
	/// Requires the use of `VariantTagPropertyAttribute`, `VariantOptionTypeAttribute` and
	/// `VariantValuePropertyAttribute` attributes if used
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class VariantAttribute : Attribute
	{
	}

	/// <summary>
	/// An attribute to put on a property/field that indicates where to hold the 
	/// tag enum value. Must match the type passed to the `VariantAttribute`
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class VariantTagPropertyAttribute : Attribute
	{
	}

	/// <summary>
	/// An attribute to put on a property/field that indicates where to hold the
	/// tag value object. The type must be compatible with all value types, recommend using `object?`
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class VariantValuePropertyAttribute : Attribute
	{
	}

	/// <summary>
	/// An attribute to put on an enum option to specify if the tag has an attached
	/// value in the variant, otherwise the attached type will be null
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
	public class VariantOptionAttribute : Attribute
	{
		/// <summary>
		/// The type of the variant option value to use
		/// </summary>
		public CandidTag Tag { get; }

		/// <param name="tag">The tag of the variant option</param>
		public VariantOptionAttribute(CandidTag tag)
		{
			this.Tag = tag;
		}

		/// <param name="tag">The tag of the variant option</param>
		public VariantOptionAttribute(string tag) : this(CandidTag.FromName(tag))
		{

		}
	}

	/// <summary>
	/// An attribute to specify the candid type definition for a value
	/// This is REQUIRED for any raw candid values
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method)]
	public class CandidTypeDefinitionAttribute : Attribute
	{
		/// <summary>
		/// The candid type definition for the value
		/// </summary>
		public CandidType Type { get; }

		/// <param name="definition">The candid type definition in string format https://github.com/dfinity/candid/blob/master/spec/Candid.md#core-grammar</param>
		public CandidTypeDefinitionAttribute(string definition)
		{
			this.Type = CandidTextParser.Parse(definition);
		}
	}
}
