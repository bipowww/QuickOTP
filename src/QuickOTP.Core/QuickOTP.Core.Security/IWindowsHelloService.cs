using System.Threading.Tasks;

namespace QuickOTP.Core.Security;

public interface IWindowsHelloService
{
	bool IsAvailable { get; }

	Task<bool> VerifyAsync(string message);
}
