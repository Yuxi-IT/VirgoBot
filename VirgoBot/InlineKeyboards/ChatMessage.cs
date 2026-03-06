using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot.Types.ReplyMarkups;

namespace VirgoBot.InlineKeyboards
{
    public class ChatMessage
    {
        public static InlineKeyboardButton MessageRating = new()
        {
            Text = "对回答评分⭐"
        };
    }
}
