using System;
using System.Text.RegularExpressions;
using EbayKleinanzeigenCrawler.Interfaces;
using EbayKleinanzeigenCrawler.Models;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

namespace EbayKleinanzeigenCrawler.Manager
{
    public class TelegramManager : StatefulManagerBase<long>, IDisposable
    {
        private readonly string _telegramBotToken;

        private readonly ITelegramBotClient _botClient;

        public TelegramManager(IDataStorage dataStorage, ILogger logger) : base(dataStorage, logger)
        {
            this._telegramBotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");

            if (string.IsNullOrWhiteSpace(_telegramBotToken))
            {
                throw new InvalidOperationException("Telegram Bot token was not specified. Please first create a bot and specify its token.");
            }

            _botClient = new TelegramBotClient(_telegramBotToken);
            _botClient.OnMessage += (_, e) => Bot_OnMessage(e);
            _botClient.StartReceiving();
        }

        private void Bot_OnMessage(MessageEventArgs e)
        {
            if (e.Message.Text is null)
            {
                return;
            }

            long clientId = e.Message.Chat.Id;
            string message = e.Message.Text;
            ProcessCommand(clientId, message);
        }

        protected override void SendMessage(Subscriber<long> subscriber, string message)
        {
            SendMessage(subscriber, message, enablePreview: true, parseMode: ParseMode.Default);
        }

        private void SendMessage(Subscriber<long> subscriber, string message, bool enablePreview, ParseMode parseMode)
        {
            try
            {
                Message result = _botClient.SendTextMessageAsync(
                  chatId: subscriber.Id,
                  text: message,
                  parseMode: parseMode,
                  disableWebPagePreview: !enablePreview
              ).Result;

                if (result?.MessageId is null)
                {
                    Logger.Error($"Error when sending message to Recipient: { subscriber.Id}, Message: \"{message}\"");
                }
                else
                {
                    Logger.Information($"Recipient: {subscriber.Id}, Message: \"{message}\"");  // TODO: Add Admin-Notifications, e.g. for accumulated parsing failures
                }
            }
            catch
            {
                Logger.Error($"Telegram API Error Picture, trying normal message");
                SendMessage(subscriber, message, enablePreview: true, parseMode: ParseMode.Default);
            }
        }

        protected override void SendPictureMessage(Subscriber<long> subscriber, string message, string pictureUrl)
        {
            try
            {
                Message result = _botClient.SendPhotoAsync(
                chatId: subscriber.Id,
                caption: message,
                photo: new InputOnlineFile(pictureUrl)



            ).Result;

                if (result?.MessageId is null)
                {
                    Logger.Error($"Error when sending message to Recipient: { subscriber.Id}, Message: \"{message}\"");
                }
                else
                {
                    Logger.Information($"Recipient: {subscriber.Id}, Message: \"{message}\"");  // TODO: Add Admin-Notifications, e.g. for accumulated parsing failures
                }
            }
            catch
            {
                Logger.Error($"Telegram API Error");
            }

        }

        protected override void DisplaySubscriptionList(Subscriber<long> subscriber)
        {
            var message = "Your subscriptions:";
            foreach (Subscription subscription in subscriber.Subscriptions)
            {
                message += "\n\n" +
                           $"*Title*: {EscapeMarkdownV2Characters(subscription.Title)} \n" +
                           $"*URL*: {EscapeMarkdownV2Characters(subscription.QueryUrl.ToString())} \n" +
                           $"*Included keywords*: {EscapeMarkdownV2Characters(string.Join(", ", subscription.IncludeKeywords))} \n" +
                           $"*Excluded keywords*: {EscapeMarkdownV2Characters(string.Join(", ", subscription.ExcludeKeywords))} \n" +
                           $"*Enabled*: {subscription.Enabled}";
            }

            SendMessage(subscriber, message, enablePreview: false, parseMode: ParseMode.MarkdownV2);
        }

        private string EscapeMarkdownV2Characters(string text)
        {
            const string markdownEscapeCharacters = @"([_\*\[\]\(\)~`>#\+\-=\|\{\}\.!])";
            return Regex.Replace(text, markdownEscapeCharacters, @"\$1");
        }

        public void Dispose()
        {
            _botClient.StopReceiving();
        }
    }
}
