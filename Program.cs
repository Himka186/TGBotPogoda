using Telegram.Bot.Exceptions; 
using Telegram.Bot.Polling; 
using Telegram.Bot.Types.ReplyMarkups; 
using Telegram.Bot; 
using Telegram.Bot.Types; 
using Telegram.Bot.Types.Enums; 
using Newtonsoft.Json; 
using System.Net.Http;

namespace WeatherForecastBot 
{
    internal class Program 
    {
        private static HttpClient httpClient = new HttpClient(); // Экземпляр HttpClient для выполнения HTTP-запросов.
        private static string userFeedback = ""; // Поле для хранения отзыва пользователя.
        private static bool waitingForFeedback = false; // Флаг для определения, ожидается ли отзыв от пользователя.

        // Метод для обработки обновлений от бота.
        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is not { } message) // Проверка, что обновление содержит сообщение.
                return;

            var chatId = message.Chat.Id; // Получение ID чата, из которого пришло сообщение.

            // Создание основного меню с кнопками.
            ReplyKeyboardMarkup mainMenu = new(new[]
            {
                new KeyboardButton[] { "Узнать погоду" },
                new KeyboardButton[] { new KeyboardButton("Погода по геопозиции 📍") { RequestLocation = true } }, // Кнопка для отправки геопозиции.
                new KeyboardButton[] { "Обратная связь" }
            })
            {
                ResizeKeyboard = true // Установка автоматической подгонки клавиатуры под экран.
            };

            // Меню погоды с вариантами выбора.
            ReplyKeyboardMarkup weatherMenu = new(new[]
            {
                new KeyboardButton[] { "Сейчас" },
                new KeyboardButton[] { "На неделю" },
                new KeyboardButton[] { "Обратная связь" }
            })
            {
                ResizeKeyboard = true
            };

            // Сохранение текста сообщения или пустой строки, если текст отсутствует.
            string messageText = message.Text ?? string.Empty;

            // Обработка команд и текстов сообщений.
            switch (messageText)
            {
                case "/start": // Стартовая команда.
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "Привет! Хочешь узнать погоду в городе Сургут или по своей геопозиции?\nВыберите действие:",
                        replyMarkup: mainMenu,
                        cancellationToken: cancellationToken);
                    break;

