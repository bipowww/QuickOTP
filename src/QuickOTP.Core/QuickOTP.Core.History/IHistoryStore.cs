using System.Collections.Generic;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.History;

public interface IHistoryStore
{
	IReadOnlyList<HistoryEntry> GetEntries();

	void Add(HistoryEntry entry);

	void Clear();

	void Prune(HistoryRetention retention);

	void MarkFalsePositive(string messageId, string code);
}
