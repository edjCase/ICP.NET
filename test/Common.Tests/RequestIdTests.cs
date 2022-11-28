using EdjCase.ICP.Agent;
using EdjCase.ICP.Agent.Auth;
using EdjCase.ICP.Agent.Identity;
using EdjCase.ICP.Agent.Requests;
using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using EdjCase.ICP.Candid.Utilities;
using Snapshooter.Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace ICP.Candid.Tests
{
	public class RequestIdTests
	{
		private IHashFunction  sha256 = SHA256HashFunction.Create();

		[Fact]
		public void RequestId_Dict()
		{
			RequestId requestId = RequestId.FromObject(new Dictionary<string, IHashable>
			{
				{"arg", new byte[]{68, 73, 68, 76, 0, 1, 121, 54, 215, 245, 143}.ToHashable() },
				{"canister_id", Principal.FromRaw(new byte[]{0, 0, 0, 0, 0, 0, 0, 12, 1, 1}) },
				{"ingress_expiry", ICTimestamp.FromNanoSeconds(1669488594991000000) },
				{"method_name", "deleteSavedItem".ToHashable() },
				{"nonce", new byte[]{0, 0, 1, 132, 181, 66, 179, 129, 253, 30, 229, 15, 18, 153, 227, 38}.ToHashable() },
				{"request_type", "call".ToHashable() },
				{"sender", Principal.FromRaw(new byte[]{ 1, 21, 182, 196, 80, 130, 54, 35, 242, 253, 87, 201, 224, 138, 44, 199, 161, 21, 101, 92, 106, 37, 214, 170, 254, 173, 248, 228, 2}) }
			}, this.sha256);
			var expected = new byte[] { 26, 131, 195, 163, 207, 0, 163, 105, 137, 221, 27, 79, 139, 36, 141, 141, 161, 176, 173, 222, 165, 213, 153, 232, 98, 36, 205, 164, 71, 128, 210, 221 };
			Assert.Equal(expected, requestId.RawValue);
		}

		[Fact]
		public void RequestId_CallRequest()
		{
			var sha256 = SHA256HashFunction.Create();
			var arg = CandidArg.FromCandid(
				CandidValueWithType.FromValueAndType(
					CandidPrimitive.Text("https://www.theverge.com/rss/index.xml"),
					new CandidPrimitiveType(PrimitiveType.Text)
				),
				CandidValueWithType.FromValueAndType(
					new CandidRecord(new Dictionary<CandidTag, CandidValue>
					{
							{
								CandidTag.FromName("title"),
								CandidPrimitive.Text("The Title")
							},
							{
								CandidTag.FromName("body"),
								new CandidRecord(new Dictionary<CandidTag, CandidValue>
								{
									{
										CandidTag.FromName("format"),
										new CandidOptional(CandidPrimitive.Text("text/html"))
									},
									{
										CandidTag.FromName("value"),
										CandidPrimitive.Text("<h1>Hello</h1>")
									}
								})
							},
							{
								CandidTag.FromName("link"),
								CandidPrimitive.Text("https://google.com")
							},
							{
								CandidTag.FromName("authors"),
								new CandidVector(new CandidValue[]
								{
									new CandidVariant("name", CandidPrimitive.Text("author1")),
									new CandidVariant("name", CandidPrimitive.Text("author2"))
								})
							},
							{
								CandidTag.FromName("imageLink"),
								new CandidOptional(CandidPrimitive.Text("https://google.com"))
							},
							{
								CandidTag.FromName("language"),
								new CandidOptional(CandidPrimitive.Text("en-us"))
							},
							{
								CandidTag.FromName("date"),
								CandidPrimitive.Nat(0)
							}
							}),
							new CandidRecordType(new Dictionary<CandidTag, CandidType>
							{
							{
								CandidTag.FromName("title"),
								new CandidPrimitiveType(PrimitiveType.Text)
							},
							{
								CandidTag.FromName("body"),
								new CandidRecordType(new Dictionary<CandidTag, CandidType>
								{
									{
										CandidTag.FromName("format"),
										new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Text))
									},
									{
										CandidTag.FromName("value"),
										new CandidPrimitiveType(PrimitiveType.Text)
									}
								})
							},
							{
								CandidTag.FromName("link"),
								new CandidPrimitiveType(PrimitiveType.Text)
							},
							{
								CandidTag.FromName("authors"),
								new CandidVectorType(new CandidVariantType(new Dictionary<CandidTag, CandidType>
								{
									{
										CandidTag.FromName("name"),
										new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Text))
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
							},
							{
								CandidTag.FromName("imageLink"),
								new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Text))
							},
							{
								CandidTag.FromName("language"),
								new CandidOptionalType(new CandidPrimitiveType(PrimitiveType.Text))
							},
							{
								CandidTag.FromName("date"),
								new CandidPrimitiveType(PrimitiveType.Nat)
							}
					})
				)
			);
			Principal canisterId = Principal.FromText("qaa6y-5yaaa-aaaaa-aaafa-cai");
			string method = "push";
			Principal sender = Principal.Anonymous();
			var ingressExpiry = ICTimestamp.FromNanoSeconds(1669529380992000000);
			var nonce = new byte[] { 0, 0, 1, 132, 183, 176, 255, 1, 91, 241, 252, 187, 142, 76, 79, 198 };
			var request = new CallRequest(canisterId, method, arg, sender, ingressExpiry, nonce);
			Dictionary<string, IHashable> hashable = request.BuildHashableItem();
			RequestId id = RequestId.FromObject(hashable, this.sha256);

			byte[] expected = new byte[] { 78, 111, 64, 56, 245, 197, 70, 19, 219, 167, 64, 89, 210, 152, 93, 11, 111, 75, 32, 58, 187, 10, 174, 154, 60, 110, 249, 101, 253, 160, 253, 214 };
			Assert.Equal(expected, id.RawValue);
		}
	}
}