using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace QuickOTP.Core.Dedup;

public sealed class CodeDeduplicator
{
	private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.Ordinal);

	private readonly TimeSpan _ttl;

	public CodeDeduplicator(TimeSpan? ttl = null)
	{
		_ttl = ttl ?? TimeSpan.FromHours(2.0);
	}

	public bool ShouldProcess(string messageId, string code)
	{
		return ShouldProcess("gmail", "default", messageId, code);
	}

	public bool ShouldProcess(string source, string accountId, string messageId, string code)
	{
		Prune();
		string key = MakeKey(source, accountId, messageId, code);
		return _seen.TryAdd(key, DateTimeOffset.UtcNow);
	}

	public void MarkProcessed(string source, string accountId, string messageId, string code)
	{
		_seen[MakeKey(source, accountId, messageId, code)] = DateTimeOffset.UtcNow;
	}

	private static string MakeKey(string source, string accountId, string messageId, string code)
	{
		return $"{source}|{accountId}|{messageId}|{code}";
	}

	private void Prune()
	{
		DateTimeOffset dateTimeOffset = DateTimeOffset.UtcNow - _ttl;
		foreach (KeyValuePair<string, DateTimeOffset> item in _seen)
		{
			if (item.Value < dateTimeOffset)
			{
				_seen.TryRemove(item.Key, out var _);
			}
		}
	}
}
