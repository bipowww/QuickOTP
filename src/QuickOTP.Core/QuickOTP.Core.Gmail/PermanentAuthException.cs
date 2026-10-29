using System;

namespace QuickOTP.Core.Gmail;

public sealed class PermanentAuthException : Exception
{
	public PermanentAuthException(string message)
		: base(message)
	{
	}

	public PermanentAuthException(string message, Exception inner)
		: base(message, inner)
	{
	}
}
