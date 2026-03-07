namespace HybridOutbox.Configuration;

public sealed class OutboxOptions
{
    public const string SectionName = "HybridOutbox";

    public InboxOptions Inbox { get; set; } = new();
    public JobOptions Job { get; set; } = new();
    public ProcessingOptions Processing { get; set; } = new();
    public InMemoryOptions InMemory { get; set; } = new();

    public sealed class InboxOptions
    {
        public bool Enabled { get; set; } = true;
    }

    public sealed class JobOptions
    {
        public bool Enabled { get; set; } = true;
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);
        public LockOptions Lock { get; set; } = new();
    }

    public sealed class ProcessingOptions
    {
        public TimeSpan Threshold { get; set; } = TimeSpan.FromSeconds(30);
        public int BatchSize { get; set; } = 100;
        public LockOptions Lock { get; set; } = new();
    }

    public sealed class InMemoryOptions
    {
        public bool Enabled { get; set; } = true;
        public int? Capacity { get; set; }
        public int DispatchConcurrency { get; set; } = 1;
    }

    public sealed class LockOptions
    {
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(60);
    }
}