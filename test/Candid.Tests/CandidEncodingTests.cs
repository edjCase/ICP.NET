using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using EdjCase.ICP.Candid.Utilities;
using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace EdjCase.ICP.Candid.Tests
{
	// https://github.com/dfinity/candid/blob/master/test/prim.test.did
	// https://github.com/dfinity/candid/blob/master/test/reference.test.did
	public class CandidEncodingTests
	{
		[Theory]
		[InlineData(0, "00")]
		[InlineData(1, "01")]
		[InlineData(127, "7F")]
		[InlineData(624485, "E58E26")]
		public void Encode_Nat(ulong natValue, string expectedHex)
		{
			const string expectedPrefix = "00017D";
			var nat = UnboundedUInt.FromUInt64(natValue);
			var candidNat = CandidValue.Nat(nat);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidNat, new CandidPrimitiveType(PrimitiveType.Nat));
		}

		[Fact]
		public void Encode_Nat_Big()
		{
			const string expectedPrefix = "00017D";
			const string expectedHex = "808098F4E9B5CA6A";
			BigInteger bigInteger = 60000000000000000;
			var nat = UnboundedUInt.FromBigInteger(bigInteger);
			var candidNat = CandidValue.Nat(nat);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidNat, new CandidPrimitiveType(PrimitiveType.Nat));
		}

		[Theory]
		[InlineData(0, "0000000000000000")]
		[InlineData(16, "1000000000000000")]
		[InlineData(543210, "EA49080000000000")]
		public void Encode_Nat64(ulong natValue, string expectedHex)
		{
			const string expectedPrefix = "000178";
			var candidNat = CandidValue.Nat64(natValue);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidNat, new CandidPrimitiveType(PrimitiveType.Nat64));
		}

		[Theory]
		[InlineData(0, "00000000")]
		[InlineData(16, "10000000")]
		[InlineData(543210, "EA490800")]
		public void Encode_Nat32(uint natValue, string expectedHex)
		{
			const string expectedPrefix = "000179";
			var candidNat = CandidValue.Nat32(natValue);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidNat, new CandidPrimitiveType(PrimitiveType.Nat32));
		}

		[Theory]
		[InlineData(0, "0000")]
		[InlineData(16, "1000")]
		[InlineData(9999, "0F27")]
		public void Encode_Nat16(ushort natValue, string expectedHex)
		{
			const string expectedPrefix = "00017A";
			var candidNat = CandidValue.Nat16(natValue);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidNat, new CandidPrimitiveType(PrimitiveType.Nat16));
		}

		[Theory]
		[InlineData(0, "00")]
		[InlineData(16, "10")]
		[InlineData(99, "63")]
		public void Encode_Nat8(byte natValue, string expectedHex)
		{
			const string expectedPrefix = "00017B";
			var candidNat = CandidValue.Nat8(natValue);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidNat, new CandidPrimitiveType(PrimitiveType.Nat8));
		}

		[Theory]
		[InlineData(0, "00")]
		[InlineData(16, "10")]
		[InlineData(-4, "7C")]
		[InlineData(-15, "71")]
		[InlineData(-68, "BC7F")]
		[InlineData(624485, "E58E26")]
		[InlineData(-123456, "C0BB78")]
		[InlineData(128, "8001")]
		public void Encode_Int(long intValue, string expectedHex)
		{
			const string expectedPrefix = "00017C";
			var @int = UnboundedInt.FromInt64(intValue);
			var candidInt = CandidValue.Int(@int);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidInt, new CandidPrimitiveType(PrimitiveType.Int));
		}

		[Fact]
		public void Encode_Int_Big()
		{
			const string expectedPrefix = "00017C";
			const string expectedHex = "8080E88B96CAB5957F";
			BigInteger bigInteger = -60000000000000000;
			var @int = UnboundedInt.FromBigInteger(bigInteger);
			var candidInt = CandidValue.Int(@int);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidInt, new CandidPrimitiveType(PrimitiveType.Int));
		}

		[Theory]
		[InlineData(0, "0000000000000000")]
		[InlineData(16, "1000000000000000")]
		[InlineData(-15, "F1FFFFFFFFFFFFFF")]
		[InlineData(4294967295, "FFFFFFFF00000000")]
		public void Encode_Int64(long intValue, string expectedHex)
		{
			const string expectedPrefix = "000174";
			var candidInt = CandidValue.Int64(intValue);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidInt, new CandidPrimitiveType(PrimitiveType.Int64));
		}

		[Theory]
		[InlineData(0, "00000000")]
		[InlineData(16, "10000000")]
		[InlineData(-15, "F1FFFFFF")]
		[InlineData(65535, "FFFF0000")]
		public void Encode_Int32(int intValue, string expectedHex)
		{
			const string expectedPrefix = "000175";
			var candidInt = CandidValue.Int32(intValue);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidInt, new CandidPrimitiveType(PrimitiveType.Int32));
		}

		[Theory]
		[InlineData(0, "0000")]
		[InlineData(16, "1000")]
		[InlineData(-15, "F1FF")]
		[InlineData(9999, "0F27")]
		public void Encode_Int16(short intValue, string expectedHex)
		{
			const string expectedPrefix = "000176";
			var candidInt = CandidValue.Int16(intValue);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidInt, new CandidPrimitiveType(PrimitiveType.Int16));
		}

		[Theory]
		[InlineData(0, "00")]
		[InlineData(16, "10")]
		[InlineData(99, "63")]
		public void Encode_Int8(byte intValue, string expectedHex)
		{
			const string expectedPrefix = "000177";
			var candidInt = CandidValue.Int8((sbyte)intValue);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidInt, new CandidPrimitiveType(PrimitiveType.Int8));
		}
		[Fact]
		public void Encode_Int8_Neg()
		{
			const string expectedPrefix = "000177";
			const string expectedHex = "F1";
			var candidInt = CandidValue.Int8((sbyte)-15);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidInt, new CandidPrimitiveType(PrimitiveType.Int8));
		}

		[Theory]
		[InlineData(1.0, "0000803F")]
		[InlineData(1.23456, "10069E3F")]
		[InlineData(-98765.4321, "B7E6C0C7")]
		public void Encode_Float32(float value, string expectedHex)
		{
			const string expectedPrefix = "000173";
			var candidValue = CandidValue.Float32(value);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, new CandidPrimitiveType(PrimitiveType.Float32));
		}

		[Theory]
		[InlineData(1.0, "000000000000F03F")]
		[InlineData(1.23456, "38328FFCC1C0F33F")]
		[InlineData(-98765.4321, "8AB0E1E9D61CF8C0")]
		public void Encode_Float64(double value, string expectedHex)
		{
			const string expectedPrefix = "000172";
			var candidValue = CandidValue.Float64(value);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, new CandidPrimitiveType(PrimitiveType.Float64));
		}

		[Theory]
		[InlineData(false, "00")]
		[InlineData(true, "01")]
		public void Encode_Bool(bool value, string expectedHex)
		{
			const string expectedPrefix = "00017E";
			var candidValue = CandidValue.Bool(value);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, new CandidPrimitiveType(PrimitiveType.Bool));
		}

		[Theory]
		[InlineData("", "00")]
		[InlineData("A", "0141")]
		[InlineData("The quick brown fox jumps over the lazy dog", "2B54686520717569636B2062726F776E20666F78206A756D7073206F76657220746865206C617A7920646F67")]
		public void Encode_Text(string value, string expectedHex)
		{
			const string expectedPrefix = "000171";
			var candidValue = CandidValue.Text(value);
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, new CandidPrimitiveType(PrimitiveType.Text));
		}

		[Fact]
		public void Encode_Null()
		{
			const string expectedPrefix = "00017F";
			var candidValue = CandidValue.Null();
			string expectedHex = "";
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, new CandidPrimitiveType(PrimitiveType.Null));
		}

		[Fact]
		public void Encode_Reserved()
		{
			const string expectedPrefix = "000170";
			var candidValue = CandidValue.Reserved();
			string expectedHex = "";
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, new CandidPrimitiveType(PrimitiveType.Reserved));
		}

		[Fact]
		public void Encode_Opt()
		{
			string expectedPrefix = "016E7C";
			var candidValue = new CandidOptional(null);
			string expectedHex = "010000";
			var typeDef = new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Int)); // opt int
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, typeDef);

			candidValue = new CandidOptional(CandidValue.Int(UnboundedInt.FromInt64(42)));
			expectedHex = "0100012A";
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, typeDef);


			candidValue = new CandidOptional(candidValue);
			// TODO docs say this but I order the type table differently. Should work though?
			//expectedPrefix = "026E016E7C";
			//expectedHex = "010001012A";
			expectedPrefix = "026E7C6E00";
			expectedHex = "010101012A";
			typeDef = new CandidOptionalType(typeDef); // opt opt int
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, typeDef);
		}

		[Fact]
		public void Encode_Vec()
		{
			var candidValue = new CandidVector(new CandidValue[0]);
			string expectedPrefix = "016D7C0100";
			string expectedHex = "00";
			var typeDef = new CandidVectorType(new CandidPrimitiveType(PrimitiveType.Int));
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, typeDef);

			var vector = new CandidPrimitive[]
			{
				CandidValue.Int(UnboundedInt.FromInt64(1)),
				CandidValue.Int(UnboundedInt.FromInt64(2))
			};
			candidValue = new CandidVector(vector);
			expectedHex = "020102";
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, typeDef);
		}

		[Theory]
		[InlineData("id", 23515)]
		[InlineData("", 0)]
		[InlineData("description", 1595738364)]
		[InlineData("_1.23_", 1360503298)]
		public void Encode_Label(string name, uint hashedName)
		{
			uint digest = CandidTag.HashName(name);
			Assert.Equal(hashedName, digest);

		}

		[Fact]
		public void Encode_Record_Ids()
		{
			var candidValue = new CandidRecord(new Dictionary<CandidTag, CandidValue>
			{
				{new CandidTag(1), CandidValue.Int(UnboundedInt.FromInt64(42)) },
			});
			string expectedPrefix = "";
			string expectedHex = "016C01017C01002A";
			var typeDef = new CandidRecordType(new Dictionary<CandidTag, CandidType>
			{
				{new CandidTag(1), new CandidPrimitiveType(PrimitiveType.Int) },
			});
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, typeDef);
		}

		[Fact]
		public void Encode_Record_Named()
		{
			var candidValue = new CandidRecord(new Dictionary<CandidTag, CandidValue>
			{
				{CandidTag.FromName("foo"),CandidValue.Int(UnboundedInt.FromInt64(42)) },
				{CandidTag.FromName("bar"), CandidValue.Bool(true) }
			});
			string expectedPrefix = "";
			string expectedHex = "016C02D3E3AA027E868EB7027C0100012A";
			var typeDef = new CandidRecordType(new Dictionary<CandidTag, CandidType>
			{
				{CandidTag.FromName("foo"), new CandidPrimitiveType(PrimitiveType.Int) },
				{CandidTag.FromName("bar"), new CandidPrimitiveType(PrimitiveType.Bool) },
			});
			TestUtil.AssertEncodedCandid(expectedHex, expectedPrefix, candidValue, typeDef);
		}

		[Fact]
		public void Encode_Variant()
		{
			//4449444C Magic header
			//03 3 types
			//6B -21 Variant [0]
			//02 variant length
			//9CC201 'ok'
			//02 ref
			//E58EB402 'err'
			//01 ref
			//6B -21 Variant [1]
			//01 variant length
			//CFA0DEF206 'NotFound'
			//7F -1 Null
			//6C -20 record [2]
			//05 record length
			//C4A7C9A101 'total'
			//79 -7 NAT32
			//DC8BD3F401 'desktop'
			//79 -7 NAT32
			//8D98F3E704 'time'
			//7C INT
			//E2D8DEFB0B 'mobile'
			//79 -7 NAT32
			//89FB97EB0E 'route'
			//71 -15 Text
			//01 Arg count
			//00 ref
			//01 variant index/tag
			//00 variant value
			const string hex = "4449444C036B029CC20102E58EB402016B01CFA0DEF2067F6C05C4A7C9A10179DC8BD3F401798D98F3E7047CE2D8DEFB0B7989FB97EB0E7101000100";
			var value1 = new CandidVariant("err", new CandidVariant("NotFound"));
			var type1 = new CandidVariantType(new Dictionary<CandidTag, CandidType>
			{
				{
					CandidTag.FromName("ok"),
					new CandidRecordType(new Dictionary<CandidTag, CandidType>
					{
						{ "total", new CandidPrimitiveType(PrimitiveType.Nat32) },
						{ "desktop", new CandidPrimitiveType(PrimitiveType.Nat32) },
						{ "time", new CandidPrimitiveType(PrimitiveType.Int) },
						{ "mobile", new CandidPrimitiveType(PrimitiveType.Nat32) },
						{ "route", new CandidPrimitiveType(PrimitiveType.Text) }
					})
				},
				{
					CandidTag.FromName("err"),
					new CandidVariantType(new Dictionary<CandidTag, CandidType>
					{
						{"NotFound", new CandidPrimitiveType(PrimitiveType.Null) }
					})
				}
			});
			var expectedArg = CandidArg.FromCandid(new List<CandidTypedValue>
			{
				CandidTypedValue.FromValueAndType(value1, type1)
			});

			byte[] actualBytes = ByteUtil.FromHexString(hex);
			CandidArg actualArg = CandidArg.FromBytes(actualBytes);

			Assert.Equal(expectedArg, actualArg);
		}
		[Fact]
		public void Encode_Record_Recursive()
		{
			//4449444C Magic header
			//02 2 types -- Compound Types start
			//6E Opt [0]
			//01 Ref [1]
			//6C -20 Record [1]
			//01 record length
			//A78A839908 'selfRef'
			//00 Ref [0]
			//01 Arg count -- Arg types start
			//01 Ref [1]
			//01 Opt has value -- Values start
			//00 Opt does not have value
			const string actualHex = "4449444C026E016C01A78A8399080001010100";

			var value1 = new CandidRecord(new Dictionary<CandidTag, CandidValue>
			{
				{
					CandidTag.FromName("selfRef"),
					new CandidOptional(new CandidRecord(new Dictionary<CandidTag, CandidValue>
					{
						{
							CandidTag.FromName("selfRef"),
							new CandidOptional()
						}
					}))
				}
			});
			var referenceId = CandidId.Create("rec_1");
			var type1 = new CandidRecordType(new Dictionary<CandidTag, CandidType>
			{
				{
					CandidTag.FromName("selfRef"),
					new CandidOptionalType(new CandidReferenceType(referenceId))
				}
			}, referenceId);
			var expectedArg = CandidArg.FromCandid(new List<CandidTypedValue>
			{
				CandidTypedValue.FromValueAndType(value1, type1)
			});

			byte[] actualBytes = Convert.FromHexString(actualHex);
			CandidArg actualArg = CandidArg.FromBytes(actualBytes);

			string expectedHex = Convert.ToHexString(expectedArg.Encode());

#pragma warning disable xUnit2000 // Constants and literals should be the expected argument
			Assert.Equal(expectedHex, actualHex);
#pragma warning restore xUnit2000 // Constants and literals should be the expected argument
			Assert.Equal(expectedArg, actualArg);
		}

		[Fact]
		public void Encode_Service_Example1()
		{
			const string hex = "4449444C026A000001016901076D6574686F6431000101010A0000000001E0102A0101";
			var value1 = new CandidService(Principal.FromText("bpo6s-4qaaa-aaaap-acava-cai"));
			var type1 = new CandidServiceType(new Dictionary<CandidId, CandidFuncType>{
				{
					CandidId.Create("method1"),
					new CandidFuncType(new List<FuncMode>{FuncMode.Query}, new List<CandidType>(), new List<CandidType>())
				},
			});
			var expectedArg = CandidArg.FromCandid(new List<CandidTypedValue>
			{
				CandidTypedValue.FromValueAndType(value1, type1)
			});


			byte[] actualBytes = ByteUtil.FromHexString(hex);
			CandidArg actualArg = CandidArg.FromBytes(actualBytes);

			Assert.Equal(expectedArg, actualArg);

			string actualHex = ByteUtil.ToHexString(expectedArg.Encode());

			Assert.Equal(hex, actualHex);
		}


		[Fact]
		public void EncodeDecode_1()
		{
			var content = new CandidRecord(new Dictionary<CandidTag, CandidValue>
			{
				{
					CandidTag.FromName("title"),
					CandidValue.Text("The Title")
				},
				{
					CandidTag.FromName("imageLink"),
					new CandidOptional(CandidValue.Text("https://google.com"))
				},
				{
					CandidTag.FromName("body"),
					new CandidRecord(new Dictionary<CandidTag, CandidValue>
					{
						{
							CandidTag.FromName("value"),
							CandidValue.Text("<h1>Hello</h1>")
						},
						{
							CandidTag.FromName("format"),
							new CandidOptional(CandidValue.Text("text/html"))
						}
					})
				},
				{
					CandidTag.FromName("date"),
					CandidValue.Int(0)
				},
				{
					CandidTag.FromName("link"),
					CandidValue.Text("https://google.com")
				},
				{
					CandidTag.FromName("language"),
					new CandidOptional(CandidValue.Text("en-us"))
				},
				{
					CandidTag.FromName("authors"),
					new CandidVector(new CandidValue[]
					{
						new CandidVariant("name", CandidValue.Text("author1")),
						new CandidVariant("name", CandidValue.Text("author2"))
					})
				}
			});
			var contentType = new CandidRecordType(new Dictionary<CandidTag, CandidType>
			{
				{
					CandidTag.FromName("title"),
					new CandidPrimitiveType(PrimitiveType.Text)
				},
				{
					CandidTag.FromName("imageLink"),
					new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Text))
				},
				{
					CandidTag.FromName("body"),
					new CandidRecordType(new Dictionary<CandidTag, CandidType>
					{
						{
							CandidTag.FromName("value"),
							new CandidPrimitiveType(PrimitiveType.Text)
						},
						{
							CandidTag.FromName("format"),
							new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Text))
						}
					})
				},
				{
					CandidTag.FromName("date"),
					new CandidPrimitiveType(PrimitiveType.Int)
				},
				{
					CandidTag.FromName("link"),
					new CandidPrimitiveType(PrimitiveType.Text)
				},
				{
					CandidTag.FromName("language"),
					new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Text))
				},
				{
					CandidTag.FromName("authors"),
					new CandidVectorType(new CandidVariantType(new Dictionary<CandidTag, CandidType>
					{
						{
							CandidTag.FromName("name"),
							new CandidPrimitiveType(PrimitiveType.Text)
						},
						{
							CandidTag.FromName("identity"),
							new CandidPrimitiveType(PrimitiveType.Principal)
						},
						{
							CandidTag.FromName("handle"),
							new CandidPrimitiveType(PrimitiveType.Text)
						}
					}))
				}
			});
			var arg = CandidArg.FromCandid(
				CandidTypedValue.FromValueAndType(
					CandidValue.Text("https://www.theverge.com/rss/index.xml"),
					new CandidPrimitiveType(PrimitiveType.Text)
				),
				CandidTypedValue.FromValueAndType(content, contentType)
			);

			byte[] encodedBytes = arg.Encode();
			CandidArg decodedArg = CandidArg.FromBytes(encodedBytes);

			Assert.Equal(arg, decodedArg);

		}
	}
}
