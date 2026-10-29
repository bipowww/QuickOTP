using QuickOTP.Core.Models;

namespace QuickOTP.Core.Sources;

public interface IOtpSource
{
	string SourceId { get; }

	MessageSourceKind Kind { get; }
}
