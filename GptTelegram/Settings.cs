using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GptTelegram
{
    internal class Settings
    {
        internal static string telegramApiKey = "";
        internal static string openAiApiKey = "";
        // this chatID is used to make your bot private, it will only accept requests and messages from this chatID
        internal static long chatId = 0;
        // Set this system message to personalize your bot, leave it empty for the standard ChatGPT experience
        internal static string systemMessage = "";
    }
}