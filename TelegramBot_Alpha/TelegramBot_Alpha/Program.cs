
using Telegram.Bot;

var bot = new TelegramBotClient("8071381491:AAEf6RXLsFNcNlzLiSXT4XtnE9I-oqD_vVc");
var me = await bot.GetMe();
Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");