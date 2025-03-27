using EdjCase.ICP.Candid;
using EdjCase.ICP.Candid.Mapping;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EdjCase.ICP.Candid.Tests
{
	public class CandidConverterTests
	{
		[Fact]
		public void Text_From_String()
		{
			string value = "Test";
			CandidValue expectedValue = CandidValue.Text(value);
			CandidType expectedType = new CandidPrimitiveType(PrimitiveType.Text);
			CandidTypedValue expected = CandidTypedValue.FromValueAndType(expectedValue, expectedType);

			this.Test(value, expected, (x, y) => x == y);
		}


		[Fact]
		public void Vector_From_List()
		{
			List<string> values = new List<string>
			{
				"1",
				"2",
				"3"
			};
			CandidValue expectedValue = new CandidVector(values.Select(v => CandidValue.Text(v)).ToArray());
			CandidType expectedType = new CandidVectorType(new CandidPrimitiveType(PrimitiveType.Text));
			CandidTypedValue expected = CandidTypedValue.FromValueAndType(expectedValue, expectedType);

			this.Test(values, expected, Enumerable.SequenceEqual);
		}

		[Fact]
		public void Vector_From_Array()
		{
			var values = new string[]
			{
				"1",
				"2",
				"3"
			};
			CandidValue expectedValue = new CandidVector(values.Select(v => CandidValue.Text(v)).ToArray());
			CandidType expectedType = new CandidVectorType(new CandidPrimitiveType(PrimitiveType.Text));
			CandidTypedValue expected = CandidTypedValue.FromValueAndType(expectedValue, expectedType);

			this.Test(values, expected, Enumerable.SequenceEqual);
		}
		[Fact]
		public void Vector_From_Dict()
		{
			var values = new Dictionary<string, UnboundedUInt>
			{
				["1"] = 1,
				["2"] = 2,
				["3"] = 3
			};
			CandidValue[] elements = values
				.Select(v => new CandidRecord(new Dictionary<CandidTag, CandidValue>
				{
					[0] = CandidValue.Text(v.Key),
					[1] = CandidValue.Nat(v.Value)
				}))
				.ToArray();
			CandidValue expectedValue = new CandidVector(elements);
			CandidType expectedType = new CandidVectorType(new CandidRecordType(new Dictionary<CandidTag, CandidType>
			{
				[0] = CandidType.Text(),
				[1] = CandidType.Nat()
			}));
			CandidTypedValue expected = CandidTypedValue.FromValueAndType(expectedValue, expectedType);

			this.Test(values, expected, Enumerable.SequenceEqual);
		}

		public class RecordClass
		{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
			public OptionalValue<string> StringField { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
			public int IntField { get; set; }

			public override bool Equals(object? obj)
			{
				if (obj is not RecordClass r)
				{
					return false;
				}
				return this.StringField == r.StringField && this.IntField == r.IntField;
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(this.StringField, this.IntField);
			}
		}
		[Fact]
		public void Record_From_Class()
		{
			var values = new RecordClass
			{
				StringField = OptionalValue<string>.WithValue("StringValue"),
				IntField = 2
			};
			CandidTag stringFieldName = CandidTag.FromName("StringField");
			CandidTag intFieldName = CandidTag.FromName("IntField");
			var fields = new Dictionary<CandidTag, CandidValue>
			{
				{stringFieldName, new CandidOptional(CandidValue.Text("StringValue"))},
				{intFieldName, CandidValue.Int32(2)}
			};
			CandidValue expectedValue = new CandidRecord(fields);

			var fieldTypes = new Dictionary<CandidTag, CandidType>
			{
				{stringFieldName, new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Text))},
				{intFieldName, new CandidPrimitiveType(PrimitiveType.Int32)}
			};
			CandidType expectedType = new CandidRecordType(fieldTypes);
			CandidTypedValue expected = CandidTypedValue.FromValueAndType(expectedValue, expectedType);

			this.Test(values, expected, (x, y) =>
			{
				return x.IntField == y.IntField
					&& x.StringField == y.StringField;
			});
		}


		public class RecordNullableFieldClass
		{
			public string? StringField { get; set; }
			public int IntField { get; set; }

			public override bool Equals(object? obj)
			{
				if (obj is not RecordNullableFieldClass r)
				{
					return false;
				}
				return this.StringField == r.StringField && this.IntField == r.IntField;
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(this.StringField, this.IntField);
			}
		}




		[Variant]
		public class VariantValueClass
		{
			[VariantTagProperty]
			public VariantValueClassType Type { get; set; }
			[VariantValueProperty]
			public object? Value { get; set; }


			public string AsV2()
			{
				return (string)this.Value!;
			}
			public int AsV3()
			{
				return (int)this.Value!;
			}
			public OptionalValue<string> AsV4()
			{
				return (OptionalValue<string>)this.Value!;
			}
		}

		public enum VariantValueClassType
		{
			[CandidName("v1")]
			V1,
			[CandidName("v2")]
			V2,
			[CandidName("v3")]
			V3,
			[CandidName("v4")]
			V4
		}

		[Fact]
		public void Variant_From_Class()
		{
			var variant = new VariantValueClass
			{
				Type = VariantValueClassType.V4,
				Value = OptionalValue<string>.WithValue("text")
			};
			CandidValue expectedValue = new CandidVariant("v4", new CandidOptional(CandidValue.Text("text")));

			var optionTypes = new Dictionary<CandidTag, CandidType>
			{
				{CandidTag.FromName("v1"), new CandidPrimitiveType(PrimitiveType.Null)},
				{CandidTag.FromName("v2"), new CandidPrimitiveType(PrimitiveType.Text)},
				{CandidTag.FromName("v3"), new CandidPrimitiveType(PrimitiveType.Int32)},
				{CandidTag.FromName("v4"), new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Text))}
			};
			CandidType expectedType = new CandidVariantType(optionTypes);
			CandidTypedValue expected = CandidTypedValue.FromValueAndType(expectedValue, expectedType);

			this.Test(
				variant,
				expected,
				(x, y) =>
				{
					if (!ReferenceEquals(x.Value, null))
					{
						if (!ReferenceEquals(y.Value, null))
						{
							return x.Type == y.Type && x.Value!.Equals(y.Value);
						}
					}
					return false;
				});
		}


		[Fact]
		public void Tuple_From_Class()
		{
			var tuple = new Arg0ItemRecord("f0Value", "f1Value");

			CandidRecord value = new CandidRecord(new Dictionary<CandidTag, CandidValue>
			{
				[0] = CandidValue.Text("f0Value"),
				[1] = CandidValue.Text("f1Value")
			});
			CandidType type = new CandidRecordType(new Dictionary<CandidTag, CandidType>
			{
				[0] = CandidType.Text(),
				[1] = CandidType.Text()
			});
			CandidTypedValue expected = CandidTypedValue.FromValueAndType(value, type);
			this.Test(
				tuple,
				expected,
				(x, y) =>
				{
					return x.F0 == y.F0 && x.F1 == y.F1;
				}
			);
		}

		public class Arg0ItemRecord
		{
			[CandidTag(0U)] public string F0 { get; set; }

			[CandidTag(1U)] public string F1 { get; set; }

			public Arg0ItemRecord(string f0, string f1)
			{
				this.F0 = f0;
				this.F1 = f1;
			}

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
			public Arg0ItemRecord()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
			{
			}
		}



		[Fact]
		public void Optional_Type_Coercion()
		{
			OptTypeCoercion o = new()
			{
				TextValue = "Text1",
				ListOfText = new List<string?> { "Text2", null }
			};

			CandidValue oT = new CandidOptional(
				new CandidRecord(
					new Dictionary<CandidTag, CandidValue>
					{
						["TextValue"] = new CandidOptional(
							CandidValue.Text("Text1")
						),
						["ListOfText"] = new CandidOptional(
							new CandidVector(new CandidValue[]
							{
								new CandidOptional(CandidValue.Text("Text2")),
								new CandidOptional()
							})
						)
					}
				)
			);

			OptTypeCoercion actual = CandidConverter.Default.ToObject<OptTypeCoercion>(oT);
			Assert.Equal(o.TextValue, actual.TextValue);
			Assert.Equal(o.ListOfText, actual.ListOfText);

		}

		public class OptOverride
		{

			[CandidOptional]
			public string? OptValue { get; set; }
			public int? OptIntValue { get; set; }
			public OptionalValue<string> OptValue2 { get; set; } = OptionalValue<string>.NoValue();
		}

		[Fact]
		public void OptionalValueOverride_Record_WithValue()
		{
			var raw = new OptOverride { OptValue = "Test1", OptIntValue = 1, OptValue2 = OptionalValue<string>.WithValue("Test2") };
			var candidValue = new CandidRecord(new Dictionary<CandidTag, CandidValue>
			{
				["OptValue"] = new CandidOptional(CandidValue.Text("Test1")),
				["OptIntValue"] = new CandidOptional(CandidValue.Int32(1)),
				["OptValue2"] = new CandidOptional(CandidValue.Text("Test2")),
			});
			var candidType = new CandidRecordType(new Dictionary<CandidTag, CandidType>
			{
				["OptValue"] = new CandidOptionalType(CandidType.Text()),
				["OptIntValue"] = new CandidOptionalType(CandidType.Int32()),
				["OptValue2"] = new CandidOptionalType(CandidType.Text()),
			});
			var candid = new CandidTypedValue(candidValue, candidType);
			this.Test(raw, candid, (a, b) => a.OptValue == b.OptValue && a.OptValue2 == b.OptValue2);
		}
		[Fact]
		public void OptionalValueOverride_Record_Null()
		{
			var raw = new OptOverride { OptValue = null, OptIntValue = null, OptValue2 = OptionalValue<string>.NoValue() };
			var candidValue = new CandidRecord(new Dictionary<CandidTag, CandidValue>
			{
				["OptValue"] = new CandidOptional(),
				["OptIntValue"] = new CandidOptional(),
				["OptValue2"] = new CandidOptional(),
			});
			var candidType = new CandidRecordType(new Dictionary<CandidTag, CandidType>
			{
				["OptValue"] = new CandidOptionalType(CandidType.Text()),
				["OptIntValue"] = new CandidOptionalType(CandidType.Int32()),
				["OptValue2"] = new CandidOptionalType(CandidType.Text()),
			});
			var candid = new CandidTypedValue(candidValue, candidType);
			this.Test(raw, candid, (a, b) => a.OptValue == b.OptValue && a.OptValue2 == b.OptValue2);
		}

		[Variant]
		public class OptOverrideVariant
		{
			[VariantTagProperty]
			public TagEnum Tag { get; set; }

			[VariantValueProperty]
			public object? Value { get; set; }

			[CandidOptional]
			public string? AsString()
			{
				return (string)this.Value!;
			}

			public int? AsInt()
			{
				return (int?)this.Value!;
			}
		}

		public enum TagEnum
		{
			String,
			Int,
			Null
		}

		[Fact]
		public void OptionalValueOverride_Variant_WithStringValue()
		{
			var raw = new OptOverrideVariant
			{
				Tag = TagEnum.String,
				Value = "test"
			};
			var candidValue = new CandidVariant("String", new CandidOptional(CandidValue.Text("test")));
			var candidType = new CandidVariantType(new Dictionary<CandidTag, CandidType>
			{
				["String"] = new CandidOptionalType(CandidType.Text()),
				["Int"] = new CandidOptionalType(CandidType.Int32()),
				["Null"] = new CandidPrimitiveType(PrimitiveType.Null),
			});
			var candid = new CandidTypedValue(candidValue, candidType);
			this.Test(raw, candid, (a, b) => a.Tag == b.Tag && a.Value!.Equals(b.Value));
		}
		[Fact]
		public void OptionalValueOverride_Variant_WithNullStringValue()
		{
			var raw = new OptOverrideVariant
			{
				Tag = TagEnum.String,
				Value = null
			};
			var candidValue = new CandidVariant("String", new CandidOptional());
			var candidType = new CandidVariantType(new Dictionary<CandidTag, CandidType>
			{
				["String"] = new CandidOptionalType(CandidType.Text()),
				["Int"] = new CandidOptionalType(CandidType.Int32()),
				["Null"] = new CandidPrimitiveType(PrimitiveType.Null),
			});
			var candid = new CandidTypedValue(candidValue, candidType);
			this.Test(raw, candid, (a, b) => a.Tag == b.Tag && ((string?)a.Value) == ((string?)b.Value));
		}

		[Fact]
		public void OptionalValueOverride_Variant_WithIntValue()
		{
			var raw = new OptOverrideVariant
			{
				Tag = TagEnum.Int,
				Value = 1
			};
			var candidValue = new CandidVariant("Int", new CandidOptional(CandidValue.Int32(1)));
			var candidType = new CandidVariantType(new Dictionary<CandidTag, CandidType>
			{
				["String"] = new CandidOptionalType(CandidType.Text()),
				["Int"] = new CandidOptionalType(CandidType.Int32()),
				["Null"] = new CandidPrimitiveType(PrimitiveType.Null),
			});
			var candid = new CandidTypedValue(candidValue, candidType);
			this.Test(raw, candid, (a, b) => a.Tag == b.Tag && ((int?)a.Value) == ((int?)b.Value));
		}

		[Fact]
		public void OptionalValueOverride_Variant_WithNullIntValue()
		{
			var raw = new OptOverrideVariant
			{
				Tag = TagEnum.Int,
				Value = null
			};
			var candidValue = new CandidVariant("Int", new CandidOptional());
			var candidType = new CandidVariantType(new Dictionary<CandidTag, CandidType>
			{
				["String"] = new CandidOptionalType(CandidType.Text()),
				["Int"] = new CandidOptionalType(CandidType.Int32()),
				["Null"] = new CandidPrimitiveType(PrimitiveType.Null),
			});
			var candid = new CandidTypedValue(candidValue, candidType);
			this.Test(raw, candid, (a, b) => a.Tag == b.Tag && a.Value == null && b.Value == null);
		}

		[Fact]
		public void OptionalValueOverride_Variant_WithNullValue()
		{
			var raw = new OptOverrideVariant
			{
				Tag = TagEnum.Null,
				Value = null
			};
			var candidValue = new CandidVariant("Null", CandidValue.Null());
			var candidType = new CandidVariantType(new Dictionary<CandidTag, CandidType>
			{
				["String"] = new CandidOptionalType(CandidType.Text()),
				["Int"] = new CandidOptionalType(CandidType.Int32()),
				["Null"] = new CandidPrimitiveType(PrimitiveType.Null),
			});
			var candid = new CandidTypedValue(candidValue, candidType);
			this.Test(raw, candid, (a, b) => a.Tag == b.Tag && a.Value == null && b.Value == null);
		}

		public class OptTypeCoercion
		{
			public string? TextValue { get; set; }
			public List<string?>? ListOfText { get; set; }

		}

		public class MyList : List<string>
		{

		}

		[Fact]
		public void CustomList()
		{
			var raw = new MyList { "test" };
			var candidValue = new CandidVector(new CandidValue[]
			{
				CandidValue.Text("test")
			});
			var candidType = new CandidVectorType(CandidType.Text());
			var candid = new CandidTypedValue(candidValue, candidType);
			this.Test(raw, candid, (a, b) => a.SequenceEqual(b));
		}
		public class MyDict : Dictionary<string, int>
		{

		}

		[Fact]
		public void CustomDict()
		{
			var raw = new MyDict { ["test"] = 1 };
			var candidValue = new CandidVector(new CandidValue[] {
				new CandidRecord(
					new Dictionary<CandidTag, CandidValue>
					{
						[0] = CandidValue.Text("test"),
						[1] = CandidValue.Int32(1)
					}
				)
			});
			var candidType = new CandidVectorType(new CandidRecordType(new Dictionary<CandidTag, CandidType>
			{
				[0] = CandidType.Text(),
				[1] = CandidType.Int32()
			}));
			var candid = new CandidTypedValue(candidValue, candidType);
			this.Test(raw, candid, (a, b) => a["test"] == a["test"]);
		}

		public class MyOptValue : OptionalValue<Principal>
		{

		}

		[Fact]
		public void CustomOptionalValue()
		{
			var raw = MyOptValue.WithValue(Principal.Anonymous());
			var candidValue = new CandidOptional(CandidValue.Principal(Principal.Anonymous()));
			var candidType = new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Principal));
			var candid = new CandidTypedValue(candidValue, candidType);
			this.Test(raw, candid, (a, b) => a.ValueOrDefault == b.ValueOrDefault);
		}

		public class AllRawTypes
		{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
			[CandidTypeDefinition("bool")]
			public CandidPrimitive Bool { get; set; }
			[CandidTypeDefinition("nat")]
			public CandidPrimitive Nat { get; set; }
			[CandidTypeDefinition("int")]
			public CandidPrimitive Int { get; set; }
			[CandidTypeDefinition("nat8")]
			public CandidPrimitive Nat8 { get; set; }
			[CandidTypeDefinition("nat16")]
			public CandidPrimitive Nat16 { get; set; }
			[CandidTypeDefinition("nat32")]
			public CandidPrimitive Nat32 { get; set; }
			[CandidTypeDefinition("nat64")]
			public CandidPrimitive Nat64 { get; set; }
			[CandidTypeDefinition("int8")]
			public CandidPrimitive Int8 { get; set; }
			[CandidTypeDefinition("int16")]
			public CandidPrimitive Int16 { get; set; }
			[CandidTypeDefinition("int32")]
			public CandidPrimitive Int32 { get; set; }
			[CandidTypeDefinition("int64")]
			public CandidPrimitive Int64 { get; set; }
			[CandidTypeDefinition("float32")]
			public CandidPrimitive Float32 { get; set; }
			[CandidTypeDefinition("float64")]
			public CandidPrimitive Float64 { get; set; }
			[CandidTypeDefinition("text")]
			public CandidPrimitive Text { get; set; }
			[CandidTypeDefinition("null")]
			public CandidPrimitive Null { get; set; }
			[CandidTypeDefinition("reserved")]
			public CandidPrimitive Reserved { get; set; }
			[CandidTypeDefinition("empty")]
			public CandidPrimitive Empty { get; set; }
			[CandidTypeDefinition("principal")]
			public CandidPrimitive Principal { get; set; }
			[CandidTypeDefinition("service { test : () -> () }")]
			public CandidService Service { get; set; }
			[CandidTypeDefinition("() -> ()")]
			public CandidFunc Func { get; set; }
			[CandidTypeDefinition("opt text")]
			public CandidOptional Opt { get; set; }
			[CandidTypeDefinition("vec text")]
			public CandidVector Vec { get; set; }
			[CandidTypeDefinition("record { text4 : text }")]
			public CandidRecord Rec { get; set; }
			[CandidTypeDefinition("variant { text5 : text }")]
			public CandidVariant Var { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
		}

		[Fact]
		public void AllRawTypes__Record__NoMapping()
		{
			var raw = new AllRawTypes
			{
				Bool = CandidValue.Bool(true),
				Nat = CandidValue.Nat(1),
				Int = CandidValue.Int(-1),
				Nat8 = CandidValue.Nat8(2),
				Nat16 = CandidValue.Nat16(2),
				Nat32 = CandidValue.Nat32(2),
				Nat64 = CandidValue.Nat64(2),
				Int8 = CandidValue.Int8(2),
				Int16 = CandidValue.Int16(2),
				Int32 = CandidValue.Int32(2),
				Int64 = CandidValue.Int64(2),
				Float32 = CandidValue.Float32(3.14f),
				Float64 = CandidValue.Float64(-3.14f),
				Text = CandidValue.Text("text"),
				Null = CandidValue.Null(),
				Reserved = CandidValue.Reserved(),
				Empty = CandidValue.Empty(),
				Principal = CandidValue.Principal(Models.Principal.FromText("rrkah-fqaaa-aaaaa-aaaaq-cai")),
				Service = new CandidService(Models.Principal.FromText("rrkah-fqaaa-aaaaa-aaaaq-cai")),
				Func = new CandidFunc(new CandidService(Models.Principal.FromText("rrkah-fqaaa-aaaaa-aaaaq-cai")), "test"),
				Opt = new CandidOptional(CandidValue.Text("text2")),
				Vec = new CandidVector(new CandidValue[] { CandidValue.Text("text3") }),
				Rec = new CandidRecord(new Dictionary<CandidTag, CandidValue>
				{
					["text4"] = CandidValue.Text("text4")
				}),
				Var = new CandidVariant("text5", CandidValue.Text("text5"))
			};

			var candidValue = new CandidRecord(new Dictionary<CandidTag, CandidValue>
			{
				["Bool"] = raw.Bool,
				["Nat"] = raw.Nat,
				["Int"] = raw.Int,
				["Nat8"] = raw.Nat8,
				["Nat16"] = raw.Nat16,
				["Nat32"] = raw.Nat32,
				["Nat64"] = raw.Nat64,
				["Int8"] = raw.Int8,
				["Int16"] = raw.Int16,
				["Int32"] = raw.Int32,
				["Int64"] = raw.Int64,
				["Float32"] = raw.Float32,
				["Float64"] = raw.Float64,
				["Text"] = raw.Text,
				["Null"] = raw.Null,
				["Reserved"] = raw.Reserved,
				["Empty"] = raw.Empty,
				["Principal"] = raw.Principal,
				["Service"] = raw.Service,
				["Func"] = raw.Func,
				["Opt"] = raw.Opt,
				["Vec"] = raw.Vec,
				["Rec"] = raw.Rec,
				["Var"] = raw.Var
			});
			var funcType = new CandidFuncType(new List<FuncMode>(), new List<CandidType>(), new List<CandidType>());
			var candidType = new CandidRecordType(new Dictionary<CandidTag, CandidType>
			{
				["Bool"] = CandidType.Bool(),
				["Nat"] = CandidType.Nat(),
				["Int"] = CandidType.Int(),
				["Nat8"] = CandidType.Nat8(),
				["Nat16"] = CandidType.Nat16(),
				["Nat32"] = CandidType.Nat32(),
				["Nat64"] = CandidType.Nat64(),
				["Int8"] = CandidType.Int8(),
				["Int16"] = CandidType.Int16(),
				["Int32"] = CandidType.Int32(),
				["Int64"] = CandidType.Int64(),
				["Float32"] = CandidType.Float32(),
				["Float64"] = CandidType.Float64(),
				["Text"] = CandidType.Text(),
				["Null"] = CandidType.Null(),
				["Reserved"] = CandidType.Reserved(),
				["Empty"] = CandidType.Empty(),
				["Principal"] = CandidType.Principal(),
				["Service"] = new CandidServiceType(new Dictionary<CandidId, CandidFuncType>
				{
					[CandidId.Create("test")] = funcType
				}),
				["Func"] = funcType,
				["Opt"] = new CandidOptionalType(CandidType.Text()),
				["Vec"] = new CandidVectorType(CandidType.Text()),
				["Rec"] = new CandidRecordType(new Dictionary<CandidTag, CandidType>
				{
					["text4"] = CandidType.Text()
				}),
				["Var"] = new CandidVariantType(new Dictionary<CandidTag, CandidType>
				{
					["text5"] = CandidType.Text()
				})
			});
			var candid = new CandidTypedValue(candidValue, candidType);

			this.Test(raw, candid, (a, b) =>
			{
				return a.Bool == b.Bool
					&& a.Nat == b.Nat
					&& a.Int == b.Int
					&& a.Nat8 == b.Nat8
					&& a.Nat16 == b.Nat16
					&& a.Nat32 == b.Nat32
					&& a.Nat64 == b.Nat64
					&& a.Int8 == b.Int8
					&& a.Int16 == b.Int16
					&& a.Int32 == b.Int32
					&& a.Int64 == b.Int64
					&& a.Float32 == b.Float32
					&& a.Float64 == b.Float64
					&& a.Text == b.Text
					&& a.Null == b.Null
					&& a.Reserved == b.Reserved
					&& a.Empty == b.Empty
					&& a.Principal == b.Principal
					&& a.Service == b.Service
					&& a.Func == b.Func
					&& a.Opt == b.Opt
					&& a.Vec == b.Vec
					&& a.Rec == b.Rec
					&& a.Var == b.Var;
			});
		}

		[Variant]
		public class RawVariant
		{
			[VariantTagProperty]
			public RawVariantTag Tag { get; set; }
			[VariantValueProperty]
			public object? Value { get; set; }

			[CandidTypeDefinition("() -> ()")]
			public CandidFunc AsFunc()
			{
				return (CandidFunc)this.Value!;
			}

			[CandidTypeDefinition("opt text")]
			public CandidOptional AsOptional()
			{
				return (CandidOptional)this.Value!;
			}
		}
		public enum RawVariantTag
		{
			Func,
			Optional
		}

		[Fact]
		public void RawVariant__NoMapping()
		{
			var value1 = new CandidFunc(new CandidService(Models.Principal.FromText("rrkah-fqaaa-aaaaa-aaaaq-cai")), "test");
			var raw = new RawVariant
			{
				Tag = RawVariantTag.Func,
				Value = value1
			};
			var candidValue = new CandidVariant("Func", value1);
			var candidType = new CandidVariantType(new Dictionary<CandidTag, CandidType>
			{
				["Func"] = new CandidFuncType(new List<FuncMode>(), new List<CandidType>(), new List<CandidType>()),
				["Optional"] = new CandidOptionalType(CandidType.Text())
			});
			var candidTypedValue = new CandidTypedValue(candidValue, candidType);

			var value2 = new CandidOptional(CandidValue.Text("text"));
			var raw2 = new RawVariant
			{
				Tag = RawVariantTag.Optional,
				Value = value2
			};

			var candidValue2 = new CandidVariant("Optional", value2);
			var candidTypedValue2 = new CandidTypedValue(candidValue2, candidType);


			CandidTypedValue actualTypedCandid = CandidConverter.Default.FromTypedObject(raw);
			Assert.Equal(candidTypedValue, actualTypedCandid);

			RawVariant actual = CandidConverter.Default.ToObject<RawVariant>(candidValue);
			Assert.NotNull(actual);
			Assert.Equal(raw.Tag, actual.Tag);
			Assert.Equal(raw.Value, actual.Value);

			CandidTypedValue actualTypedCandid2 = CandidConverter.Default.FromTypedObject(raw2);
			Assert.Equal(candidTypedValue2, actualTypedCandid2);

			RawVariant actual2 = CandidConverter.Default.ToObject<RawVariant>(candidValue2);
			Assert.NotNull(actual2);
			Assert.Equal(raw2.Tag, actual2.Tag);
			Assert.Equal(raw2.Value, actual2.Value);
		}

		[Fact]
		public void ToObject__Invalid_Map_Text_Record__Throws()
		{
			CandidRecord recordValue = new (new Dictionary<CandidTag, CandidValue>
			{
				["test"] = CandidValue.Text("test")
			});
			Assert.Throws<Exception>(() => CandidConverter.Default.ToObject<string>(recordValue));
		}

		[Fact]
		public void ToObject__Invalid_Map_Record_Field__Throws()
		{
			CandidTag stringFieldName = CandidTag.FromName("StringField");
			CandidTag intFieldName = CandidTag.FromName("IntField");
			var fields = new Dictionary<CandidTag, CandidValue>
			{
				{stringFieldName, new CandidOptional(CandidValue.Int64(1))}, // Wrong type/value
				{intFieldName, CandidValue.Int32(2)}
			};
			CandidRecord recordValue = new (fields);


			Assert.Throws<Exception>(() => CandidConverter.Default.ToObject<RecordClass>(recordValue));
		}


		private void Test<T>(T raw, CandidTypedValue candid, Func<T, T, bool> areEqual)
			where T : notnull
		{
			CandidTypedValue actual = CandidConverter.Default.FromTypedObject(raw!);
			Assert.Equal(candid, actual);


			T? obj = CandidConverter.Default.ToObject<T>(candid.Value);
			Assert.NotNull(obj);
			Assert.True(areEqual(raw, obj!));
		}
	}
}
