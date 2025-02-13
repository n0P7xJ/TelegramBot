using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Collections.Concurrent;

class Program
{
    private static readonly string BotToken = "8188680902:AAGk8224w5r_wMLtsnTiBskREhui8_GfTOw";
    private static readonly TelegramBotClient BotClient = new(BotToken);
    private static readonly ConcurrentDictionary<long, UserState> UserStates = new();

    static async Task Main()
    {
        Console.WriteLine("🚀 Бот запущено!");

        using var cts = new CancellationTokenSource();
        var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

        BotClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);
        await Task.Delay(-1);
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message || message.Text is not { } messageText) return;

        long chatId = message.Chat.Id;
        if (!UserStates.ContainsKey(chatId)) UserStates[chatId] = new UserState();

        var userState = UserStates[chatId];

        if (messageText == "/start")
        {
            await botClient.SendTextMessageAsync(chatId, "👋 Вітаю! Напиши `/register` для реєстрації.");
            return;
        }

        if (messageText == "/register")
        {
            userState.Step = 1;
            await botClient.SendTextMessageAsync(chatId, "📝 Введіть ваше ім'я:");
            return;
        }

        if (userState.Step == 1)
        {
            userState.Name = messageText;
            userState.Step = 2;
            await botClient.SendTextMessageAsync(chatId, "📧 Введіть ваш email:");
            return;
        }

        if (userState.Step == 2)
        {
            userState.Email = messageText;
            userState.Step = 3;
            await botClient.SendTextMessageAsync(chatId, "🔑 Введіть пароль:");
            return;
        }

        if (userState.Step == 3)
        {
            userState.Password = messageText;
            userState.Step = 0;

            bool success = await RegisterUserAsync(userState, chatId);
            if (success)
            {
                await botClient.SendTextMessageAsync(chatId, "✅ Реєстрація успішна!");
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Помилка: email вже зареєстрований.");
            }

            UserStates.TryRemove(chatId, out _);
            return;
        }

        if (messageText == "/profile")
        {
            using var db = new AppDbContext();
            var user = db.Users.FirstOrDefault(u => u.ChatId == chatId);

            if (user == null)
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Ви не зареєстровані.");
                return;
            }

            string profileInfo = $"👤 Ваш профіль:\n\n" +
                                 $"📛 Ім'я: {user.Name}\n" +
                                 $"📧 Email: {user.Email}";

            await botClient.SendTextMessageAsync(chatId, profileInfo);
            return;
        }
    }

    private static async Task<bool> RegisterUserAsync(UserState user, long chatId)
    {
        using var db = new AppDbContext();

        if (db.Users.Any(u => u.Email == user.Email))
        {
            return false;
        }

        db.Users.Add(new User
        {
            Name = user.Name,
            Email = user.Email,
            Password = user.Password, 
            ChatId = chatId 
        });

        await db.SaveChangesAsync();
        return true;
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
}

