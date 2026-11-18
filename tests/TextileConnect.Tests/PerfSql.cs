using System.Collections.Concurrent;

namespace TextileConnect.Tests;

/// <summary>Collects the SQL commands (with their time) that the API sends to MySQL, for the profile of the benchmark. Off unless a test switches it on.</summary>
public static class PerfSql
{
    public static volatile bool Enabled;
    public static readonly ConcurrentQueue<string> Lines = new();
}
