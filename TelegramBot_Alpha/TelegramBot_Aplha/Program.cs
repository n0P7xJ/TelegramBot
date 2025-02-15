using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using DeepL;

public partial class Program
{
    private static readonly ITelegramBotClient BotClient = new TelegramBotClient("8188680902:AAGk8224w5r_wMLtsnTiBskREhui8_GfTOw");
    private static readonly ConcurrentDictionary<long, UserState> UserStates = new();
    private static readonly string LogFilePath = "user_logs.txt";
    private static readonly string DeepLApiKey = "e820cdf2-fbb2-4c3e-8694-0336352f44ee:fx"; // Вставте ваш API ключ DeepL

    static async Task Main()
    {
        Console.WriteLine("🚀 Бот запущено!");
        using var cts = new CancellationTokenSource();
        var receiverOptions = new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery } };
        BotClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);
        await Task.Delay(-1);
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is { } message && message.Text is { } messageText)
        {
            LogUserAction(message.Chat.Id, messageText);
            await HandleMessageAsync(botClient, message.Chat.Id, messageText);
        }
        else if (update.CallbackQuery is { } callbackQuery)
        {
            await HandleCallbackQueryAsync(botClient, callbackQuery);
        }
    }

    private static async Task HandleMessageAsync(ITelegramBotClient botClient, long chatId, string messageText)
    {
        if (!UserStates.ContainsKey(chatId)) UserStates[chatId] = new UserState();
        var userState = UserStates[chatId];

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("👤 Профіль", "profile"), InlineKeyboardButton.WithCallbackData("📝 Реєстрація", "register") },
            new [] { InlineKeyboardButton.WithCallbackData("🌍 Переклад", "translate"), InlineKeyboardButton.WithCallbackData("ℹ Допомога", "help") }
        });

        if (messageText == "/start")
        {
            await botClient.SendTextMessageAsync(chatId, "👋 Ласкаво просимо! Оберіть команду нижче:", replyMarkup: keyboard);
            return;
        }

        if (messageText == "/help")
        {
            await botClient.SendTextMessageAsync(chatId, "📌 Доступні команди:\n\n👤 /profile – Переглянути профіль\n📝 /register – Зареєструватися\n🌍 /translate – Перекласти текст\nℹ /help – Показати всі команди", replyMarkup: keyboard);
            return;
        }

        if (userState.Step == 10)
        {
            userState.SourceLanguage = messageText;
            userState.Step = 11;
            await botClient.SendTextMessageAsync(chatId, "✍ Введіть мову, на яку перекладати:");
            return;
        }

        if (userState.Step == 11)
        {
            userState.TargetLanguage = messageText;
            userState.Step = 12;
            await botClient.SendTextMessageAsync(chatId, "📝 Введіть текст для перекладу:");
            return;
        }

        if (userState.Step == 12)
        {
            string translatedText = await TranslateTextWithDeepL(messageText, userState.SourceLanguage, userState.TargetLanguage);
            await botClient.SendTextMessageAsync(chatId, "🌍 Переклад: " + translatedText);
            userState.Step = 0;
            return;
        }
    }

    private static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        long chatId = callbackQuery.Message.Chat.Id;
        var userState = UserStates.GetOrAdd(chatId, new UserState());

        if (callbackQuery.Data == "translate")
        {
            userState.Step = 10;
            await botClient.SendTextMessageAsync(chatId, "🌍 Введіть мову, з якої перекладати (наприклад, en, uk, ru):");
        }
    }

    private static async Task<string> TranslateTextWithDeepL(string text, string sourceLanguage, string targetLanguage)
    {
        // Ініціалізуємо клієнт DeepL
        var translator = new Translator(DeepLApiKey);

        try
        {
            // Виконуємо переклад
            var result = await translator.TranslateTextAsync(text, sourceLanguage, targetLanguage);

            return result.Text; // Повертаємо перекладений текст
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Помилка: {ex.Message}");
            return "Сталася помилка під час перекладу";
        }
    }

    private static void LogUserAction(long chatId, string messageText)
    {
        string logEntry = $"{DateTime.UtcNow} | Chat ID: {chatId} | Message: {messageText}\n";
        File.AppendAllText(LogFilePath, logEntry);
    }

    private static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"❌ Помилка: {exception.Message}");
    }
}

public class UserState
{
    public int Step { get; set; } = 0;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? SourceLanguage { get; set; }
    public string? TargetLanguage { get; set; }
}
