using System.Collections.Generic;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.History;

public sealed class JsonHistoryStore : IHistoryStore
{
	private readonly SecureHistoryStore _inner;

	public JsonHistoryStore(string? path = null)
	{
		_inner = new SecureHistoryStore(path);
	}

	public IReadOnlyList<HistoryEntry> GetEntries()
	{
		return _inner.GetEntries();
	}

	public void Add(HistoryEntry entry)
	{
		_inner.Add(entry);
	}

	public void Clear()
	{
		_inner.Clear();
	}

	public void Prune(HistoryRetention retention)
	{
		_inner.Prune(retention);
	}

	public void MarkFalsePositive(string messageId, string code)
	{
		_inner.MarkFalsePositive(messageId, code);
	}
}
