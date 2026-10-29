using System;

namespace QuickOTP.Core.Gmail;

public sealed class GmailApiException : Exception
{
	public int StatusCode { get; }

	public string? ResponseBody { get; }

	public GmailApiException(int statusCode, string message, string? responseBody = null)
		: base(message)
	{
		StatusCode = statusCode;
		ResponseBody = responseBody;
	}
}
