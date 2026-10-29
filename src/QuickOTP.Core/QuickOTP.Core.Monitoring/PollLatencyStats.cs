using System;
using System.Linq;

namespace QuickOTP.Core.Monitoring;

public sealed class PollLatencyStats
{
	private sealed class LatencySeries
	{
		private readonly double[] _buf;

		private int _count;

		private int _next;

		public int Count => _count;

		public LatencySeries(int capacity)
		{
			_buf = new double[capacity];
		}

		public void Add(double ms)
		{
			_buf[_next] = ms;
			_next = (_next + 1) % _buf.Length;
			if (_count < _buf.Length)
			{
				_count++;
			}
		}

		public SeriesStats Compute()
		{
			if (_count == 0)
			{
				return SeriesStats.Empty;
			}
			double[] array = new double[_count];
			if (_count < _buf.Length)
			{
				Array.Copy(_buf, array, _count);
			}
			else
			{
				int num = _buf.Length - _next;
				Array.Copy(_buf, _next, array, 0, num);
				Array.Copy(_buf, 0, array, num, _next);
			}
			Array.Sort(array);
			return new SeriesStats(array.Average(), Percentile(array, 0.5), Percentile(array, 0.9), Percentile(array, 0.95), Percentile(array, 0.99), array[0], array[^1], array.Length);
		}

		private static double Percentile(double[] sortedAsc, double p)
		{
			if (sortedAsc.Length == 0)
			{
				return 0.0;
			}
			int num = (int)Math.Clamp(Math.Ceiling(p * (double)sortedAsc.Length) - 1.0, 0.0, sortedAsc.Length - 1);
			return sortedAsc[num];
		}
	}

	private readonly object _lock = new object();

	private readonly LatencySeries _total = new LatencySeries(1000);

	private readonly LatencySeries _historyApi = new LatencySeries(1000);

	private readonly LatencySeries _messageFetch = new LatencySeries(1000);

	private readonly LatencySeries _otpProcessing = new LatencySeries(1000);

	private readonly LatencySeries _clipboard = new LatencySeries(1000);

	public long TotalPolls { get; private set; }

	public long TotalChanges { get; private set; }

	public long TotalApiCalls { get; private set; }

	public long OtpMeasurements { get; private set; }

	public void RecordPoll(TimeSpan gmailDuration, int changeCount, TimeSpan? detectionLatency)
	{
		lock (_lock)
		{
			TotalPolls++;
			TotalChanges += changeCount;
			if (detectionLatency.HasValue)
			{
				_total.Add(detectionLatency.Value.TotalMilliseconds);
			}
		}
	}

	public void Record(OtpLatencyMeasurement m)
	{
		lock (_lock)
		{
			OtpMeasurements++;
			if (m.TotalDetectionLatency.HasValue)
			{
				_total.Add(m.TotalDetectionLatency.Value.TotalMilliseconds);
			}
			if (m.HistoryApiLatency.HasValue)
			{
				_historyApi.Add(m.HistoryApiLatency.Value.TotalMilliseconds);
			}
			if (m.MessageFetchLatency.HasValue)
			{
				_messageFetch.Add(m.MessageFetchLatency.Value.TotalMilliseconds);
			}
			if (m.OtpProcessingLatency.HasValue)
			{
				_otpProcessing.Add(m.OtpProcessingLatency.Value.TotalMilliseconds);
			}
			if (m.ClipboardLatency.HasValue)
			{
				_clipboard.Add(m.ClipboardLatency.Value.TotalMilliseconds);
			}
		}
	}

	public void AddApiCalls(int count)
	{
		lock (_lock)
		{
			TotalApiCalls += count;
		}
	}

	public LatencySnapshot Snapshot()
	{
		lock (_lock)
		{
			SeriesStats seriesStats = _total.Compute();
			return new LatencySnapshot(TotalPolls, TotalChanges, TotalApiCalls, seriesStats.AverageMs, seriesStats.P50Ms, seriesStats.P95Ms);
		}
	}

	public DetailedLatencySnapshot SnapshotDetailed()
	{
		lock (_lock)
		{
			return new DetailedLatencySnapshot(TotalPolls, TotalChanges, TotalApiCalls, OtpMeasurements, _total.Compute(), _historyApi.Compute(), _messageFetch.Compute(), _otpProcessing.Compute(), _clipboard.Compute());
		}
	}
}