                case "Узнать погоду": // Команда для выбора типа погоды.
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "И так, вам погоду на сейчас или на неделю?",
                        replyMarkup: weatherMenu,
                        cancellationToken: cancellationToken);
                    break;

                case "Погода по геопозиции 📍": // Команда для запроса геопозиции.
                    await botClient.SendMessage(chatId, "Пожалуйста, отправьте вашу геопозицию.", cancellationToken: cancellationToken);
                    break;

                case "Сейчас": // Запрос текущей погоды.
                    string apiUrl = "https://api.weatherapi.com/v1/current.json?q=Surgut&lang=ru&key=cc73fc27b4f64c0db9894854240401";
                    string json = await httpClient.GetStringAsync(apiUrl); // Получение данных погоды из API.

                    CurrentWeather currentWeatherObject = JsonConvert.DeserializeObject<CurrentWeather>(json); // Десериализация данных.

                    await botClient.SendPhoto(
                        chatId: chatId,
                        photo: InputFile.FromUri($"https:{currentWeatherObject.current.Condition.Icon}"), // Отправка иконки погоды.
                        caption: $"🌤️ Погода на {currentWeatherObject.location.Localtime}\n🌡️ Температура воздуха: {currentWeatherObject.current.Temp_C}℃\n" +
                                 $"😌 Ощущается как: {currentWeatherObject.current.Feelslike_C}℃\n💨 Ветер: {currentWeatherObject.current.Wind_Kph} км/ч. Порывами до: {currentWeatherObject.current.Gust_Kph} км/ч\n" +
                                 $"🌧️ Осадки: {currentWeatherObject.current.Condition.Text}",
                        cancellationToken: cancellationToken);
                    break;

                case "Обратная связь": // Команда для отправки обратной связи.
                    waitingForFeedback = true; // Установка флага ожидания отзыва.
                    await botClient.SendMessage(chatId, "Пожалуйста, напишите ваш отзыв или предложение. Нам будет очень приятно.", cancellationToken: cancellationToken);
                    break;

                default: // Обработка остальных сообщений.
                    if (waitingForFeedback)
                    {
                        userFeedback = messageText; // Сохранение отзыва пользователя.
                        Console.WriteLine($"Отзыв от пользователя: {userFeedback}"); // Логирование отзыва.
                        await botClient.SendMessage(chatId, "Ваш отзыв принят. Спасибо! 🙏", cancellationToken: cancellationToken);
                        waitingForFeedback = false; // Сброс флага.
                    }
                    else
                    {
                        await botClient.SendMessage(chatId, "Выберите действие с помощью кнопок или напишите /start для начала.", cancellationToken: cancellationToken);
                    }
                    break;
            }

            // Обработка геопозиции, если она предоставлена.
            if (message.Location != null)
            {
                double latitude = message.Location.Latitude; // Широта.
                double longitude = message.Location.Longitude; // Долгота.

                Console.WriteLine($"Полученные координаты: Широта: {latitude}, Долгота: {longitude}");

                // Запрос геокодера для определения города.
                string geoApiUrl = $"https://api.opencagedata.com/geocode/v1/json?q={latitude}+{longitude}&key=d9b3a9b392db4d61a66cbf8cf8e3d3e3";
                string geoJson = await httpClient.GetStringAsync(geoApiUrl);

                dynamic geoData = JsonConvert.DeserializeObject(geoJson); // Десериализация ответа.
                string cityName = geoData.results[0].components.city; // Получение имени города.

                Console.WriteLine($"Определенный город: {cityName}");

                // Получение погоды для определенного города.
                string locationApiUrl = $"https://api.weatherapi.com/v1/current.json?q={cityName}&lang=ru&key=4f3ee66ce178420b82f123726240911";
                string locationJson = await httpClient.GetStringAsync(locationApiUrl);

                CurrentWeather locationWeather = JsonConvert.DeserializeObject<CurrentWeather>(locationJson); // Десериализация погоды.

                await botClient.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromUri($"https:{locationWeather.current.Condition.Icon}"), // Отправка данных о погоде.
                    caption: $"🌤️ Погода в {locationWeather.location.Name} на {locationWeather.location.Localtime}\n🌡️ Температура воздуха: {locationWeather.current.Temp_C}℃\n" +
                             $"😌 Ощущается как: {locationWeather.current.Feelslike_C}℃\n💨 Ветер: {locationWeather.current.Wind_Kph} км/ч. Порывами до: {locationWeather.current.Gust_Kph} км/ч\n" +
                             $"🌧️ Осадки: {locationWeather.current.Condition.Text}",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(chatId, "Пожалуйста, отправьте вашу геопозицию, используя кнопку в меню.", cancellationToken: cancellationToken);
            }
        }

        // Метод для обработки ошибок.
        public static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage); // Логирование ошибки.
            return Task.CompletedTask;
        }

        // Точка входа в приложение.
        static async Task Main(string[] args)
        {
            TelegramBotClient botClient = new TelegramBotClient("7180531130:AAHVg1JuSrM_7r-nalBdQtBgQPd_7aqmSEo");

            using var cts = new CancellationTokenSource(); // Токен отмены для завершения работы.

            botClient.StartReceiving(
                HandleUpdateAsync, // Метод для обработки обновлений.
                HandlePollingErrorAsync, // Метод для обработки ошибок.
                new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() }, // Настройки получения обновлений.
                cancellationToken: cts.Token
            );

            var me = await botClient.GetMe(); // Получение информации о боте.
            Console.WriteLine($"Start listening for @{me.Username}"); // Вывод имени бота.
            Console.ReadLine(); // Ожидание завершения работы.

            cts.Cancel(); // Отмена получения обновлений.
        }

        // Классы для десериализации данных о погоде.
        public class Location
        {
            public string Name { get; set; } // Название локации.
            public string Localtime { get; set; } // Локальное время.
        }

        public class Condition
        {
            public string Text { get; set; } // Текст описания погоды.
            public string Icon { get; set; } // Иконка погоды.
            public int Code { get; set; } // Код состояния.
        }

        public class Current
        {
            public double Temp_C { get; set; } // Температура в градусах Цельсия.
            public double Feelslike_C { get; set; } // Ощущаемая температура.
            public double Wind_Kph { get; set; } // Скорость ветра в км/ч.
            public double Gust_Kph { get; set; } // Порывы ветра в км/ч.
            public Condition Condition { get; set; } // Состояние погоды.
        }

        public class CurrentWeather
        {
            public Location location { get; set; } // Локация.
            public Current current { get; set; } // Текущая погода.
        }

        public class Day
        {
            public double AvgTemp_C { get; set; } // Средняя температура за день.
            public double MaxWind_Mph { get; set; } // Максимальная скорость ветра.
            public Condition Condition { get; set; } // Состояние погоды.
        }

        public class Forecast
        {
            public List<ForecastDay> Forecastday { get; set; } // Прогноз на несколько дней.
        }

        public class ForecastDay
        {
            public string Date { get; set; } // Дата прогноза.
            public Day Day { get; set; } // Данные за день.
        }

        public class WeatherForecast
        {
            public Location Location { get; set; } // Локация прогноза.
            public Forecast Forecast { get; set; } // Прогноз.
        }
    }
}