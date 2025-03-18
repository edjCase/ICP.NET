using BenchmarkDotNet.Attributes;
using EdjCase.ICP.BLS;
using EdjCase.ICP.BLS.Models;
using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Performance.Tests.Benchmarks
{
	public class SignatureBenchmarks
	{
		private readonly byte[] publicKey;
		private readonly byte[] msgHash;
		private readonly byte[] signature;

		public SignatureBenchmarks()
		{
			//this.publicKey = ByteUtil.FromHexString("a7623a93cdb56c4d23d99c14216afaab3dfd6d4f9eb3db23d038280b6d5cb2caaee2a19dd92c9df7001dede23bf036bc0f33982dfb41e8fa9b8e96b5dc3e83d55ca4dd146c7eb2e8b6859cb5a5db815db86810b8d12cee1588b5dbf34a4dc9a5");
			//this.msgHash = ByteUtil.FromHexString("hello");
			//this.signature = ByteUtil.FromHexString("b89e13a212c830586eaa9ad53946cd968718ebecc27eda849d9232673dcd4f440e8b5df39bf14a88048c15e16cbcaabe");
			this.publicKey = ByteUtil.FromHexString("b2be11dc8e54ee74dbc07569fd74fe03b5f52ad71cd49a8579b6c6387891f5a20ad980ec2747618c1b9ad35846a68a3e");
			this.msgHash = ByteUtil.FromHexString("");
			this.signature = ByteUtil.FromHexString("b53cfdf8b488a286df1ed20432e2bbc4e6361003757dfda3a4fd6cd98de95e5513f7c448d70b2681e14547a6ced47e7c10e28432e8abcb34de1dc28f39328fd2a13db12a4c6a30bd17b0e42881a429003e4c24583ba0f29a40fd836cf05e1a40");
		}

		[Benchmark]
		public void Verify()
		{
			DefaultBlsCryptograhy.VerifySignature(this.publicKey, this.msgHash, this.signature);
		}

	}
}
