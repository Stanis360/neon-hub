namespace NeonMonitor.ViewModels;

/// <summary>
/// Ringpuffer fester Größe für Diagrammverläufe. Fehlende Werte werden als
/// <see cref="double.NaN"/> gespeichert und im Diagramm als Lücke gezeichnet.
/// <para>Wird nur auf dem UI-Thread verändert.</para>
/// </summary>
public sealed class ChartSeries
{
    private double[] _buffer;
    private int _start;

    public ChartSeries(int capacity) => _buffer = new double[Math.Max(2, capacity)];

    /// <summary>Wird nach jeder Änderung ausgelöst (Diagramm zeichnet neu).</summary>
    public event EventHandler? Changed;

    public int Capacity => _buffer.Length;
    public int Count { get; private set; }

    /// <summary>Index 0 = ältester Wert.</summary>
    public double this[int index] => _buffer[(_start + index) % _buffer.Length];

    public void Add(double value)
    {
        if (Count < _buffer.Length)
        {
            _buffer[(_start + Count) % _buffer.Length] = value;
            Count++;
        }
        else
        {
            _buffer[_start] = value;
            _start = (_start + 1) % _buffer.Length;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Kapazität ändern; die neuesten Werte bleiben erhalten.</summary>
    public void Resize(int capacity)
    {
        capacity = Math.Max(2, capacity);
        if (capacity == _buffer.Length) return;
        var keep = Math.Min(Count, capacity);
        var next = new double[capacity];
        for (var i = 0; i < keep; i++) next[i] = this[Count - keep + i];
        _buffer = next;
        _start = 0;
        Count = keep;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Inhalt komplett ersetzen (ein einziges Changed-Ereignis statt vieler).</summary>
    public void ReplaceAll(IReadOnlyList<double> values)
    {
        _start = 0;
        Count = Math.Min(values.Count, _buffer.Length);
        var offset = values.Count - Count;
        for (var i = 0; i < Count; i++) _buffer[i] = values[offset + i];
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _start = 0;
        Count = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
