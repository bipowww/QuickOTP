using System;
using System.Collections.Generic;

namespace QuickOTP.Core.Gmail;

public sealed class GmailApiBudget
{
	public const int MaxHistoryListPerMinute = 120;

	public const int MaxMessagesGetPerMinute = 60;

	public const int MaxTotalPerMinute = 180;

	public static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(30.0);

	public static readonly TimeSpan Window = TimeSpan.FromMinutes(1.0);

	public static readonly TimeSpan HourWindow = TimeSpan.FromHours(1.0);

	private readonly object _lock = new object();

	private readonly Queue<DateTimeOffset> _history = new Queue<DateTimeOffset>();

	private readonly Queue<DateTimeOffset> _gets = new Queue<DateTimeOffset>();

	private readonly Queue<DateTimeOffset> _total = new Queue<DateTimeOffset>();

	private readonly Queue<DateTimeOffset> _totalHour = new Queue<DateTimeOffset>();

	private DateTimeOffset? _cooldownUntil;

	public bool IsInCooldown(DateTimeOffset now)
	{
		lock (_lock)
		{
			DateTimeOffset? cooldownUntil = _cooldownUntil;
			return cooldownUntil.HasValue && _cooldownUntil > now;
		}
	}

	public TimeSpan? CooldownRemaining(DateTimeOffset now)
	{
		lock (_lock)
		{
			DateTimeOffset? cooldownUntil = _cooldownUntil;
			if (!cooldownUntil.HasValue || _cooldownUntil <= now)
			{
				return null;
			}
			return _cooldownUntil - now;
		}
	}

	public bool TryConsume(GmailApiCallKind kind, DateTimeOffset now, out bool budgetExceeded)
	{
		budgetExceeded = false;
		lock (_lock)
		{
			DateTimeOffset? cooldownUntil = _cooldownUntil;
			if (cooldownUntil.HasValue && _cooldownUntil > now)
			{
				budgetExceeded = true;
				return false;
			}
			PruneLocked(now);
			int num = ((kind == GmailApiCallKind.HistoryList) ? (_history.Count + 1) : _history.Count);
			int num2 = ((kind == GmailApiCallKind.MessagesGet) ? (_gets.Count + 1) : _gets.Count);
			int num3 = _total.Count + 1;
			if (num > 120 || num2 > 60 || num3 > 180)
			{
				_cooldownUntil = now + CooldownDuration;
				budgetExceeded = true;
				return false;
			}
			_total.Enqueue(now);
			_totalHour.Enqueue(now);
			switch (kind)
			{
			case GmailApiCallKind.HistoryList:
				_history.Enqueue(now);
				break;
			case GmailApiCallKind.MessagesGet:
				_gets.Enqueue(now);
				break;
			}
			return true;
		}
	}

	public void EnterCooldown(DateTimeOffset now)
	{
		lock (_lock)
		{
			_cooldownUntil = now + CooldownDuration;
		}
	}

	public BudgetSnapshot Snapshot(DateTimeOffset now)
	{
		lock (_lock)
		{
			PruneLocked(now);
			return new BudgetSnapshot(_history.Count, _gets.Count, _total.Count, _totalHour.Count, IsInCooldown(now), CooldownRemaining(now));
		}
	}

	private void PruneLocked(DateTimeOffset now)
	{
		PruneQueue(_history, now - Window);
		PruneQueue(_gets, now - Window);
		PruneQueue(_total, now - Window);
		PruneQueue(_totalHour, now - HourWindow);
		DateTimeOffset? cooldownUntil = _cooldownUntil;
		if (cooldownUntil.HasValue && _cooldownUntil <= now)
		{
			_cooldownUntil = null;
		}
	}

	private static void PruneQueue(Queue<DateTimeOffset> q, DateTimeOffset cutoff)
	{
		while (q.Count > 0 && q.Peek() < cutoff)
		{
			q.Dequeue();
		}
	}
}
