using System;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Телеграм_бот
{
    class Program
    {
        private static string token = "TOKEN";
        private static string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\MILAN\UsersBot.mdf;Integrated Security=True";
        //строка подключение к БД
        private static TelegramBotClient client;
        private static Program progr;
        static void Main(string[] args)
        {
            progr = new Program();
            progr.Start(args); //запуск экземпляра программы
        }

        private void Start(string[] args)
        {
            client = new TelegramBotClient(token);
            client.OnMessage += OnMessageHandler;
            client.OnCallbackQuery += OnCallbackQuery;
            client.StartReceiving();
            Console.WriteLine("Start bot");
            Console.ReadLine();
            client.StopReceiving();
        }
        private static IReplyMarkup GetKeyboard()
        {
            return new ReplyKeyboardMarkup()
            {
                Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton>{ new KeyboardButton {Text = "Ввести имя" }, new KeyboardButton { Text = "Список пользователей" } },
                    new List<KeyboardButton>{ new KeyboardButton { Text = "Температура в мск" } }
                },
            };
        }//button

        private async void OnMessageHandler(object sender, MessageEventArgs e)
        {
            var message = e.Message;
            SqlConnection connection = new SqlConnection(connectionString);
            if (message == null || message.Type != MessageType.Text) return;
            try
            {
                Console.WriteLine($"Time: {message.Date.AddHours(5)} Id: {message.Chat.Id}, Name: {message.From.FirstName}, Message: {message.Text}");
                switch (message.Text.ToLower())
                {
                    case "/start":
                        connection.Open();
                        var command = $"SET IDENTITY_INSERT Users ON " +
                            $"EXEC INSERTUSERS {message.Chat.Id}, '{message.From.FirstName}', '{message.Date.AddHours(5).ToString("yyyy-MM-dd HH:mm:ss")}'";
                        string text;
                        SqlCommand check_user = new SqlCommand(command, connection);
                        if (check_user.ExecuteScalar().ToString() == "2") 
                        { 
                            text = "Приветствую вас!"; 
                        }
                        else 
                        { 
                            text = $"Приветствую вас, {check_user.ExecuteScalar()}!"; 
                        }
                        connection.Close();
                        var first = await client.SendTextMessageAsync(message.Chat.Id, text, replyMarkup: GetKeyboard()); //Сообщение бота
                        update_database(first);
                        break;

                    case "температура в мск":
                        WebRequest request = WebRequest.Create("http://api.openweathermap.org/data/2.5/weather?q=Moscow&appid= TOKEN");
                        //подключение к API 
                        request.Method = "POST";//метод на возвращение ответ на интернет запрос
                        WebResponse response = await request.GetResponseAsync();
                        string answer = string.Empty;
                        //Считываем всё что получили с сайта
                        using (Stream s = response.GetResponseStream())
                        {
                            using (StreamReader reader = new StreamReader(s))
                            {
                                answer = await reader.ReadToEndAsync();
                            }
                        }
                        response.Close();//закрытие чтения
                        Temperatura response_global = JsonConvert.DeserializeObject<Temperatura>(answer);//Конвертация из Json в текст
                        var result = await client.SendTextMessageAsync(message.Chat.Id, $"Текущая температура воздуха в Москве "+ response_global.main.temp+" °C", replyMarkup: GetKeyboard());
                        update_database(result);
                        break;

                    case "список пользователей":
                        await Task.Delay(1000);
                        await client.SendChatActionAsync(message.Chat.Id, ChatAction.Typing); //показывает, что бот пишет
                        SqlCommand AllUserCount = new SqlCommand($"SELECT COUNT(RealName) FROM Users", connection);
                        SqlCommand AllUser = new SqlCommand($"SELECT RealName, FirstMessage FROM Users", connection);
                        connection.Open();
                        string[,] names = new string[(int)AllUserCount.ExecuteScalar(), 2];
                        SqlDataReader reader_usernames = AllUser.ExecuteReader();
                        int i = 0;
                        while (reader_usernames.Read())
                        {
                            names[i, 0] = reader_usernames[0].ToString();
                            names[i, 1] = reader_usernames[1].ToString();
                            i++;
                        }
                        reader_usernames.Close();
                        connection.Close();
                        var inlineKeyboardMarkup = new InlineKeyboardMarkup(GetInlineKeyboard(names));
                        var inline_names = await client.SendTextMessageAsync(message.Chat.Id, "Список всех пользователей: ", replyMarkup: inlineKeyboardMarkup);
                        update_database(inline_names);
                        break;

                    case "ввести имя":
                        connection.Open();
                        SqlCommand command2 = new SqlCommand ($"SELECT ISNULL(NickName,1) FROM Users WHERE Id = {message.Chat.Id}", connection);
                        if (command2.ExecuteScalar().ToString() != "1")
                        {
                            var nick = await client.SendTextMessageAsync(message.Chat.Id, $"Вы уже задали себе имя, {command2.ExecuteScalar()}", replyToMessageId: message.MessageId, replyMarkup: GetKeyboard());
                            update_database(nick);
                        }
                        else
                        {
                            var nameFail = await client.SendTextMessageAsync(message.Chat.Id, "Введите желаемое имя", replyToMessageId: message.MessageId, replyMarkup: new ForceReplyMarkup());
                            update_database(nameFail);
                        }
                        break;

                        default:
                            SqlCommand com = new SqlCommand($"SELECT LastMessage FROM Users WHERE Id = {message.Chat.Id}", connection);
                            connection.Open();
                        if (com.ExecuteScalar().ToString() == "Введите желаемое имя") //Если была введена команда изменения имени
                        {
                            Telegram.Bot.Types.Message exists;
                            SqlCommand update_name = new SqlCommand($"IF EXISTS(SELECT NickName FROM Users WHERE NickName = N'{message.Text}') BEGIN SELECT 1; END\n " +
                                                                $"ELSE BEGIN UPDATE Users SET NickName = N'{message.Text}' WHERE Id = {message.Chat.Id}; SELECT 2; END", connection);
                            if (update_name.ExecuteScalar().ToString() == "2")
                            {
                                exists = await client.SendTextMessageAsync(message.Chat.Id, $"Теперь я буду называть вас так \"{message.Text}\".", replyMarkup: GetKeyboard());
                            }
                            else
                            {
                                exists = await client.SendTextMessageAsync(message.Chat.Id, $"Данное имя уже занято, напишите другое.", replyMarkup: GetKeyboard());
                            }
                            update_database(exists);
                            connection.Close();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }//обработчик нажатия кнопок
        private void update_database(Telegram.Bot.Types.Message message) //Обновление базы данных 
        {
            Console.WriteLine($"BOT ({message.From.Username}, Time: {message.Date.AddHours(5)}) - Chat Id: {message.Chat.Id}, Message: {message.Text}");
            SqlConnection connection = new SqlConnection(connectionString);
            SqlCommand update_data = new SqlCommand($"UPDATE Users SET LastMessage = N'{message.Text}' WHERE Id = N'{message.Chat.Id}'", connection);
            connection.Open();
            update_data.ExecuteNonQuery();
            connection.Close();
        }

        private static InlineKeyboardButton[][] GetInlineKeyboard(string[,] stringArray)
        {
            var keyboardInline = new InlineKeyboardButton[stringArray.Length / 2][]; //ставим количество строк равным количеству элементов в массиве
            var keyboardButtons = new InlineKeyboardButton[stringArray.Length / 2]; //создаём массив кнопок размером с количеством элементов массива
            for (var i = 0; i < stringArray.Length / 2; i++)
            {
                keyboardButtons[i] = new InlineKeyboardButton
                {
                    Text = stringArray[i, 0],
                    CallbackData = stringArray[i, 0] //в качестве ответа передаётся имя пользователя
                };
            }
            for (var j = 1; j <= stringArray.Length / 2; j++)
            {
                keyboardInline[j - 1] = keyboardButtons.Take(1).ToArray();
                keyboardButtons = keyboardButtons.Skip(1).ToArray();
            }
            return keyboardInline;
        }

        private async void OnCallbackQuery(object sc, Telegram.Bot.Args.CallbackQueryEventArgs ev)
        {
            var message = ev.CallbackQuery.Message;
            string datafirstmessage="0";
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            SqlCommand command = new SqlCommand($"SELECT FirstMessage FROM Users WHERE RealName = '{ev.CallbackQuery.Data}'", connection);
            SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                datafirstmessage = reader["FirstMessage"].ToString();
            }
            reader.Close();
            connection.Close();
            var intext = await client.SendTextMessageAsync(message.Chat.Id, datafirstmessage, replyMarkup: GetKeyboard());
        }//обработчик нажатия inline кнопок
    }
}
