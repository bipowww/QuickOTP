using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace QuickOTP.Services;

public sealed class SingleInstancePipe : IDisposable
{
	public const string PipeName = "QuickOTP_ShowWindow_Pipe_v1";

	private readonly Action _onShow;

	private CancellationTokenSource? _cts;

	private Task? _listenTask;

	public SingleInstancePipe(Action onShow)
	{
		_onShow = onShow;
	}

	public void Start()
	{
		_cts = new CancellationTokenSource();
		_listenTask = Task.Run(() => ListenAsync(_cts.Token));
	}

	private async Task ListenAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				await using NamedPipeServerStream server = new NamedPipeServerStream("QuickOTP_ShowWindow_Pipe_v1", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
				await server.WaitForConnectionAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
				using StreamReader reader = new StreamReader(server);
				if (await reader.ReadLineAsync(ct).ConfigureAwait(continueOnCapturedContext: false) == "SHOW")
				{
					_onShow();
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch
			{
				try
				{
					await Task.Delay(500, ct).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch
				{
					break;
				}
			}
		}
	}

	public static void SignalShowWindow()
	{
		try
		{
			using NamedPipeClientStream namedPipeClientStream = new NamedPipeClientStream(".", "QuickOTP_ShowWindow_Pipe_v1", PipeDirection.Out);
			namedPipeClientStream.Connect(2000);
			using StreamWriter streamWriter = new StreamWriter(namedPipeClientStream)
			{
				AutoFlush = true
			};
			streamWriter.WriteLine("SHOW");
		}
		catch
		{
		}
	}

	public void Dispose()
	{
		try
		{
			_cts?.Cancel();
			_cts?.Dispose();
		}
		catch
		{
		}
	}
}
