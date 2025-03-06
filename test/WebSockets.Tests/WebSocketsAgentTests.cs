using EdjCase.ICP.Agent;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.BLS;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.WebSockets;
using Moq;
using EdjCase.ICP.Candid.Utilities;
using EdjCase.ICP.WebSockets.Models;
using System.Formats.Cbor;
using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Candid.Crypto;
using EdjCase.ICP.Candid;

namespace WebSockets.Tests
{
	public class WebSocketsAgentTests
	{
		private class TestMessage
		{
			public string? Message { get; set; }
		}
		private static Principal gatewayPrincipal = Principal.FromHex("505A46A6746E7D266C3301A70CDA43E072476D1736EACAFD7327BEF802");
		private static Principal canisterId = Principal.FromText("j4n55-giaaa-aaaap-qb3wq-cai");
		private static Uri gatewayUri = new Uri("wss://gateway.fake");
		private static SubjectPublicKeyInfo rootPublicKey = SubjectPublicKeyInfo.MainNetRootPublicKey;
		private static IIdentity identity = IdentityUtil.GenerateEd25519Identity();
		private static Principal clientPrincipal = identity.GetPrincipal();

		[Fact]
		public async Task ValidCallAndClose()
		{
			int onMessageCallCount = 0;
			int onOpenCallCount = 0;
			int onErrorCallCount = 0;
			int onCloseCallCount = 0;

			ulong? clientNonce = null;


			var clientMock = new Mock<IWebSocketClient>(MockBehavior.Strict);
			clientMock
				.Setup(client => client.ConnectAsync(gatewayUri, It.IsAny<CancellationToken>()))
				.Returns(Task.CompletedTask);
			clientMock
				.Setup(client => client.Dispose());
			clientMock
				.Setup(client => client.SendAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
				.Returns(Task.CompletedTask);
			clientMock
				.Setup(client => client.CloseAsync(It.IsAny<CancellationToken>(), It.IsAny<string?>()))
				.Returns(Task.CompletedTask);
			clientMock
				.SetupSequence(client => client.ReceiveAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(() => {
					// Handshake: 55799({"gateway_principal": h'...'})
					byte[] handshakeBytes = ByteUtil.FromHexString("D9D9F7A171676174657761795F7072696E636970616C581D505A46A6746E7D266C3301A70CDA43E072476D1736EACAFD7327BEF802");
					return (handshakeBytes, false);
				})
				.ReturnsAsync(() => {
					// Open
					ClientKey clientKey = new()
					{
						Id = clientPrincipal,
						Nonce = clientNonce!.Value
					};
					OpenMessage openMessage = new()
					{
						ClientKey = clientKey
					};
					ServiceMessage serviceMessage = new(ServiceMessageTag.OpenMessage, openMessage);

					byte[] openMessageBytes = this.BuildIncomingMessage(
						1,
						clientKey,
						canisterId,
						serviceMessage,
						isServiceMessage: true
					);
					return (openMessageBytes, false);
				})
				.ReturnsAsync(() => {
					// Message
					ClientKey clientKey = new()
					{
						Id = clientPrincipal,
						Nonce = clientNonce!.Value
					};
					TestMessage data = new()
					{
						Message = "test"
					};
					byte[] appMessageBytes = this.BuildIncomingMessage(
						2,
						clientKey,
						canisterId,
						data,
						isServiceMessage: false
					);
					return (appMessageBytes, false);
				})
				.ReturnsAsync(() => {
					// Ack
					ClientKey clientKey = new()
					{
						Id = clientPrincipal,
						Nonce = clientNonce!.Value
					};
					AckMessage ackMessage = new()
					{
						LastIncomingSequenceNumber = 1
					};
					ServiceMessage serviceMessage = new(ServiceMessageTag.AckMessage, ackMessage);

					byte[] openMessageBytes = this.BuildIncomingMessage(
						3,
						clientKey,
						canisterId,
						serviceMessage,
						isServiceMessage: true
					);
					return (openMessageBytes, false);
				})
				.ReturnsAsync(() => {
					// Close
					ClientKey clientKey = new()
					{
						Id = clientPrincipal,
						Nonce = clientNonce!.Value
					};

					byte[] closeMessageBytes = this.BuildIncomingMessage(
						4,
						clientKey,
						canisterId,
						"",
						isServiceMessage: true
					);
					return (closeMessageBytes, true);
				});
			clientMock
				.SetupGet(client => client.IsOpen)
				.Returns(() => onCloseCallCount <= 0);

			Action<TestMessage> onMessage = (TestMessage message) =>
			{
				Assert.Equal("test", message.Message);
				onMessageCallCount += 1;
			};
			Action onOpen = () => { onOpenCallCount += 1; };
			Action<Exception> onError = (Exception exception) => {
				Assert.Fail(exception.ToString());
			};
			Action onClose = () => { onCloseCallCount += 1; };

			WebSocketAgent<TestMessage> agent = new(
				canisterId,
				gatewayUri,
				rootPublicKey,
				identity,
				clientMock.Object,
				onMessage,
				onOpen,
				onError,
				onClose,
				customConverter: null
			);
			clientNonce = agent.ClientNonce;
			while (agent.IsOpen)
			{
				await agent.ReceiveNextAsync();
				clientNonce = agent.ClientNonce;
			}
			Assert.Equal(1, onOpenCallCount);
			Assert.Equal(1, onMessageCallCount);
			Assert.Equal(0, onErrorCallCount);
			Assert.Equal(1, onCloseCallCount);
		}

		[Fact]
		public async Task InvalidAck_Close()
		{
			int onMessageCallCount = 0;
			int onOpenCallCount = 0;
			int onErrorCallCount = 0;
			int onCloseCallCount = 0;

			ulong? clientNonce = null;


			var clientMock = new Mock<IWebSocketClient>(MockBehavior.Strict);
			clientMock
				.Setup(client => client.ConnectAsync(gatewayUri, It.IsAny<CancellationToken>()))
				.Returns(Task.CompletedTask);
			clientMock
				.Setup(client => client.Dispose());
			clientMock
				.Setup(client => client.SendAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
				.Returns(Task.CompletedTask);
			clientMock
				.Setup(client => client.CloseAsync(It.IsAny<CancellationToken>(), It.IsAny<string?>()))
				.Returns(Task.CompletedTask);
			clientMock
				.SetupSequence(client => client.ReceiveAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(() =>
				{
					// Handshake: 55799({"gateway_principal": h'...'})
					byte[] handshakeBytes = ByteUtil.FromHexString("D9D9F7A171676174657761795F7072696E636970616C581D505A46A6746E7D266C3301A70CDA43E072476D1736EACAFD7327BEF802");
					return (handshakeBytes, false);
				})
				.ReturnsAsync(() =>
				{
					// Open
					ClientKey clientKey = new()
					{
						Id = clientPrincipal,
						Nonce = clientNonce!.Value
					};
					OpenMessage openMessage = new()
					{
						ClientKey = clientKey
					};
					ServiceMessage serviceMessage = new(ServiceMessageTag.OpenMessage, openMessage);

					byte[] openMessageBytes = this.BuildIncomingMessage(
						1,
						clientKey,
						canisterId,
						serviceMessage,
						isServiceMessage: true
					);
					return (openMessageBytes, false);
				})
				.ReturnsAsync(() =>
				{
					// Message
					ClientKey clientKey = new()
					{
						Id = clientPrincipal,
						Nonce = clientNonce!.Value
					};
					TestMessage data = new()
					{
						Message = "test"
					};
					byte[] appMessageBytes = this.BuildIncomingMessage(
						2,
						clientKey,
						canisterId,
						data,
						isServiceMessage: false
					);
					return (appMessageBytes, false);
				})
				.ReturnsAsync(() =>
				{
					// Ack
					ClientKey clientKey = new()
					{
						Id = clientPrincipal,
						Nonce = clientNonce!.Value
					};
					AckMessage ackMessage = new()
					{
						LastIncomingSequenceNumber = 2
					};
					ServiceMessage serviceMessage = new(ServiceMessageTag.AckMessage, ackMessage);

					byte[] openMessageBytes = this.BuildIncomingMessage(
						3,
						clientKey,
						canisterId,
						serviceMessage,
						isServiceMessage: true
					);
					return (openMessageBytes, false);
				});
			clientMock
				.SetupGet(client => client.IsOpen)
				.Returns(() => onCloseCallCount <= 0 && onErrorCallCount <= 0);

			Action<TestMessage> onMessage = (TestMessage message) =>
			{
				Assert.Equal("test", message.Message);
				onMessageCallCount += 1;
			};
			Action onOpen = () => { onOpenCallCount += 1; };
			Action<Exception> onError = (Exception exception) => {
				Assert.Contains("Ack sequence is invalid", exception.ToString());
				onErrorCallCount += 1;
			};
			Action onClose = () => { onCloseCallCount += 1; };

			WebSocketAgent<TestMessage> agent = new(
				canisterId,
				gatewayUri,
				rootPublicKey,
				identity,
				clientMock.Object,
				onMessage,
				onOpen,
				onError,
				onClose,
				customConverter: null
			);
			clientNonce = agent.ClientNonce;
			while (agent.IsOpen)
			{
				await agent.ReceiveNextAsync();
				clientNonce = agent.ClientNonce;
			}
			Assert.Equal(1, onOpenCallCount);
			Assert.Equal(1, onMessageCallCount);
			Assert.Equal(1, onErrorCallCount);
			Assert.Equal(0, onCloseCallCount);
		}


		private byte[] BuildIncomingMessage<T>(
			ulong sequenceNumber,
			ClientKey clientKey,
			Principal canisterId,
			T data,
			bool isServiceMessage
		) where T : notnull
		{

			CandidArg arg = CandidArg.FromCandid(
				new List<CandidTypedValue>
				{
					CandidConverter.Default.FromTypedObject(data)
				}
			);
			byte[] dataBytes = arg.Encode();
			ICTimestamp.Now().NanoSeconds.TryToUInt64(out ulong timestamp);
			WebSocketMessage message = new(sequenceNumber, dataBytes, clientKey, timestamp, isServiceMessage);

			CborWriter cborWriter = new();
			cborWriter.WriteTag(CborTag.SelfDescribeCbor);
			message.ToCbor(cborWriter);
			byte[] content = cborWriter.Encode();
			byte[] contentHash = SHA256HashFunction.Create().ComputeHash(content);
			string clientKeyString = $"{clientKey.Id}_{clientKey.Nonce}";
			HashTree tree = HashTree.Labeled(
				"websocket",
				HashTree.Labeled(
					clientKeyString,
					HashTree.Leaf(contentHash)
				)
			);
			byte[] treeRootHash = tree.BuildRootHash();
			HashTree certTree = HashTree.Labeled(
				"canister",
				HashTree.Labeled(
					canisterId.Raw,
					HashTree.Labeled(
						"certified_data",
						HashTree.Leaf(treeRootHash)
					)
				)
			);
			byte[] signature = new byte[32];
			Certificate cert = new Certificate(certTree, signature);
			cborWriter = new();
			cert.ToCbor(cborWriter);
			byte[] certBytes = cborWriter.Encode();

			cborWriter = new ();
			cborWriter.WriteTag(CborTag.SelfDescribeCbor);
			Certificate.TreeToCborInternal(cborWriter, tree);
			byte[] treeBytes = cborWriter.Encode();


			ClientIncomingMessage incomingMessage = new (
				key: clientKeyString,
				content: content,
				cert: certBytes,
				tree: treeBytes
			);
			cborWriter = new ();
			incomingMessage.ToCbor(cborWriter);
			return cborWriter.Encode();
		}
	}
}
