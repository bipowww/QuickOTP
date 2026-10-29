using System;
using QuickOTP.Core.Models;
using QuickOTP.Core.Rules;

namespace QuickOTP.Core.Detection;

public sealed class OtpDetectionEngine
{
	private readonly CodeDetector _detector;

	private readonly ServiceDetector _services;

	private readonly UserRulesEngine _rules;

	private readonly TimeSpan _defaultValidity;

	public OtpDetectionEngine(DetectionOptions? options = null, ServiceDetector? services = null, UserRulesEngine? rules = null, TimeSpan? defaultValidity = null)
	{
		_detector = new CodeDetector(options);
		_services = services ?? new ServiceDetector();
		_rules = rules ?? new UserRulesEngine(UserRules.Empty);
		_defaultValidity = defaultValidity ?? TimeSpan.FromMinutes(10.0);
	}

	public OtpDetectionResult? Detect(MessageCandidate message, TimeSpan? defaultValidity = null)
	{
		if (_rules.ShouldIgnore(message))
		{
			return null;
		}
		CodeDetectionResult codeDetectionResult = _detector.Detect(message.Subject, message.PlainText, message.HtmlText);
		if (codeDetectionResult == null || !codeDetectionResult.IsConfident)
		{
			return null;
		}
		if (_rules.IsFalsePositive(message, codeDetectionResult.Code))
		{
			return null;
		}
		string text = string.Join('\n', message.Subject, message.PlainText, (message.HtmlText == null) ? null : CodeDetector.HtmlToText(message.HtmlText));
		DateTimeOffset receivedAt = message.ReceivedAt ?? DateTimeOffset.Now;
		DateTimeOffset? expiresAt = ExpirationParser.TryParseExpiry(text, receivedAt, defaultValidity ?? _defaultValidity);
		string service = _rules.ResolveServiceOverride(message) ?? _services.Detect(message, codeDetectionResult.Context) ?? codeDetectionResult.Service;
		_rules.IsAutoCopyDisabled(service);
		return new OtpDetectionResult
		{
			Code = codeDetectionResult.Code,
			Confidence = codeDetectionResult.Score,
			Service = service,
			Sender = message.Sender,
			MessageId = message.Id,
			AccountId = message.AccountId,
			Source = message.Source,
			ReceivedAt = receivedAt,
			ExpiresAt = expiresAt,
			Subject = message.Subject
		};
	}

	public CodeDetectionResult? DetectRaw(string? subject, string? body, string? html = null)
	{
		return _detector.Detect(subject, body, html);
	}
}
