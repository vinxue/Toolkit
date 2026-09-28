namespace Memo.Audio;

/// <summary>Thread-safe circular buffer of interleaved float samples; overwrites the oldest data when full.</summary>
internal sealed class FloatRingBuffer
{
    private readonly float[] _buffer;
    private readonly Lock _lock = new();
    private int _readPos;
    private int _count;

    public FloatRingBuffer(int capacity) => _buffer = new float[capacity];

    public int Count
    {
        get { lock (_lock) return _count; }
    }

    public void Write(ReadOnlySpan<float> data)
    {
        lock (_lock)
        {
            int capacity = _buffer.Length;
            if (data.Length > capacity)
                data = data[^capacity..];

            int overflow = _count + data.Length - capacity;
            if (overflow > 0)
            {
                _readPos = (_readPos + overflow) % capacity;
                _count -= overflow;
            }

            int writePos = (_readPos + _count) % capacity;
            int first = Math.Min(data.Length, capacity - writePos);
            data[..first].CopyTo(_buffer.AsSpan(writePos));
            data[first..].CopyTo(_buffer);
            _count += data.Length;
        }
    }

    public int Read(Span<float> destination)
    {
        lock (_lock)
        {
            int n = Math.Min(destination.Length, _count);
            int first = Math.Min(n, _buffer.Length - _readPos);
            _buffer.AsSpan(_readPos, first).CopyTo(destination);
            _buffer.AsSpan(0, n - first).CopyTo(destination[first..]);
            _readPos = (_readPos + n) % _buffer.Length;
            _count -= n;
            return n;
        }
    }

    public void Discard(int samples)
    {
        lock (_lock)
        {
            int n = Math.Min(samples, _count);
            _readPos = (_readPos + n) % _buffer.Length;
            _count -= n;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _readPos = 0;
            _count = 0;
        }
    }
}
