﻿using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TimetableTgBot.Constants;

namespace TimetableTgBot.TgCommands;

public static class TimeTableCommands
{
    private static ConcurrentDictionary<string, InlineKeyboardMarkup> SavedCalendars = new();

    public static async Task MenuTimeTable(ITelegramBotClient botClient, Message message)
    {
        var timeTableMenuMarkup = new InlineKeyboardMarkup(new[]
        {
            new [] {
                InlineKeyboardButton.WithCallbackData("Просмотр", "\0"),
                InlineKeyboardButton.WithCallbackData("Редактирование", $"TA_{message.Date.Month:00}_{message.Date.Year}")
            },
            PublicConstants.EmptyInlineKeyboardButton,
            new [] { InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu), },
        });

        // Send message
        await botClient.EditMessageTextAsync(
            message.Chat.Id,
            message.MessageId,
            "Меню расписания:",
            replyMarkup: timeTableMenuMarkup,
            parseMode: ParseMode.MarkdownV2);
    }

    public static async Task ChooseDateTimeTable(CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery.Data, PublicConstants.ChooseMonthTimeTable);

        string month = match.Groups[1].Value;
        string year = match.Groups[2].Value;


        if (SavedCalendars.TryGetValue($"{month}_{year}", out InlineKeyboardMarkup markup))
        { }
        else
        {
            DateOnly currentDate = DateOnly.ParseExact($"01/{month}/{year}", PublicConstants.dateFormat, null);
            int daysInMonth = DateTime.DaysInMonth(currentDate.Year, currentDate.Month);
            int firstDayOfMonth = ((int)currentDate.DayOfWeek + 6) % 7;
            var monthName = currentDate.ToString("MMMM", new CultureInfo("ru-RU"));

            // Month and Name day of week
            var rows = new List<InlineKeyboardButton[]>
            {
                new[] {
                    InlineKeyboardButton.WithCallbackData($"{char.ToUpper(monthName[0])}{monthName[1..]} {currentDate.Year}", "\0")},
                PublicConstants.WeekButtons
            };

            // Calendar
            int currentDay = 1;
            while (currentDay <= daysInMonth)
            {
                var row = new InlineKeyboardButton[7];

                for (int i = 0; i < 7; i++)
                {
                    if (currentDay <= daysInMonth && (i >= firstDayOfMonth || rows.Count > 2))
                    {
                        row[i] = InlineKeyboardButton.WithCallbackData(currentDay.ToString(), $"TG_{currentDay:00}_{month}_{year}");
                        currentDay++;
                    }
                    else
                    {
                        row[i] = "\0";
                    }
                }
                rows.Add(row);
            }

            // previous and next buttons
            var previousMonth = currentDate.AddMonths(-1);
            var nextMonth = currentDate.AddMonths(1);

            rows.Add(new[] {
                currentDate.Year >=  callbackQuery.Message.Date.AddYears(-1).Year ? InlineKeyboardButton.WithCallbackData("<<",callbackData: $"TA_{previousMonth.Month:00}_{previousMonth.Year}") : "\0",
                PublicConstants.EmptyInlineKeyboardButton[0],
                currentDate.Year <= callbackQuery.Message.Date.AddYears(1).Year ? InlineKeyboardButton.WithCallbackData(">>",$"TA_{nextMonth.Month:00}_{nextMonth.Year}") : "\0",
            });
            rows.Add(new[] {
                InlineKeyboardButton.WithCallbackData("Назад", PublicConstants.MenuTimeTable),
                InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu),
            });

            markup = new InlineKeyboardMarkup(rows);
            SavedCalendars.TryAdd($"{month}_{year}", markup);
        }

        // Send message
        await botClient.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            "Выберите дату:",
            replyMarkup: markup);
    }

    public static async Task MenuDayTimeTable(BotDbContext context, CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery.Data, PublicConstants.MenuDayTimeTable);

        string day = match.Groups[1].Value;
        string month = match.Groups[2].Value;
        string year = match.Groups[3].Value;

        DateOnly currentDate = DateOnly.ParseExact($"{day}/{month}/{year}", PublicConstants.dateFormat, null);

        var dayTimetable = await context.WorkTimes
            .Where(arg => arg.UserId == callbackQuery.From.Id && arg.Date == currentDate)
            .OrderBy(arg => arg.Start)
            .ToListAsync();

        var stringBuilder = new StringBuilder();
        foreach (var item in dayTimetable)
        {
            stringBuilder.AppendLine($"{item.Start.Hour:00}:{item.Start.Minute:00} \\- {item.IsBusy} \\- {item.Description}\n");
        }

        // previous and next buttons
        var previousMonth = currentDate.AddDays(-1);
        var nextMonth = currentDate.AddDays(1);

        var rows = new InlineKeyboardButton[][] {
            new[]{
                InlineKeyboardButton.WithCallbackData("Изменить время",$"TJ_{day}_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Выбрать время", $"TB_{day}_{month}_{year}"),
            },
            new[]{
                InlineKeyboardButton.WithCallbackData("Удалить всё", $"TI_{day}_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Выбрать шаблон", $"TL_{day}_{month}_{year}"),
            },
            new[]{
                currentDate.Year >=  callbackQuery.Message.Date.AddYears(-1).Year ? InlineKeyboardButton.WithCallbackData("<<",$"TG_{previousMonth.Day :00}_{previousMonth.Month:00}_{previousMonth.Year}") : "\0",
                PublicConstants.EmptyInlineKeyboardButton[0],
                currentDate.Year <=  callbackQuery.Message.Date.AddYears(1).Year ? InlineKeyboardButton.WithCallbackData(">>",$"TG_{nextMonth.Day :00}_{nextMonth.Month :00}_{nextMonth.Year}") : "\0",
            },
            new[]{
                InlineKeyboardButton.WithCallbackData("Назад", $"TA_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu),
            }
        };

        // Send message
        await botClient.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            $"Вы выбрали: {day}/{month}/{year}\n{stringBuilder}\nВыберите час:",
            replyMarkup: new InlineKeyboardMarkup(rows));
    }

    public static async Task ChooseHourTimeTable(char next, char previous, CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery?.Data, PublicConstants.ChooseHourTimeTable);

        string day = match.Groups[2].Value;
        string month = match.Groups[3].Value;
        string year = match.Groups[4].Value;

        // Hours
        var rows = new List<InlineKeyboardButton[]>();

        for (var i = 0; i < 24;)
        {
            var row = new InlineKeyboardButton[6];
            for (var j = 0; j < 6; j++)
            {
                row[j] = InlineKeyboardButton.WithCallbackData($"{i:00}", $"T{next}_{i:00}_{day}_{month}_{year}");
                i++;
            }
            rows.Add(row);
        }
        rows.Add(PublicConstants.EmptyInlineKeyboardButton);
        rows.Add(new[] {
            InlineKeyboardButton.WithCallbackData("Назад", $"T{previous}_{day}_{month}_{year}"),
            InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu)
        });

        // Send message
        await botClient.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            $"Вы выбрали: __*{day}/{month}/{year}*__\nВыберите час:",
            replyMarkup: new InlineKeyboardMarkup(rows),
            parseMode: ParseMode.MarkdownV2);
    }

    public static async Task ChooseMinuteTimeTable(char next, char previous, CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery?.Data, PublicConstants.ChooseMinuteTimeTable);

        string hour = match.Groups[2].Value;
        string day = match.Groups[3].Value;
        string month = match.Groups[4].Value;
        string year = match.Groups[5].Value;

        // Minute
        var rows = new List<InlineKeyboardButton[]>();

        for (var i = 0; i < 60;)
        {
            var row = new InlineKeyboardButton[6];
            for (var j = 0; j < 6; j++)
            {
                row[j] = InlineKeyboardButton.WithCallbackData($"{i:00}", $"T{next}_{i:00}_{hour}_{day}_{month}_{year}");
                i += 5;
            }
            rows.Add(row);
        }
        rows.Add(PublicConstants.EmptyInlineKeyboardButton);
        rows.Add(new[] {
            InlineKeyboardButton.WithCallbackData("Назад", $"T{previous}_{day}_{month}_{year}"),
            InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu)
        });

        // Send message
        await botClient.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            "Вы выбрали:\n" +
            $"Дата: __*{day}/{month}/{year}*__\n" +
            $"Час: __*{hour}*__",
            replyMarkup: new InlineKeyboardMarkup(rows),
            parseMode: ParseMode.MarkdownV2);
    }

    public static async Task ChooseIsBusyTimeTable(string next, char previous, CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery?.Data, PublicConstants.ChooseIsBusyTimeTable);

        string minute = match.Groups[2].Value;
        string hour = match.Groups[3].Value;
        string day = match.Groups[4].Value;
        string month = match.Groups[5].Value;
        string year = match.Groups[6].Value;

        var rows = new InlineKeyboardButton[][]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Свободно",$"T{next}_0_{minute}_{hour}_{day}_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Запись", $"T{next}_1_{minute}_{hour}_{day}_{month}_{year}"),
            },
            PublicConstants.EmptyInlineKeyboardButton,
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Назад", $"T{previous}_{hour}_{day}_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu),
            }
        };

        // Send message
        await botClient.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            $"Вы свободны?\n" +
            $"Дата: {day}/{month}/{year}\n" +
            $"Время: {hour}:{minute}",
            replyMarkup: new InlineKeyboardMarkup(rows),
            parseMode: ParseMode.MarkdownV2);
    }

    public static async Task AddDescriptionTimeTable(string? description, string data, ITelegramBotClient botClient, Chat chat, int messageId)
    {
        Match match = Regex.Match(data, PublicConstants.AddDescriptionTimeTable);

        string isBusy = match.Groups[2].Value;
        string minute = match.Groups[3].Value;
        string hour = match.Groups[4].Value;
        string day = match.Groups[5].Value;
        string month = match.Groups[6].Value;
        string year = match.Groups[7].Value;

        var rows = new InlineKeyboardButton[][]
        {
            description == null
            ? new[]
            {
                InlineKeyboardButton.WithCallbackData("Сохранить описания", $"TF_{isBusy}_{minute}_{hour}_{day}_{month}_{year}"),
            }
            : new[]
            {
                InlineKeyboardButton.WithCallbackData("Удалить описание", $"TEY_{isBusy}_{minute}_{hour}_{day}_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Сохранить", $"TF_{isBusy}_{minute}_{hour}_{day}_{month}_{year}"),
            },
            PublicConstants.EmptyInlineKeyboardButton,
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Назад", $"TD_{minute}_{hour}_{day}_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu),
            }
        };

        // Send message
        await botClient.EditMessageTextAsync(
            chat.Id,
            messageId,
            $"Напишите, если хотите изменить описание к этой записи :\\)\n" +
            $"Дата: {day}/{month}/{year}\n" +
            $"Время: {hour}:{minute}\n" +
            $"Запись: {"1" == isBusy}" + (description != null ? $"\nОписание: {description}" : ""),
            replyMarkup: new InlineKeyboardMarkup(rows),
            parseMode: ParseMode.MarkdownV2);
    }

    public static async Task SaveTimeTable(BotDbContext context, CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery?.Data, PublicConstants.SaveTimeTable);

        string isBusy = match.Groups[1].Value;
        string minute = match.Groups[2].Value;
        string hour = match.Groups[3].Value;
        string day = match.Groups[4].Value;
        string month = match.Groups[5].Value;
        string year = match.Groups[6].Value;

        var userBuffer = await context.GetUserBufferAsync(callbackQuery.From);

        await context.WorkTimes.AddAsync(new Entities.WorkTime
        {
            Date = DateOnly.ParseExact($"{day}/{month}/{year}", PublicConstants.dateFormat, null),
            Start = TimeOnly.ParseExact($"{hour}:{minute}", PublicConstants.timeFormat, null),
            IsBusy = "1" == isBusy,
            UserId = callbackQuery.From.Id,
            Description = userBuffer.Buffer3
        });

        userBuffer.Buffer3 = null;
        await context.SaveChangesAsync();

        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"Запись на {day}/{month}/{year} в {hour}:{minute} сохранена!", false);

        callbackQuery.Data = $"TG_{day}_{month}_{year}";
        await MenuDayTimeTable(context, callbackQuery, botClient);
    }

    public static async Task DeleteDayTimeTable(BotDbContext context, CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery?.Data, PublicConstants.DeleteDayTimeTable);

        string day = match.Groups[1].Value;
        string month = match.Groups[2].Value;
        string year = match.Groups[3].Value;

        DateOnly currentDate = DateOnly.ParseExact($"{day}/{month}/{year}", PublicConstants.dateFormat, null);

        var dayTimetable = context.WorkTimes.Where(arg => arg.UserId == callbackQuery.From.Id && arg.Date == currentDate);
        context.WorkTimes.RemoveRange(dayTimetable);
        await context.SaveChangesAsync();

        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"Записи на {day}/{month}/{year} удалены!", false);

        callbackQuery.Data = $"TG_{day}_{month}_{year}";
        await MenuDayTimeTable(context, callbackQuery, botClient);
    }

    public static async Task ChooseTimeTimeTable(BotDbContext context, CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery?.Data, PublicConstants.ChooseTimeTimeTable);

        string day = match.Groups[1].Value;
        string month = match.Groups[2].Value;
        string year = match.Groups[3].Value;

        DateOnly currentDate = DateOnly.ParseExact($"{day}/{month}/{year}", PublicConstants.dateFormat, null);

        var times = await context.WorkTimes
            .Where(arg => arg.UserId == callbackQuery.From.Id && arg.Date == currentDate)
            .OrderBy(arg => arg.Start)
            .ToListAsync();

        // Times
        var rows = new List<InlineKeyboardButton[]>();

        for (var i = 0; i < times.Count;)
        {
            var row = new InlineKeyboardButton[(times.Count - i) >= 4 ? 4 : (times.Count - i) % 4];
            for (var j = 0; j < row.Length; j++)
            {
                row[j] = InlineKeyboardButton.WithCallbackData(
                    $"{times[i].Start.Hour}:{times[i].Start.Minute:00}",
                    $"TK0_{times[i].Id}");
                i++;
            }
            rows.Add(row);
        }

        rows.Add(PublicConstants.EmptyInlineKeyboardButton);
        rows.Add(new[] {
                InlineKeyboardButton.WithCallbackData("Назад", $"TG_{day}_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu),
        });

        // Send message
        await botClient.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            "Выберите время:",
            replyMarkup: new InlineKeyboardMarkup(rows));
    }

    public static async Task EditTimeTimeTable(string? newDescription, string data, BotDbContext context, ITelegramBotClient botClient, Chat chat, int messageId, CallbackQuery callbackQuery = null)
    {
        Match match = Regex.Match(data, PublicConstants.EditTimeTimeTable);

        char index = match.Groups[1].Value[0];
        string idWorkTime = match.Groups[2].Value;
        var time = await context.WorkTimes.FirstOrDefaultAsync(arg => arg.Id == long.Parse(idWorkTime));

        switch (index)
        {
            case 'D':
                context.WorkTimes.Remove(time);
                await context.SaveChangesAsync();
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"Запись удалена!", false);

                callbackQuery.Data = $"TG_{time.Date.Day:00}_{time.Date.Month:00}_{time.Date.Year}";
                await MenuDayTimeTable(context, callbackQuery, botClient);
                return;

            case 'R':
                if (time.Description == null)
                {
                    botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                    return;
                }
                time.Description = null;
                goto default;

            case '1':
                time.IsBusy = false;
                goto default;

            case '2':
                time.IsBusy = true;
                goto default;

            default:
                if (newDescription != null)
                    time.Description = newDescription;
                await context.SaveChangesAsync();

                var rows = new InlineKeyboardButton[][]
                {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Удалить",$"TKD_{idWorkTime}"),
                    time.IsBusy
                    ? InlineKeyboardButton.WithCallbackData("Свободно", $"TK1_{idWorkTime}")
                    : InlineKeyboardButton.WithCallbackData("Занято", $"TK2_{idWorkTime}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Удалить описание", $"TKR_{idWorkTime}"),
                },
                PublicConstants.EmptyInlineKeyboardButton,
                new[] {
                    InlineKeyboardButton.WithCallbackData("Назад", $"TJ_{time.Date.Day :00}_{time.Date.Month :00}_{time.Date.Year}"),
                    InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu),
                }
                };


                // Send message
                await botClient.EditMessageTextAsync(
                    chat.Id,
                    messageId,
                    $"Дата: {time.Date.Day}/{time.Date.Month}/{time.Date.Year}\n" +
                    $"Время: {time.Start.Hour}:{time.Start.Minute}\n" +
                    $"Занятость: {time.IsBusy}\n" +
                    $"Описание: {time.Description}\n\n" +
                    "Выбери чёнить.\nНапиши что-нибудь, чтобы изменить описание (не работает):",
                    disableWebPagePreview: true,
                    replyMarkup: new InlineKeyboardMarkup(rows));
                return;
        }
    }

    public static async Task MenuTemplateTimeTable(CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery?.Data, PublicConstants.MenuTemplateTimeTable);

        string day = match.Groups[1].Value;
        string month = match.Groups[2].Value;
        string year = match.Groups[3].Value;

        // Times
        var rows = new InlineKeyboardButton[][]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Создать", $"TM_{day}_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Выбрать", $"TQ_{day}_{month}_{year}"),
            },
            PublicConstants.EmptyInlineKeyboardButton,
            new[] {
                InlineKeyboardButton.WithCallbackData("Назад", $"TG_{day}_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu),
            }
        };

        // Send message
        await botClient.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            "При выборе шаблона, ваши записи на этот жень удалятся!",
            replyMarkup: new InlineKeyboardMarkup(rows));
    }

    public static async Task CreateNewTemplateTimeTable(BotDbContext context, CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery?.Data, PublicConstants.CreateNewTemplateTimeTable);

        string isBusy = match.Groups[1].Value;
        string minute = match.Groups[2].Value;
        string hour = match.Groups[3].Value;
        string day = match.Groups[4].Value;
        string month = match.Groups[5].Value;
        string year = match.Groups[6].Value;

        var template = await context.TimeTableTemplates.AddAsync(new Entities.TimeTableTemplate
        {
            UserId = callbackQuery.From.Id,
        });
        template.Entity.Template.Add(new Entities.WorkTime
        {
            UserId = callbackQuery.From.Id,
            IsBusy = isBusy == "1",
            Start = TimeOnly.ParseExact($"{hour}:{minute}", PublicConstants.timeFormat, null),
        });
        await context.SaveChangesAsync();

        callbackQuery.Data = $"TR0_{day}_{month}_{year}_{template.Entity.Id}";
        await TemplateTimeTable(context, callbackQuery, botClient);
    }

    public static async Task ChooseTemplateTimeTable(BotDbContext context, CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery?.Data, PublicConstants.ChooseTemplateTimeTable);

        string day = match.Groups[1].Value;
        string month = match.Groups[2].Value;
        string year = match.Groups[3].Value;

        var templates = await context.TimeTableTemplates.Where(arg => arg.UserId == callbackQuery.From.Id).ToListAsync();

        // Templates
        var rows = new List<InlineKeyboardButton[]>();

        for (var i = 0; i < templates.Count;)
        {
            var row = new InlineKeyboardButton[(templates.Count - i) >= 6 ? 6 : (templates.Count - i) % 6];
            for (var j = 0; j < row.Length; j++)
            {
                row[j] = InlineKeyboardButton.WithCallbackData(
                    $"{i + 1}",
                    $"TR0_{day}_{month}_{year}_{templates[i].Id}");
                i++;
            }
            rows.Add(row);
        }

        rows.Add(PublicConstants.EmptyInlineKeyboardButton);
        rows.Add(new[] {
                InlineKeyboardButton.WithCallbackData("Назад", $"TG_{day}_{month}_{year}"),
                InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu),
        });

        // Send message
        await botClient.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            "Выберите номер шаблона:",
            replyMarkup: new InlineKeyboardMarkup(rows));
    }

    public static async Task TemplateTimeTable(BotDbContext context, CallbackQuery callbackQuery, ITelegramBotClient botClient)
    {
        Match match = Regex.Match(callbackQuery?.Data, PublicConstants.TemplateTimeTable);

        char property = match.Groups[1].Value[0];
        string day = match.Groups[2].Value;
        string month = match.Groups[3].Value;
        string year = match.Groups[4].Value;
        string idTemplate = match.Groups[5].Value;
        Console.WriteLine(callbackQuery.Data);
        switch (property)
        {
            case '0':
                var rows = new InlineKeyboardButton[][] {
                    new[]{
                        InlineKeyboardButton.WithCallbackData("Удалить",$"TR1_{day}_{month}_{year}_{idTemplate}"),
                        InlineKeyboardButton.WithCallbackData("Изменить", $"TR2_{day}_{month}_{year}_{idTemplate}"),
                    },
                    new[]{
                        InlineKeyboardButton.WithCallbackData("Выбрать", $"TR3_{day}_{month}_{year}"),
                    },
                    PublicConstants.EmptyInlineKeyboardButton,
                    new[]{
                        InlineKeyboardButton.WithCallbackData("Назад", $"TQ_{day}_{month}_{year}"),
                        InlineKeyboardButton.WithCallbackData("Меню", PublicConstants.GoMenu),
                    }
                };

                var template = await context.TimeTableTemplates
                    .Include(arg => arg.Template)
                    .FirstOrDefaultAsync(arg => arg.Id == long.Parse(idTemplate));

                var text = new StringBuilder();
                foreach (var item in template.Template)
                {
                    text.AppendLine($"{item.Start.ToString(PublicConstants.timeFormat)} - {item.IsBusy}");
                }

                // Send message
                await botClient.EditMessageTextAsync(
                    callbackQuery.Message.Chat.Id,
                    callbackQuery.Message.MessageId,
                    $"Вы выбрали:\n {text}\nВыбери что сделать:",
                    replyMarkup: new InlineKeyboardMarkup(rows));
                return;

            case '1':
                var templateRemove = await context.TimeTableTemplates
                    .Include(arg => arg.Template)
                    .FirstOrDefaultAsync(arg => arg.Id == long.Parse(idTemplate));

                context.WorkTimes.RemoveRange(templateRemove.Template);
                context.TimeTableTemplates.Remove(templateRemove);
                await context.SaveChangesAsync();

                callbackQuery.Data = $"TQ_{day}_{month}_{year}";
                await ChooseTemplateTimeTable(context, callbackQuery, botClient);
                return;
        }
    }
}
