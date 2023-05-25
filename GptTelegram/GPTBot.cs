#define SEND_WELCOME_MESSAGES

using OpenAI_API;
using OpenAI_API.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Telegram.Bot;
using Telegram.Bot.Types;
using Newtonsoft.Json;

// docs here: https://github.com/OkGoDoIt/OpenAI-API-dotnet
// remember to set api keys in the Settings class!

namespace GptTelegram
{
    public class GPTBot
    {
        private static bool stopThread = false;
        private static bool isclosing = false;
        public static TelegramBotClient bot;
        static int SW_HIDE = 0;
        static int SW_SHOW = 5;
        static int SW_MIN = 2;
        public static Stopwatch? stopwatch;
        static OpenAI_API.Chat.Conversation? chat;
        static OpenAIAPI? api;
        static List<(string, string)> commands = new System.Collections.Generic.List<(string, string)>()
        {
            ("/newtopic", "Starts a new conversation"),
            //("/setmodel", "does nothing, not yet implemented"),
            ("/getusage", "Gets the money spent in the last 30 days"),
        };

        public static void Main()
        {
            Initialize();
#if SEND_WELCOME_MESSAGES
            SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);
#endif
            while (!isclosing) ;
        }

        public static async void Initialize()
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();
            bot = new TelegramBotClient(Settings.telegramApiKey);
            await SetCommands();
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_MIN);
#if SEND_WELCOME_MESSAGES
            await SendMessage("PalGPT online");
#endif
            Console.WriteLine("chatgpt cmd bot is running, press any key to close it.");
            api = new OpenAIAPI(Settings.openAiApiKey);
            chat = api.Chat.CreateConversation();
            chat.Model = Model.ChatGPTTurbo;
            chat.AppendSystemMessage(string.IsNullOrEmpty(Settings.systemMessage) ? "You are ChatGPT, a helpful assistant." : Settings.systemMessage);
            bot.StartReceiving(HandleUpdateAsync, (a, v, c) => { return null; });
        }

        public static async Task SetCommands()
        {
            var commandList = new List<BotCommand>();
            foreach(var commandTuple in commands)
            {
                BotCommand command = new BotCommand();
                command.Command = commandTuple.Item1;
                command.Description = commandTuple.Item2;
                commandList.Add(command);
            }
            await bot.SetMyCommandsAsync(commandList);
        }

        public static void HandleCommand(string command)
        {
            switch (command)
            {
                case "/newtopic":
                    StartNewConversation();
                    break;
                case "/setmodel":
                    //SendMessage("this command will let you change model, not yet implemented");
                    break;
                case "/getusage":
                    GetUsage();
                    break;
            }
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient itelegramBotClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message.Chat.Id != Settings.chatId || stopwatch.Elapsed.TotalSeconds < 2)
            {
                Console.WriteLine("chat id incorrect or message received before 5 seconds");
                return;
            };
            foreach(var command in commands)
            {
                if (command.Item1.Equals(update.Message.Text))
                {
                    HandleCommand(update.Message.Text);
                    return;
                }
            }
            stopThread = false;
            var thinkingMessage = await SendMessage("Thinking");
            Thread thread = new Thread(new ParameterizedThreadStart(ThinkingAnimation));
            thread.Start(thinkingMessage);
            chat.AppendUserInput(update.Message.Text);
            string response = await chat.GetResponseFromChatbotAsync();
            stopThread = true;
            await bot.EditMessageTextAsync(Settings.chatId, thinkingMessage.MessageId, response);
        }

        private static async void ThinkingAnimation(object obj)
        {
            var message = (Message)obj;
            int sleepTimer = 600;
            string[] dotSequences = { ".", "..", "..." };
            int dotIndex = 0;
            while (!stopThread)
            {
                await bot.EditMessageTextAsync(Settings.chatId, message.MessageId, message.Text + dotSequences[dotIndex]);
                await Task.Delay(sleepTimer);
                dotIndex = (dotIndex + 1) % dotSequences.Length;
            }
        }

        public static Task<Message> SendMessage(string message)
        {
            if (bot == null)
            {
                bot = new TelegramBotClient(Settings.telegramApiKey);
            }
            return Task.Run(async () =>
            {
                var returnedMessage = await bot.SendTextMessageAsync(Settings.chatId, message);
                return returnedMessage;
            });
        }

        #region COMMANDS
        private static void StartNewConversation()
        {
            chat = api.Chat.CreateConversation();
            SendMessage("The conversation has been reset");
        }

        private static async void GetUsage()
        {
            var today = DateTime.Today;
            today = today.AddDays(1);
            var lastMonth = today.AddDays(-30);
            string todayStr = today.ToString("yyyy-MM-dd");
            string lastMonthStr = lastMonth.ToString("yyyy-MM-dd");
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Settings.openAiApiKey);
            var response = await httpClient.GetAsync($"https://api.openai.com/dashboard/billing/usage?start_date={lastMonthStr}&end_date={todayStr}");
            var responseContent = await response.Content.ReadAsStringAsync();
            var json = new UsageResponseJSON();
            json = JsonConvert.DeserializeObject<UsageResponseJSON>(responseContent);
            json.total_usage = json.total_usage.Replace('.', ',');
            float euro = float.Parse(json.total_usage)/100f;
            await SendMessage($"In the last 30 days you spent:\n{euro.ToString("F2")}€\nOr more precisely:\n{json.total_usage} cents");
        }

        #endregion

        private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            switch (ctrlType)
            {
                case CtrlTypes.CTRL_CLOSE_EVENT:
                    SendMessage("PalGPT offline, see you next time!");
                    Thread.Sleep(3000);
                    isclosing = true;
                    break;
            }
            return true;
        }

        #region unmanaged

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }
        #endregion

    }
}