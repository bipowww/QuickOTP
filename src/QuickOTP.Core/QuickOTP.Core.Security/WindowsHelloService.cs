using System.Reflection;

namespace QuickOTP.Core.Security;

public sealed class WindowsHelloService : IWindowsHelloService
{
	public bool IsAvailable
	{
		get
		{
			try
			{
				return OperatingSystem.IsWindowsVersionAtLeast(10);
			}
			catch
			{
				return false;
			}
		}
	}

	public async Task<bool> VerifyAsync(string message)
	{
		if (!OperatingSystem.IsWindowsVersionAtLeast(10))
			return true;

		try
		{
			var type = Type.GetType("Windows.Security.Credentials.UI.UserConsentVerifier, Windows")
				?? Type.GetType("Windows.Security.Credentials.UI.UserConsentVerifier, Microsoft.Windows.SDK.NET");
			if (type is null)
				return true;

			var check = type.GetMethod("CheckAvailabilityAsync", Type.EmptyTypes);
			if (check is null)
				return true;

			dynamic availOp = check.Invoke(null, null)!;
			await ((Task)availOp.AsTask()).ConfigureAwait(false);
			if ((int)availOp.GetResults() != 0)
				return true;

			var request = type.GetMethod("RequestVerificationAsync", new[] { typeof(string) });
			if (request is null)
				return true;

			dynamic verifyOp = request.Invoke(null, new object[] { message })!;
			await ((Task)verifyOp.AsTask()).ConfigureAwait(false);
			return (int)verifyOp.GetResults() == 0;
		}
		catch
		{
			return true;
		}
	}
}
