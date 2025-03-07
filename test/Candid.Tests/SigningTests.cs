using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Agent.Requests;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using EdjCase.ICP.Candid.Utilities;
using Snapshooter.Xunit;
using System.Collections.Generic;
using Xunit;
using EdjCase.ICP.Agent.Models;
using System.Threading.Tasks;
using EdjCase.ICP.Agent;

namespace EdjCase.ICP.Candid.Tests
{
	public class SigningTests
	{
		[Fact]
		public void Sign_QueryRequest()
		{
			Principal canisterId = Principal.FromText("rrkah-fqaaa-aaaaa-aaaaq-cai");
			string method = "get_proposal_info";
			CandidArg arg = CandidArg.FromCandid(
				CandidTypedValue.FromValueAndType(
					value: CandidValue.Nat64(1),
					type: new CandidPrimitiveType(PrimitiveType.Nat64)
				)
			);
			string publicKeyHex = "302a300506032b6570032100029402057ca0c30e54a2cdd13de7c4ebb0b5636797cc6424159e294aeb060804";
			var publicKey = ByteUtil.FromHexString(publicKeyHex);
			string privateKeyHex = "1180beb163ce5f4b477d3f217c889866cd549bfbac2e8f3ed53b8739084acc15029402057ca0c30e54a2cdd13de7c4ebb0b5636797cc6424159e294aeb060804";
			var innerIdentity = new Ed25519Identity(publicKey, ByteUtil.FromHexString(privateKeyHex));

			var delegationExpr = ICTimestamp.FromNanoSeconds(1654641739465407291);
			var delegation = new Delegation(innerIdentity.PublicKey, delegationExpr);
			var signature = ByteUtil.FromHexString("d9d9f7a26b6365727469666963617465590223d9d9f7a2647472656583018301830183024863616e69737465728301830182045820265b79fe34b1d0d43f23d605e829e12703f954871c64cb5ff8ae6eceaa82f9eb830182045820f3cc6f6b9a49814f7f6624e7dc42926052bb2e72b7b48b6ccc2252a8fe1b18458301820458202946d643e18744dcfbf78b5c331f9e104109b08e2390854868fb9397872e8fe483024a0000000000000007010183018301830183024e6365727469666965645f6461746182035820f53e310b46d3ffd1b24376143afcf7b7840c9b1f9f5f1c7ca863b3af8df7ada482045820fd5b59459758c8afecaf7285da359e4b5adb945fb86a3c1f0efd996c21a9693882045820e872843059989fd8f4b051d1c420833575932e70cc6f0e2d0ef93d461da03f2982045820fd19809310532518d4d19506c2a1f6dc72ea1cedba901fa0a65460e7ffe6f95182045820564f4b2424417d1fd758187d114f2f305c41ead8eea3c3d706bd135d0adb71d5820458203b3e0bb27303bdc6f8bf37a349e708302e79e54ce7dca24aa7acefb8c3adca43820458206a5cbdfe12ceda534b9d6d822c6b4fc100fe0642a6df77ab83cb11774bb967838301820458204e0b0aef49a922caf9398d9af05aa4c5acf254fc2e0062954c4d69c87c3991d383024474696d65820349bbf6e68cbbb697fb16697369676e617475726558308539463bea3a594a3ebeba5b3ce5ddf26658add03bd7a5e9900641dd8063e64055e8f0d4c227a089de0649e68afa32b764747265658301820458203b49d68788166b4d130cfcba10fefa7f44d21f336b06de3091cbfcdf5d86b2628302437369678301830183018204582035f8452cafd6a69ef6b0eeb31d7ecd84575d5900874b392b2b47d44f0cfd9ef083018204582063058aff68d03011f26937f2b1fa20917395c37d6e24721690c8623f3ee502698302582040899e6d464544bb0c7dd6c46d48d869ac6eba6c99c54c33c3d9764ac31070968302582042ed7cca23fc8ae962dea8d6fb0f2ec59524f8000eabfbde60f0315cf623928d820340820458200ed8addc6a59ceecdc5caac2ebef5c8d5ab7243371e696114a15b6aa65cf358c820458207f4076db3ccdf0b56d67fe1225da71ab2d327c600d4486095eb933275e038597");

			var delegations = new List<SignedDelegation>
			{
				new SignedDelegation(delegation, signature)
			};
			var chainPublicKey = SubjectPublicKeyInfo.Ed25519(ByteUtil.FromHexString("303c300c060a2b0601040183b8430102032c000a00000000000000070101a451d1829b843e2aabdd49ea590668978a73612067bdde0b8502f844452a7558"));
			var chain = new DelegationChain(chainPublicKey, delegations);
			IIdentity identity = new DelegationIdentity(innerIdentity, chain);
			var sender = identity.GetPublicKey().ToPrincipal();
			var ingressExpiry = ICTimestamp.FromNanoSeconds(1654598046354206365);
			var request = new QueryRequest(canisterId, method, arg, sender, ingressExpiry);

			SignedRequest<QueryRequest> signedContent = identity.Sign(request);

			string signatureHex = ByteUtil.ToHexString(signedContent.SenderSignature!);
			Snapshot.Match(signatureHex);
		}
	}
}
