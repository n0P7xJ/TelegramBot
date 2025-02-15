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

        if (userState.Step == 1)
        {
            userState.Name = messageText;
            userState.Step = 2;
            await botClient.SendTextMessageAsync(chatId, "📧 Введіть вашу електронну пошту:");
            return;
        }

        if (userState.Step == 2)
        {
            userState.Email = messageText;
            userState.Step = 3;
            await botClient.SendTextMessageAsync(chatId, "🔑 Введіть ваш пароль:");
            return;
        }

        if (userState.Step == 3)
        {
            userState.Password = messageText;
            userState.Step = 0;

            bool success = await RegisterUserAsync(chatId, userState);
            if (success)
            {
                await botClient.SendTextMessageAsync(chatId, "✅ Реєстрація успішна!");
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Помилка: Ця електронна пошта вже зареєстрована.");
            }

            UserStates.TryRemove(chatId, out _);
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
    private static async Task<User?> GetUserProfileAsync(long chatId)
    {
        using var db = new AppDbContext();
        return await Task.Run(() => db.Users.FirstOrDefault(u => u.ChatId == chatId));
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
        else if (callbackQuery.Data == "register")
        {
            if (!UserStates.ContainsKey(chatId)) UserStates[chatId] = new UserState();
            UserStates[chatId].Step = 1;
            await botClient.SendTextMessageAsync(chatId, "📝 Введіть ваше ім'я:");
        }
        else if(callbackQuery.Data == "profile")
        {
            var user = await GetUserProfileAsync(chatId);
            if (user != null)
            {
                await botClient.SendTextMessageAsync(chatId, $"👤 Ваш Профіль:\nІм'я: {user.Name} \nEmail: {user.Email}");
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Ви не зареєстровані.");
            }
        }
    }

    private static async Task<bool> RegisterUserAsync(long chatId, UserState user)
    {
        using var db = new AppDbContext();

        if (db.Users.Any(u => u.Email == user.Email || u.Name == user.Name))
        {
            return false;
        }

        db.Users.Add(new User
        {
            ChatId = chatId,
            Name = user.Name,
            Email = user.Email,
            Password = user.Password
        });

        await db.SaveChangesAsync();
        return true;
    }

    private static async Task<string> TranslateTextWithDeepL(string text, string sourceLanguage, string targetLanguage)
    {
        var translator = new Translator(DeepLApiKey);

        try
        {
            var result = await translator.TranslateTextAsync(text, sourceLanguage, targetLanguage);

            return result.Text;
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
