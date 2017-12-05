using Microsoft.Bot.Builder.Dialogs.Internals;
using SkypeFinalBot.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Calling;
using Microsoft.Bot.Builder.Calling.Events;
using Microsoft.Bot.Builder.Calling.ObjectModel.Contracts;
using Microsoft.Bot.Builder.Calling.ObjectModel.Misc;

namespace SkypeFinalBot
{ 
    public class IVRBot : IDisposable, ICallingBot
    {
        private readonly Dictionary<string, CallState> callStateMap = new Dictionary<string, CallState>();

        private readonly MicrosoftCognitiveSpeechService speechService = new MicrosoftCognitiveSpeechService();
        private int i;
        private IncomingCallEvent incomingCallEvent;

        public enum Intent
        {
            WelcomeMessage,
            YesOrNoQuery,
            OneOrTwoQuery,
        }

        public IVRBot(ICallingBotService callingBotService)
        {
            this.CallingBotService = callingBotService;

            this.CallingBotService.OnIncomingCallReceived += this.OnIncomingCallReceived;
            this.CallingBotService.OnRecordCompleted += this.OnRecordCompleted;
            this.CallingBotService.OnHangupCompleted += OnHangupCompleted;
        }

        public ICallingBotService CallingBotService { get; }

        public void Dispose()
        {
            if (this.CallingBotService != null)
            {
                this.CallingBotService.OnIncomingCallReceived -= this.OnIncomingCallReceived;
                this.CallingBotService.OnRecordCompleted -= this.OnRecordCompleted;
                this.CallingBotService.OnHangupCompleted -= OnHangupCompleted;
            }
        }

        private static Task OnHangupCompleted(HangupOutcomeEvent hangupOutcomeEvent)
        {
            hangupOutcomeEvent.ResultingWorkflow = null;
            return Task.FromResult(true);
        }

        private Task OnIncomingCallReceived(IncomingCallEvent incomingCallEvent)
        {

            var record = CreateRecordingAction("Do you want to play a game? Say yes or no.", Intent.WelcomeMessage);

            this.incomingCallEvent = incomingCallEvent;
            this.incomingCallEvent.ResultingWorkflow.Actions = new List<ActionBase> {
                new Answer { OperationId = Guid.NewGuid().ToString()},
                record,
            };

            return Task.FromResult(true);
        }

        private ActionBase CreateRecordingAction(string text, Intent intent)
        {
            return new Record
            {
                OperationId = intent.ToString(),
                PlayPrompt = new PlayPrompt { OperationId = Guid.NewGuid().ToString(), Prompts = new List<Prompt> { new Prompt { Value = text } } },
                RecordingFormat = RecordingFormat.Wav,
                MaxDurationInSeconds = 2,
                PlayBeep = false,
            };
        }

        private ActionBase CreatePlayPromptAction(string text)
        {
            return new PlayPrompt
            {
                OperationId = Guid.NewGuid().ToString(),
                Prompts = new List<Prompt> { new Prompt { Value = text } }
            };
        }

        private async Task OnRecordCompleted(RecordOutcomeEvent recordOutcomeEvent)
        {
            var actions = new List<ActionBase>();

            var spokenText = string.Empty;


            if (recordOutcomeEvent.RecordOutcome.Outcome == Outcome.Success)
            {
                var record = await recordOutcomeEvent.RecordedContent;
                spokenText = await this.speechService.GetTextFromAudioAsync(record);
                if (recordOutcomeEvent.RecordOutcome.Id == Intent.WelcomeMessage.ToString() || recordOutcomeEvent.RecordOutcome.Id == Intent.YesOrNoQuery.ToString())
                {
                    if (spokenText.ToUpperInvariant().Contains("YES"))
                    {
                        var recording = CreateRecordingAction("OK, say 1 or 2.", Intent.OneOrTwoQuery);

                        actions = new List<ActionBase>
                        {
                            recording,
                        };
                    }
                    else if (spokenText.ToUpperInvariant().Contains("NO") && !spokenText.ToUpperInvariant().Contains("YES"))
                    {
                        actions.Add(CreatePlayPromptAction("Screw you!"));
                        actions.Add(new Hangup { OperationId = Guid.NewGuid().ToString() });
                    }
                    else
                    {
                        var recording = CreateRecordingAction("We couldn't recognize your message. Please repeat. Say yes or no.", Intent.YesOrNoQuery);
                        i--;
                        actions.Add(recording);
                    }
                }
                else
                {
                    if (spokenText.Contains("1") || spokenText.Contains("2"))
                    {
                        var random = new Random();
                        var randomNumber = random.Next(0, 100);
                        var list = new List<string> { "Congrats", "won" };
                        if (randomNumber % 2 == 0)
                        {
                            list[0] = "Sorry";
                            list[1] = "lost";

                        }
                        actions.Add(CreatePlayPromptAction($"{list[0]}, you {list[1]}, because you said {spokenText}!"));
                        actions.Add(new Hangup { OperationId = Guid.NewGuid().ToString() });

                    }
                    else
                    {
                        var recording = CreateRecordingAction("We couldn't recognize your message. Please repeat. Say one or two.", Intent.OneOrTwoQuery);

                        actions.Add(recording);
                    }
                }

            }
            else
            {
                actions.Add(CreatePlayPromptAction("Sorry, there was an issue."));
            }

            //actions.Add(new Hangup { OperationId = Guid.NewGuid().ToString() }); // hang up the call


            recordOutcomeEvent.ResultingWorkflow.Actions = actions;
            this.incomingCallEvent.ResultingWorkflow.Actions = actions;
        }
    }
}