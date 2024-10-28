using EdjCase.ICP.Candid.Exceptions;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using System;
using System.Collections.Generic;

namespace EdjCase.ICP.Candid.Parsers
{
	internal static class CandidTextParser
	{
		public static CandidType Parse(string text)
		{
			return Parse<CandidType>(text);
		}

		public static T Parse<T>(string text)
			where T : CandidType
		{
			CandidTextTokenHelper helper = CandidTextTokenizer.Tokenize(text);
			CandidType def = ParseType(helper);
			return (T)def;
		}


		private static (CandidId? Name, CandidType Type) ParseArgType(CandidTextTokenHelper helper)
		{
			CandidId? name = null;
			if (helper.NextToken?.Type == CandidTextTokenType.Colon)
			{
				string rawName = helper.CurrentToken.GetTextValueOrThrow();
				name = CandidId.Create(rawName);
				helper.MoveNextOrThrow(); // :
				helper.MoveNextOrThrow(); // type
			}
			CandidType type = ParseType(helper);
			return (name, type);
		}


		private static CandidType ParseType(CandidTextTokenHelper helper)
		{
			switch (helper.CurrentToken.Type)
			{
				case CandidTextTokenType.Text:
					CandidType typeDef = GetNamedType(helper);
					return typeDef;
				case CandidTextTokenType.OpenParenthesis:
					return GetFunc(helper, null);
				case CandidTextTokenType.OpenCurlyBrace:
					// TODO service type
					return GetRecord(helper, null);
				case CandidTextTokenType.OpenBracket:
					return GetVec(helper, null);
				default:
					// TODO
					throw new NotImplementedException($"Parsing encountered unimplemented candid type, next token is {helper.CurrentToken.Type}");
			}
		}
		private static CandidFuncType GetFunc(CandidTextTokenHelper helper, CandidId? recursiveId)
		{
			helper.CurrentToken.ValidateType(CandidTextTokenType.OpenParenthesis);

			helper.MoveNextOrThrow();
			var argTypes = new List<(CandidId? Name, CandidType Type)>();
			while (helper.CurrentToken.Type != CandidTextTokenType.CloseParenthesis)
			{
				(CandidId? name, CandidType t) = ParseArgType(helper);
				argTypes.Add((name, t));
				if (helper.CurrentToken.Type == CandidTextTokenType.Comma)
				{
					helper.MoveNextOrThrow();
				}
			}

			helper.MoveNextOrThrow();
			string arrow = helper.CurrentToken.GetTextValueOrThrow();
			if (arrow != "->")
			{
				throw new CandidTextParseException($"Expected token '->', got '{arrow}'");
			}
			helper.MoveNextOrThrow();
			helper.CurrentToken.ValidateType(CandidTextTokenType.OpenParenthesis);

			helper.MoveNextOrThrow();
			var returnTypes = new List<(CandidId? Name, CandidType Type)>();
			while (helper.CurrentToken.Type != CandidTextTokenType.CloseParenthesis)
			{
				(CandidId? name, CandidType t) = ParseArgType(helper);
				returnTypes.Add((name, t));
				if (helper.CurrentToken.Type == CandidTextTokenType.Comma)
				{
					helper.MoveNextOrThrow();
				}
			}

			var modes = new List<FuncMode>();
			while (helper.MoveNext())
			{
				if (helper.CurrentToken.Type == CandidTextTokenType.SemiColon
					|| helper.CurrentToken.Type == CandidTextTokenType.CloseCurlyBrace)
				{
					helper.MoveNext();
					break;
				}
				string rawMode = helper.CurrentToken.GetTextValueOrThrow();
				FuncMode mode = rawMode switch
				{
					"query" => FuncMode.Query,
					"oneway" => FuncMode.Oneway,
					"composite_query" => FuncMode.CompositeQuery,
					_ => throw new CandidTextParseException($"Unexpected func mode '{rawMode}'. Valid func modes: {string.Join(", ", Enum.GetValues(typeof(FuncMode)))}")
				};
				modes.Add(mode);
			}
			return new CandidFuncType(modes, argTypes, returnTypes, recursiveId);
		}
		private static CandidOptionalType GetOpt(CandidTextTokenHelper helper, CandidId? recursiveId)
		{
			CandidType innerValue = ParseType(helper);
			return new CandidOptionalType(innerValue, recursiveId);
		}

		private static CandidRecordType GetRecord(CandidTextTokenHelper helper, CandidId? recursiveId)
		{
			Dictionary<CandidTag, CandidType> fields = new();
			helper.CurrentToken.ValidateType(CandidTextTokenType.OpenCurlyBrace);
			helper.MoveNextOrThrow();
			uint index = 0;
			while (helper.CurrentToken.Type != CandidTextTokenType.CloseCurlyBrace)
			{
				CandidTag label;
				CandidType fieldType;
				if (helper.NextToken?.Type == CandidTextTokenType.Colon)
				{
					// `label`: `type`
					string rawLabel = helper.CurrentToken.GetTextValueOrThrow()
						.Trim('\"'); // Sometimes there are quotes for escaping reserved words
					label = uint.TryParse(rawLabel, out uint id)
						? CandidTag.FromId(id)
						: CandidTag.FromName(rawLabel);
					helper.MoveNextOrThrow();
					helper.CurrentToken.ValidateType(CandidTextTokenType.Colon);
					helper.MoveNextOrThrow();
					fieldType = ParseType(helper);
				}
				else
				{
					// `type` (based on position/index)
					label = CandidTag.FromId(index++);
					fieldType = ParseType(helper);
				}
				fields.Add(label, fieldType);
			}
			helper.MoveNext(); // May be end of file
			return new CandidRecordType(fields, recursiveId);
		}

