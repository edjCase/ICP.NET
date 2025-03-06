using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Agent.Requests;
using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.WebSockets.Models;
using EdjCase.ICP.BLS;
using EdjCase.ICP.Agent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using EdjCase.ICP.Candid.Crypto;

namespace EdjCase.ICP.WebSockets
{
	internal class WebSocketAgent<TMessage> : IWebSocketAgent<TMessage>
		where TMessage : notnull
	{
		internal ulong? ClientNonce { get; private set; }

		private Action<TMessage> onMessage { get; }
		private Action? onOpen { get; }
		private Action? onClose { get; }
		private Action<Exception>? onError { get; }
		private Principal canisterId { get; }
		private Uri gatewayUri { get; }
		private IIdentity identity { get; }
		private SubjectPublicKeyInfo RootPublicKey { get; }
		private CandidConverter? customConverter { get; }
		private IWebSocketClient client { get; }
		private Principal? gatewayPrincipal { get; set; }
		private ulong outgoingSequenceNumber = 1;
		private ulong incomingSequenceNumber = 1;
		private SHA256HashFunction sha256 = SHA256HashFunction.Create();

		public bool IsOpen => this.client.IsOpen;

		public WebSocketAgent(
			Principal canisterId,
			Uri gatewayUri,
			SubjectPublicKeyInfo rootPublicKey,
			IIdentity identity,
			IWebSocketClient client,
			Action<TMessage> onMessage,
			Action? onOpen = null,
			Action<Exception>? onError = null,
			Action? onClose = null,
			CandidConverter? customConverter = null
		)
		{
			this.canisterId = canisterId ?? throw new ArgumentNullException(nameof(canisterId));
			this.gatewayUri = gatewayUri ?? throw new ArgumentNullException(nameof(gatewayUri));
			this.RootPublicKey = rootPublicKey ?? throw new ArgumentNullException(nameof(rootPublicKey));
			this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
			this.client = client ?? throw new ArgumentNullException(nameof(client));
			this.onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
			this.onOpen = onOpen;
			this.onError = onError;
			this.onClose = onClose;
			this.customConverter = customConverter;
		}

		public async Task ConnectAsync(
			CancellationToken? cancellationToken = null
		)
		{
			// Reset session info on new connection
			this.GetOrCreateClientNonce(forceReset: true);
			this.incomingSequenceNumber = 1;
			this.outgoingSequenceNumber = 1;
			this.gatewayPrincipal = null;
			await this.client.ConnectAsync(
				this.gatewayUri,
				cancellationToken ?? CancellationToken.None
			);
		}

		public async Task SendAsync(
			TMessage message,
			CancellationToken? cancellationToken = null
		)
		{
			CandidArg arg = CandidArg.FromCandid(CandidTypedValue.FromObject(
				message,
				this.customConverter
			));

			try
			{
				await this.SendMessageToCanisterAsync(
					arg,
					false,
					cancellationToken ?? CancellationToken.None
				);
			}
			catch (ObjectDisposedException ex)
			{
				throw new InvalidOperationException("Websocket connection is closed. Cannot send message.", ex);
			}
			catch (InvalidOperationException ex)
			{
				throw new InvalidOperationException("Websocket connection has not been opened. Cannot send message.", ex);
			}
		}

		public async Task ReceiveNextAsync(
			CancellationToken? cancellationToken = null
		)
		{
			if (!this.client.IsOpen)
			{
				throw new InvalidOperationException("Websocket is not open");
			}
			cancellationToken ??= CancellationToken.None;
			byte[] data;
			bool isClosed;
			try
			{
				(data, isClosed) = await this.client.ReceiveAsync(cancellationToken.Value);
			}
			catch (Exception ex)
			{
				this.onError?.Invoke(new Exception("Failed to receive message", ex));
				return;
			}
#if DEBUG
			string hex = Candid.Utilities.ByteUtil.ToHexString(data);
#endif
			if (isClosed)
			{
				this.onClose?.Invoke();
				return;
			}

			await this.ProcessMessageAsync(
				data,
				cancellationToken.Value
			);
		}

		private async Task ProcessMessageAsync(
			byte[] data,
			CancellationToken cancellationToken
		)
		{
			CborReader reader = new(data);
			if (this.gatewayPrincipal == null)
			{
				// First message should always be handshake

				HandshakeMessage message;
				try
				{
					reader.ReadTag();
					message = HandshakeMessage.FromCbor(reader);
				}
				catch (Exception ex)
				{
					string hex = BitConverter.ToString(data).Replace("-", "");
					this.onError?.Invoke(new Exception("Unrecognized message format. Unable to parse handshake message. Message hex: " + hex, ex));
					return;
				}
				this.gatewayPrincipal = message.GatewayPrincipal;

				// Send open message to canister
				await this.SendOpenMessageAsync(
					this.gatewayPrincipal,
					cancellationToken
				);
				return;
			}

			// Process normal messages
			ClientIncomingMessage clientMessage;
			try
			{
				clientMessage = ClientIncomingMessage.FromCbor(reader);
			}
			catch (Exception ex)
			{
				this.onError?.Invoke(new Exception("Unable to parse incoming message", ex));
				return;
			}
			if (!this.ValidateMessage(clientMessage, out string? error))
			{
				this.onError?.Invoke(new Exception(error));
				await this.CloseAsync();
				return;
			}
			WebSocketMessage wsMessage;
			try
			{
				CborReader wsReader = new(clientMessage.Content);
				if (wsReader.PeekState() == CborReaderState.Tag)
				{
					// TODO handle better?
					// Sometimes there is a tag, but others not
					wsReader.ReadTag();
				}
				wsMessage = WebSocketMessage.FromCbor(wsReader);
			}
			catch (Exception ex)
			{
				this.onError?.Invoke(new Exception("Unable to parse websocket message", ex));
				return;
			}
			if (wsMessage.SequenceNumber != this.incomingSequenceNumber)
			{
				this.onError?.Invoke(new Exception("Message sequence number is invalid. Closing connection"));
				await this.CloseAsync();
				return;
			}
			this.incomingSequenceNumber++;
			if (wsMessage.ClientKey.Id != this.identity.GetPrincipal())
			{
				this.onError?.Invoke(new Exception("Message client principal is invalid. Closing connection"));
				await this.CloseAsync();
				return;
			}
			if (wsMessage.ClientKey.Nonce != this.ClientNonce)
			{
				this.onError?.Invoke(new Exception("Message client nonce is invalid. Closing connection"));
				await this.CloseAsync();
				return;
			}


			// Convert bytes to candid
			CandidArg arg;
			try
			{
				arg = CandidArg.FromBytes(wsMessage.Content);
			}
			catch (Exception ex)
			{
				this.onError?.Invoke(new Exception("Failed to parse bytes as candid", ex));
				return;
			}
			if (wsMessage.IsServiceMessage)
			{
				await this.ProcessServiceMessageAsync(arg, cancellationToken);
				return;
			}
			else
			{
				// Convert candid to message type
				TMessage message;
				try
				{
					message = arg.ToObjects<TMessage>(this.customConverter);
				}
				catch (Exception ex)
				{
					this.onError?.Invoke(new Exception("Failed to convert candid to message", ex));
					return;
				}
				this.onMessage(message);
				return;
			}
		}

		private bool ValidateMessage(
			ClientIncomingMessage clientMessage,
			[NotNullWhen(false)] out string? error
		)
		{
			CborReader certReader = new(clientMessage.Cert);
			Certificate? cert;
			try
			{
				cert = Certificate.FromCbor(certReader);
			}
			catch
			{
				cert = null;
			}


			if (cert == null || !cert.IsValid(this.RootPublicKey))
			{
				error = "Message certificate is invalid: Invalid signature";
				return false;
			}
			HashTree? witness = cert!.Tree.GetValueOrDefault(
				"canister",
				this.canisterId.Raw,
				"certified_data"
			);
			if (witness == null)
			{
				error = "Message certified data is invalid: No data in certificate tree";
				return false;
			}

			CborReader treeReader = new(clientMessage.Tree);
			HashTree? tree;
			try
			{
				treeReader.ReadTag();
				tree = Certificate.TreeFromCbor(treeReader);
			}
			catch
			{
				tree = null;
			}

			if (tree == null || !witness.AsLeaf().Value.SequenceEqual(tree.BuildRootHash()))
			{
				error = "Message tree is invalid";
				return false;
			}
			HashTree? hashFromTree = tree.GetValueOrDefault("websocket", clientMessage.Key);
			if (hashFromTree == null)
			{
				// Allow fallback to index path.
				hashFromTree = tree.GetValueOrDefault("websocket");
			}
			if (hashFromTree == null)
			{
				// The tree returned in the certification header is wrong. Return false.
				// We don't throw here, just invalidate the request.
				error = $"Message tree in the header. Does not contain path {clientMessage.Key}";
				return false;
			}
			byte[] contentHash = this.sha256.ComputeHash(clientMessage.Content);
			if (hashFromTree.Type != HashTreeType.Leaf
				|| !hashFromTree.AsLeaf().Value.SequenceEqual(contentHash))
			{
				error = "Message content hash does not match the hash in the tree";
				return false;
			}


			error = null;
			return true;
		}

		private async Task ProcessServiceMessageAsync(
			CandidArg arg,
			CancellationToken cancellationToken
		)
		{
			ServiceMessage serviceMessage = arg.ToObjects<ServiceMessage>();
			switch (serviceMessage.Tag)
			{
				case ServiceMessageTag.AckMessage:
					{
						AckMessage message = serviceMessage.AsAckMessage();
						// we throw an error only if the received sequence number is not in the queue
						// and is greater than the last sequence number in the queue
						if (message.LastIncomingSequenceNumber > this.outgoingSequenceNumber)
						{
							this.onError?.Invoke(new Exception("Ack sequence is invalid. Closing connection"));
							await this.client.CloseAsync(cancellationToken, "Ack sequence number is invalid.");
							break;
						}
						await this.SendKeepAliveMessageAsync(cancellationToken);
						break;
					}
				case ServiceMessageTag.OpenMessage:
					{
						this.onOpen?.Invoke();
						return;
					}
				default:
					throw new NotImplementedException();
			}
		}

		private async Task SendKeepAliveMessageAsync(
			CancellationToken cancellationToken
		)
		{
			CandidArg arg = new CandidArg(new List<CandidTypedValue>
			{
				new CandidTypedValue(
					new CandidVariant(
						"KeepAliveMessage",
						new CandidRecord(new Dictionary<CandidTag, CandidValue>
						{
							{ "last_incoming_sequence_num", CandidValue.Nat64(this.incomingSequenceNumber - 1)}
						})
					),
					new CandidVariantType(new Dictionary<CandidTag, CandidType>
					{
						{
							"KeepAliveMessage",
							new CandidRecordType(new Dictionary<CandidTag, CandidType>
							{
								{ "last_incoming_sequence_num", CandidType.Nat64()}
							})
						}
					})
				)
			});
			await this.SendMessageToCanisterAsync(arg, true, cancellationToken);
		}

		private ulong GetOrCreateClientNonce(bool forceReset = false)
		{
			if (forceReset || this.ClientNonce == null)
			{
				// Create random ulong
				Random rand = new();
				byte[] buf = new byte[8];
				rand.NextBytes(buf);
				this.ClientNonce = BitConverter.ToUInt64(buf, 0);
			}
			return this.ClientNonce.Value;
		}

		private async Task SendOpenMessageAsync(
			Principal gatewayPrincipal,
			CancellationToken cancellationToken
		)
		{
			ulong clientNonce = this.GetOrCreateClientNonce();
			CandidArg arg = CandidArg.FromCandid(
				new CandidTypedValue(
					new CandidRecord(
						new Dictionary<CandidTag, CandidValue>
						{
							{ "client_nonce", CandidValue.Nat64(clientNonce) },
							{ "gateway_principal", CandidValue.Principal(gatewayPrincipal) }
						}
					),
					new CandidRecordType(
						new Dictionary<CandidTag, CandidType>
						{
							{ "client_nonce", CandidType.Nat64() },
							{ "gateway_principal", CandidType.Principal() }
						}
					)
				)
			);
			await this.SendEnvelopeToCanisterAsync(
				"ws_open",
				arg,
				cancellationToken
			);
		}

		private async Task SendMessageToCanisterAsync(
			CandidArg arg,
			bool isServiceMessage,
			CancellationToken cancellationToken
		)
		{
			byte[] messageBytes = arg.Encode();

			ICTimestamp.Now().NanoSeconds.TryToUInt64(out ulong now);
			WebSocketMessage message = new(
				sequenceNumber: this.outgoingSequenceNumber,
				content: messageBytes,
				clientKey: new ClientKey
				{
					Id = this.identity.GetPrincipal(),
					Nonce = this.ClientNonce!.Value
				},
				timestamp: now,
				isServiceMessage: isServiceMessage
			);
			var wsMessage = new WebSocketMessageWrapper(message);
			this.outgoingSequenceNumber++;
			CandidArg messageArg = CandidArg.FromCandid(
				CandidTypedValue.FromObject(wsMessage)
			);
			await this.SendEnvelopeToCanisterAsync("ws_message", messageArg, cancellationToken);
		}

		private async Task SendEnvelopeToCanisterAsync(
			string methodName,
			CandidArg arg,
			CancellationToken cancellationToken
		)
		{
			Principal sender = this.identity.GetPrincipal();
			CallRequest request = new(
				this.canisterId,
				methodName,
				arg,
				sender,
				ICTimestamp.Future(TimeSpan.FromSeconds(30))
			);
			SignedContent<CallRequest> signedContent = this.identity
				.SignContent(request);

			CborWriter writer = new CborWriter();
			writer.WriteStartMap(1);
			writer.WriteTextString("envelope");
			signedContent.WriteCbor(writer);
			writer.WriteEndMap();
			byte[] message = writer.Encode();
			await this.client.SendAsync(message, cancellationToken);
		}

		public async Task CloseAsync()
		{
			if (this.IsOpen)
			{
				this.onClose?.Invoke();
				await this.client.CloseAsync(CancellationToken.None);
			}
		}

		public async ValueTask DisposeAsync()
		{
			try
			{
				await this.CloseAsync();
			}
			finally
			{
				this.client.Dispose();
			}
		}
	}
}
