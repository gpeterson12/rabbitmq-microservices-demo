using NotificationService.Models;

namespace NotificationService.Services;

/// <summary>
/// Bounded ring buffer of notification records, fixed memory footprint.
/// Snapshots walk insertion order backwards, so results are newest-first
/// without sorting and equal timestamps stay deterministically ordered.
/// </summary>
public sealed class NotificationLog : INotificationLog
{
    public const int DefaultCapacity = 1000;

    private readonly object _lock = new();
    private readonly NotificationRecord[] _buffer;
    private readonly int _capacity;
    private int _head;
    private int _count;

    public NotificationLog(int capacity = DefaultCapacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        _buffer = new NotificationRecord[capacity];
    }

    public void Add(NotificationRecord record)
    {
        lock (_lock)
        {
            _buffer[_head] = record;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity)
            {
                _count++;
            }
        }
    }

    public IReadOnlyList<NotificationRecord> LatestFirst()
    {
        lock (_lock)
        {
            var result = new List<NotificationRecord>(_count);
            for (var i = 0; i < _count; i++)
            {
                result.Add(_buffer[(_head - 1 - i + _capacity) % _capacity]);
            }

            return result;
        }
    }
}
