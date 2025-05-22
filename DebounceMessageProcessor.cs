using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class DebouncedMessageProcessor
{
    private class SenderState
    {
        public List<string> Messages { get; } = new();
        public CancellationTokenSource CTS { get; set; }
    }

    private readonly ConcurrentDictionary<string, SenderState> _senderStates = new();
    private readonly TimeSpan _debounceTime = TimeSpan.FromSeconds(30);

    public void ReceiveMessage(string jsonMessage)
    {
        var parsed = JsonSerializer.Deserialize<Message>(jsonMessage);
        if (parsed == null || string.IsNullOrWhiteSpace(parsed.Sender))
            return;

        var senderId = parsed.Sender;

        var state = _senderStates.GetOrAdd(senderId, _ => new SenderState());

        lock (state)
        {
            state.Messages.Add(parsed.MessageText);
            state.CTS?.Cancel(); // Cancel previous debounce
            state.CTS = new CancellationTokenSource();
            var token = state.CTS.Token;

            // Launch new debounce task for this sender
            Task.Run(() => DebounceAndProcessAsync(senderId, state, token));
        }
    }

    private async Task DebounceAndProcessAsync(string senderId, SenderState state, CancellationToken token)
    {
        try
        {
            await Task.Delay(_debounceTime, token);
            List<string> messagesToProcess;
            lock (state)
            {
                messagesToProcess = new List<string>(state.Messages);
                state.Messages.Clear();
            }

            Console.WriteLine($"Processing messages for sender '{senderId}':");
            foreach (var msg in messagesToProcess)
            {
                Console.WriteLine($" - {msg}");
            }
        }
        catch (TaskCanceledException)
        {
            // Debounce reset by new message
        }
    }

    // Helper class to parse incoming message
    private class Message
    {
        public string Sender { get; set; }
        public string SenderType { get; set; }
        public string MessageText { get; set; }
        public string Intent { get; set; }
        public string Sentiment { get; set; }
        public object[] Attachments { get; set; }
    }
}
