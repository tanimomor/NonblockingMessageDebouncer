public class DebounceService
{
    private readonly ConcurrentDictionary<string, List<string>> _senderMessages = new();
    private readonly ConcurrentDictionary<string, string> _scheduledJobs = new();

    public void ReceiveMessage(string sender, string message)
    {
        var messages = _senderMessages.GetOrAdd(sender, _ => new List<string>());
        lock (messages)
        {
            messages.Add(message);
        }

        // Reschedule job for sender
        if (_scheduledJobs.TryGetValue(sender, out var oldJobId))
        {
            BackgroundJob.Delete(oldJobId);
        }

        var jobId = BackgroundJob.Schedule(() => ProcessMessages(sender), TimeSpan.FromSeconds(30));
        _scheduledJobs[sender] = jobId;
    }

    public void ProcessMessages(string sender)
    {
        if (_senderMessages.TryRemove(sender, out var messages))
        {
            Console.WriteLine($"Processing for {sender}:");
            foreach (var msg in messages)
            {
                Console.WriteLine($" - {msg}");
            }
        }

        _scheduledJobs.TryRemove(sender, out _); // Clean up job ID
    }
}
