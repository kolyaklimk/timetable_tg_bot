﻿using System.Runtime.InteropServices;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TimetableTgBot.TgCommands;

public static class GeneralCommands
{
    public static async Task DeleteMessage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.DeleteMessageAsync(message.Chat.Id, message.MessageId, cancellationToken);
        }
        catch
        {
            Console.WriteLine("DeleteMessageAsync error in /start");
        }
    }

    public static async Task CreateMenu(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        Console.WriteLine(2);

        var replyKeyboardMarkup = new  InlineKeyboardMarkup(new[]
        {
            new InlineKeyboardButton[] { Constants.ImageMenu, Constants.TimeTableMenu },
            new InlineKeyboardButton[] { Constants.SupportMenu, Constants.SubscribeMenu }
        }); 
        
        await botClient.SendTextMessageAsync(
            message.Chat.Id,
            "Меню:",
            replyMarkup: replyKeyboardMarkup,
            cancellationToken: cancellationToken);
    }
}