		private static CandidVectorType GetVec(CandidTextTokenHelper helper, CandidId? recursiveId)
		{
			CandidType innerValue = ParseType(helper);
			return new CandidVectorType(innerValue, recursiveId);
		}

		private static CandidVariantType GetVariant(CandidTextTokenHelper helper, CandidId? recursiveId)
		{
			Dictionary<CandidTag, CandidType> options = new();
			helper.CurrentToken.ValidateType(CandidTextTokenType.OpenCurlyBrace);
			helper.MoveNextOrThrow();
			while (helper.CurrentToken.Type != CandidTextTokenType.CloseCurlyBrace)
			{
				string label = helper.CurrentToken
					.GetTextValueOrThrow()
					.Trim('\"'); // Sometimes there are quotes for escaping reserved words
				helper.MoveNextOrThrow();
				CandidType fieldType;
				switch (helper.CurrentToken.Type)
				{
					case CandidTextTokenType.SemiColon:
						fieldType = new CandidPrimitiveType(PrimitiveType.Null);
						helper.MoveNextOrThrow();
						break;
					case CandidTextTokenType.Colon:
						helper.MoveNextOrThrow();
						fieldType = ParseType(helper);
						break;
					case CandidTextTokenType.CloseCurlyBrace:
						/* Edge case: handles missing semicolon on the final option of a variant, when that option has a "null" field type:
						 *	type MyVariant = variant {
						 *		foo: A;
						 *		bar
						 *	}<<< Cursor is here
						 */
						fieldType = new CandidPrimitiveType(PrimitiveType.Null);
						break;
					default:
						throw new CandidTextParseException($"Unexpected token '{helper.CurrentToken.Type}' after variant label");
				}
				options.Add(CandidTag.FromName(label), fieldType);
			}
			helper.MoveNext();
			return new CandidVariantType(options, recursiveId);
		}

		private static CandidServiceType GetService(CandidTextTokenHelper helper, CandidId? recursiveId)
		{
			Dictionary<CandidId, CandidFuncType> methods = new();
			helper.CurrentToken.ValidateType(CandidTextTokenType.OpenCurlyBrace);
			helper.MoveNextOrThrow();
			while (helper.CurrentToken.Type != CandidTextTokenType.CloseCurlyBrace)
			{
				string label = helper.CurrentToken.GetTextValueOrThrow();
				helper.MoveNextOrThrow();
				helper.CurrentToken.ValidateType(CandidTextTokenType.Colon);
				helper.MoveNextOrThrow();
				CandidFuncType methodType = GetFunc(helper, null);
				methods.Add(CandidId.Create(label), methodType);
				if (helper.CurrentToken.Type == CandidTextTokenType.SemiColon)
				{
					helper.MoveNext();
				}
			}
			helper.MoveNext();

			return new CandidServiceType(methods, recursiveId);
		}

		private static CandidType GetNamedType(CandidTextTokenHelper helper)
		{
			CandidId? recursiveId = null;
			string type = helper.CurrentToken.GetTextValueOrThrow();
			// Check to see if text is recursive id like `μrec_1.record` vs just the type name `record`
			if (type.StartsWith("μ"))
			{
				recursiveId = CandidId.Create(type.Substring(1));
				helper.MoveNextOrThrow();
				helper.CurrentToken.ValidateType(CandidTextTokenType.Period); // Period seperates recursive id and type
				helper.MoveNextOrThrow();
				type = helper.CurrentToken.GetTextValueOrThrow();
			}

			helper.MoveNext();

			CandidType t = GetNamedType(type, helper, recursiveId);
			if (helper.CurrentToken.Type == CandidTextTokenType.SemiColon)
			{
				helper.MoveNext();
			}
			return t;
		}

		private static CandidType GetNamedType(string type, CandidTextTokenHelper helper, CandidId? recursiveId)
		{

			switch (type)
			{
				case "opt":
					return GetOpt(helper, recursiveId);
				case "record":
					return GetRecord(helper, recursiveId);
				case "vec":
					return GetVec(helper, recursiveId);
				case "blob": // Shorthand
					return new CandidVectorType(new CandidPrimitiveType(PrimitiveType.Nat8));
				case "variant":
					return GetVariant(helper, recursiveId);
				case "service":
					return GetService(helper, recursiveId);
				case "func":
					return GetFunc(helper, recursiveId);
				case "int8":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Int8);
				case "int16":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Int16);
				case "int32":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Int32);
				case "int64":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Int64);
				case "int":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Int);
				case "nat8":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Nat8);
				case "nat16":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Nat16);
				case "nat32":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Nat32);
				case "nat64":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Nat64);
				case "nat":
					return new CandidPrimitiveType(PrimitiveType.Nat);
				case "text":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Text);
				case "float32":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Float32);
				case "float64":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Float64);
				case "bool":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Bool);
				case "principal":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Principal);
				case "reserved":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Reserved);
				case "empty":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Empty);
				case "null":
					ThrowIfRecursiveIdSet();
					return new CandidPrimitiveType(PrimitiveType.Null);
				default:
					return new CandidReferenceType(CandidId.Create(type));
			};

			void ThrowIfRecursiveIdSet()
			{
				if (recursiveId != null)
				{
					throw new CandidTextParseException($"Recursive reference id '{recursiveId}' is set on an invalid type '{type}'");
				}
			}
		}
	}
}
