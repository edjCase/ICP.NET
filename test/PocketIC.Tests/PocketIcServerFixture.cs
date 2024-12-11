

using System;

namespace EdjCase.ICP.PocketIC.Tests;

public class PocketIcServerFixture : IDisposable
{
	public PocketIcServer Server { get; private set; }

	public PocketIcServerFixture()
	{
		// Start the server for all tests
		this.Server = PocketIcServer.Start(runtimeLogLevel: LogLevel.Debug).GetAwaiter().GetResult();
	}

	public void Dispose()
	{
		// Stop the server after all tests
		if (this.Server != null)
		{
			this.Server.DisposeAsync().GetAwaiter().GetResult();
		}
	}
}