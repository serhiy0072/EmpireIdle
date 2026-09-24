using EmpireIdle.Application.Chat.Commands;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>Раз на добу прибирає історію чату кожного світу, старшу за RetentionDays.</summary>
    public class ChatRetentionJob
    {
        private readonly ServerJobRunner _runner;

        public ChatRetentionJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: 600)]
        public Task RunAsync() => _runner.ForEachServerAsync(
            nameof(ChatRetentionJob),
            (mediator, _) => mediator.Send(new DeleteOldChatMessagesCommand()));
    }
}
