using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace EdjCase.ICP.PocketIC
{
	/// <summary>
	/// A class to help start the pocket-ic server process
	/// </summary>
	public class PocketIcServer : IAsyncDisposable
	{
		private readonly Process _serverProcess;
		private readonly int _port;

		private PocketIcServer(Process serverProcess, int port)
		{
			this._serverProcess = serverProcess;
			this._port = port;
		}

		/// <summary>
		/// Gets the URL of the server
		/// </summary>
		public string GetUrl() => $"http://127.0.0.1:{this._port}";

		/// <summary>
		/// Stops the server process
		/// </summary>
		public async ValueTask StopAsync()
		{
			if (!this._serverProcess.HasExited)
			{
				this._serverProcess.Kill();
				await this._serverProcess.WaitForExitAsync();
			}
		}

		/// <summary>
		/// Disposes of the server process
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			await this.StopAsync();
			this._serverProcess.Dispose();
		}


		/// <summary>
		/// Starts the pocket-ic server process
		/// </summary>
		/// <param name="runtimeLogLevel">Outputs the runtime logs using Debug.WriteLine(...) with the specified log level. Null value disables the logging</param>
		/// <param name="showErrorLogs">Outputs the error logs using Debug.WriteLine(...)</param>
		/// <returns>The instance of the PocketIcServer with the running process</returns>
		public static async Task<PocketIcServer> StartAsync(
			LogLevel? runtimeLogLevel = null,
			bool showErrorLogs = true
		)
		{
			string binPath = GetBinPath();
			EnsureExecutablePermission(binPath);

			string pid = Guid.NewGuid().ToString();
			string picFilePrefix = $"pocket_ic_{pid}";
			string portFilePath = Path.Combine(Path.GetTempPath(), $"{picFilePrefix}.port");

			var startInfo = new ProcessStartInfo
			{
				FileName = binPath,
				Arguments = $"--port-file {portFilePath}",
				RedirectStandardOutput = runtimeLogLevel != null,
				RedirectStandardError = showErrorLogs,
				UseShellExecute = false
			};
			if (runtimeLogLevel != null)
			{
				startInfo.EnvironmentVariables["RUST_LOG"] = runtimeLogLevel.Value.ToString().ToLower();
			}

			Process? serverProcess = Process.Start(startInfo);

			if (serverProcess == null)
			{
				throw new Exception("Failed to start PocketIC server process");
			}
			if (runtimeLogLevel != null)
			{
				serverProcess.OutputDataReceived += (sender, e) =>
				{
					if (e.Data != null)
					{
						Debug.WriteLine(e.Data);
					}
				};
				serverProcess.BeginOutputReadLine();
			}

			if (showErrorLogs)
			{

				serverProcess.ErrorDataReceived += (sender, e) =>
				{
					if (e.Data != null)
					{
						Debug.WriteLine(e.Data);
					}
				};
				serverProcess.BeginErrorReadLine();
			}

			TimeSpan interval = TimeSpan.FromMilliseconds(20);
			TimeSpan timeout = TimeSpan.FromSeconds(30);
			Stopwatch stopwatch = Stopwatch.StartNew();
			int port = -1;
			while (true)
			{
				try
				{
					string portString = await File.ReadAllTextAsync(portFilePath);
					if (int.TryParse(portString.Trim(), out port))
					{
						break;
					}
				}
				catch (Exception)
				{

				}
				if (stopwatch.Elapsed > timeout)
				{
					break;
				}
				await Task.Delay(interval); // wait to try again
			}
			if (port == -1)
			{
				throw new Exception($"Failed to start PocketIC server after {timeout}");
			}

			return new PocketIcServer(serverProcess, port);
		}

		private static string GetBinPath()
		{
			string fileName = "pocket-ic";
			string? ridFolder = null;

			if (RuntimeInformation.OSArchitecture == Architecture.X64)
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					ridFolder = "linux-x64";
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					ridFolder = "osx-x64";
				}
			}
			if (ridFolder == null)
			{
				throw new PlatformNotSupportedException($"Unsupported operating system/architecture: {RuntimeInformation.RuntimeIdentifier}. Supported: linux-x64, osx-64");
			}


			// Check environment variable first
			string? envPath = Environment.GetEnvironmentVariable("POCKET_IC_PATH");
			if (!string.IsNullOrEmpty(envPath))
			{
				if (File.Exists(envPath))
				{
					return envPath;
				}
				else
				{
					Console.WriteLine($"Warning: POCKET_IC_PATH environment variable is set, but file does not exist: {envPath}");
				}
			}

			// List of possible locations to search for the binary
			var searchPaths = new[]
			{
				AppContext.BaseDirectory,
				Path.GetDirectoryName(typeof(PocketIcServer).Assembly.Location),
				Environment.CurrentDirectory,
			};

			foreach (var basePath in searchPaths)
			{
				if (basePath == null) continue;

				string[] possiblePaths = new[]
				{
					Path.Combine(basePath, "runtimes", ridFolder, "native", fileName),
					Path.Combine(basePath, fileName),
				};

				foreach (var path in possiblePaths)
				{
					if (File.Exists(path))
					{
						return path;
					}
				}
			}

			throw new FileNotFoundException($"PocketIC binary not found. Searched in {string.Join(", ", searchPaths)}, and POCKET_IC_PATH environment variable");
		}
		private static void EnsureExecutablePermission(string filePath)
		{
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				try
				{
					var fileInfo = new FileInfo(filePath);
					var unixFileMode = (UnixFileMode)fileInfo.UnixFileMode;
					unixFileMode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
					File.SetUnixFileMode(filePath, unixFileMode);
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException($"Failed to set executable permission on '{filePath}': {ex.Message}", ex);
				}
			}
		}
	}

	/// <summary>
	/// Specifies the level of logging.
	/// </summary>
	public enum LogLevel
	{
		/// <summary>
		/// Error level, used for logging error messages.
		/// </summary>
		Error,

		/// <summary>
		/// Warn level, used for logging warning messages.
		/// </summary>
		Warn,

		/// <summary>
		/// Info level, used for logging informational messages.
		/// </summary>
		Info,

		/// <summary>
		/// Debug level, used for logging debug messages.
		/// </summary>
		Debug,

		/// <summary>
		/// Trace level, used for logging trace messages.
		/// </summary>
		Trace,
	}
}
