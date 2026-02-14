using System;

namespace paprUI.Models;

public enum SerialTrafficDirection
{
    Outgoing,
    Incoming,
    System
}

public sealed record SerialTrafficEntry(DateTimeOffset Timestamp, SerialTrafficDirection Direction, string Message)
{
    public string TimestampText => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
}
