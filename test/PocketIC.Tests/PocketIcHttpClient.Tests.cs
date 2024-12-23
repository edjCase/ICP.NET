using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.PocketIC;
using EdjCase.ICP.PocketIC.Client;
using EdjCase.ICP.PocketIC.Models;
using Xunit;

namespace EdjCase.ICP.PocketIC.Tests;


public class PocketIcHttpClientTests : IClassFixture<PocketIcServerFixture>
{
	private readonly PocketIcServerFixture fixture;
	private string url => this.fixture.Server.GetUrl();

	public PocketIcHttpClientTests(PocketIcServerFixture fixture)
	{
		this.fixture = fixture;
	}

	[Fact]
	public async Task Test()
	{
		PocketIcHttpClient client = new(new HttpClient(), this.url, TimeSpan.FromSeconds(5));
		List<Instance> instances = await client.GetInstancesAsync();
		Assert.NotNull(instances);
		Assert.Empty(instances);

		// Create Instance
		(int instanceId, _) = await client.CreateInstanceAsync();

		instances = await client.GetInstancesAsync();
		Assert.NotNull(instances);
		Assert.Single(instances);
		Assert.Equal(instanceId, instances[0].Id);
		Assert.Equal(InstanceStatus.Available, instances[0].Status);

		// Check topology
		List<SubnetTopology> subnetTopologies = await client.GetTopologyAsync(instanceId);
		Assert.NotNull(subnetTopologies);
		SubnetTopology subnetTopology = Assert.Single(subnetTopologies);
		Assert.Equal(SubnetType.Application, subnetTopology.Type);
		Assert.Equal(13, subnetTopology.NodeIds.Count);



		// Upload and download blob
		byte[] blob = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
		string blobId = await client.UploadBlobAsync(blob);
		Assert.NotNull(blobId);
		Assert.NotEmpty(blobId);

		byte[] downloadedBlob = await client.DownloadBlobAsync(blobId);
		Assert.NotNull(downloadedBlob);
		Assert.Equal(blob, downloadedBlob);

		// Get time
		ICTimestamp timestamp = await client.GetTimeAsync(instanceId);
		Assert.NotNull(timestamp);

		// Set time
		await client.SetTimeAsync(instanceId, timestamp);

		// Tick
		await client.TickAsync(0);

		Principal subnetPublicKey = await client.GetPublicKeyForSubnetAsync(instanceId, subnetTopology.Id);
		Assert.NotNull(subnetPublicKey);

		// TODO
		// byte[] message = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
		// byte[] signature = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
		// Principal publicKey = Principal.Anonymous();
		// bool validSignature = await client.VerifySignatureAsync(message, publicKey, subnetPublicKey, signature);
		// Assert.True(validSignature);


		// Delete the instance
		await client.DeleteInstanceAsync(instanceId);

		instances = await client.GetInstancesAsync();
		Assert.NotNull(instances);
		Assert.Single(instances);
		Assert.Equal(instanceId, instances[0].Id);
		Assert.Equal(InstanceStatus.Deleted, instances[0].Status);
	}
}

