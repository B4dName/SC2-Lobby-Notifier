using System;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Windows.Media;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.Notifications;

namespace SC2_Lobby_Notifier
{

    //============================================================================= КЛАСС ЛОББИ =============================================================================

    public class Lobby
    {
        //==================================================================== ПЕРЕМЕННЫЕ ====================================================================

        // ========== Неизменяемые данные о лобби ==========

        // Уникальный ID лобби и ID лобби в системе Battle.net
        public string LobbyId;
        // ID региона лобби
        string RegionId { get; }
        // ID хранилища данных о лобби
        string BnetBucketId { get; }
        // Имя карты лобби
        string MapName { get; }
        // Выбранный режим игры лобби
        string Mode { get; }
        // Ссылка на лого игры лобби
        string ImageUrl { get; }
        // Путь к изображению игры
        string LogoImage { get; }
        // Регион в котором находится лобби
        string Region { get; }
        // Откорректированное по часовому поясу время создания лобби
        DateTime UpTime { get; }
        // Ссылка на API для получения новых данных о лобби
        string LobbyResponse { get; }


        // ========== Постоянно обновляемые данные ==========

        // Значение шкалы прогресса от 0 до 1 (отношение SlotsHumansTaken к SlotsHumansTotal)
        string ProgressBarValue;
        // Сколько мест занято в лобби игроков
        string SlotsHumansTaken;
        // Сколько мест всего в лобби для игроков
        string SlotsHumansTotal;
        // Имя хоста лобби
        string HostName;
        // Заголовок лобби
        string LobbyTitle;


        // ========== Данные при завершении существования лобби ==========

        // Статус существования лобби [открыто|отменено|начато|неизвестно]
        string Status = "open";
        // Цветной статус у текста лобби
        SolidColorBrush StatusColor = Brushes.Yellow;
        // Время закрытия лобби
        DateTime ClosedTime;



        //==================================================================== ИНИЦИАЛИЗАЦИЯ ====================================================================

        // Инициализация класса лобби
        public Lobby(dynamic details)
        {
            LobbyId = details.bnetRecordId;

            RegionId = details.regionId;
            BnetBucketId = details.bnetBucketId;
            MapName = details.map.name;
            Mode = details.mapVariantMode;
            ImageUrl = $"https://static.sc2arcade.com/dimg/{details.map.iconHash}.jpg";
            LogoImage = MainWindow.DataDirectory + details.map.iconHash + ".jpg";
            Region = (details.regionId == 2) ? "EU" : "US";
            UpTime = DateTime.Parse(details.createdAt.ToString()).ToLocalTime();
            LobbyResponse = $"https://api.sc2arcade.com/lobbies/{RegionId}/{BnetBucketId}/{LobbyId}";
        }



        //==================================================================== ТЕЛО ОТОБРАЖАЕМОГО УВЕДОМЛЕНИЯ ЛОББИ ====================================================================

        // Создание тела уведомления и последующее его отображение с цикличным обновлением
        public void ShowLobbyAsNotification()
        {
            // Скачивание иконки карты лобби
            DowloadLogoImage();

            // Первое получение данных о лобби для вывода в область уведомлений
            while (!UpdateLobbyData()) { }

            // Тело уведомления сверху в низ
            new ToastContentBuilder()

                // Небольшая иконка карты в виде круга
                .AddAppLogoOverride(new Uri(LogoImage), ToastGenericAppLogoCrop.Circle)

                // Заголовок уведомления - [КАРТА | ВАРИАНТ ИГРЫ | РЕГИОН]
                .AddText($"{MapName} | {Mode} | {Region}")

                // Адаптивное отображение заголовка лобби
                .AddVisualChild(new AdaptiveText()
                {
                    Text = new BindableString("title")
                })

                // Адаптивное отображение хоста лобби
                .AddVisualChild(new AdaptiveText()
                {
                    Text = new BindableString("host")
                })

                // Время создания лобби
                .AddCustomTimeStamp(UpTime)

                // Адаптивная шкала прогресса
                .AddVisualChild(new AdaptiveProgressBar()
                {
                    Title = Properties.Resources.PlayersInLobby,
                    Value = new BindableProgressBarValue("progress"),
                    ValueStringOverride = new BindableString("slots"),
                    Status = Properties.Resources.Remaining
                })

                // Добавление большого изображения иконки игры к уведомлению
                .AddInlineImage(new Uri(LogoImage))

                // Показ уведомления
                .Show(Notification =>
                {
                    Notification.Data = new NotificationData(new Dictionary<string, string>()
                    {
                        { "title" , LobbyTitle},
                        { "host" , HostName},
                        { "progress", ProgressBarValue },
                        { "slots", SlotsHumansTaken + "/" + SlotsHumansTotal }
                    });

                    // Привязка события клика ЛКМ по уведомлению
                    Notification.Activated += Activated;

                    // Сохранение уникального идентификатора в уведомлении
                    Notification.Tag = LobbyId;

                    // Время по истечению, которого уведомление удалится (2 часа)
                    const int ExpirationTime = 2;

                    // Настройка времени по истечению, которого уведомление удалится
                    Notification.ExpirationTime = DateTime.Now.AddHours(ExpirationTime);
                });

            // Добавление сообщения одного большого сообщения из кучи маленьких о создании лобби к общему списку сообщений
            Addition.LogMessages.AddRange(
                new List<Addition.Log>()
                {
                    new Addition.Log($" https://sc2arcade.com/lobby/{RegionId}/{BnetBucketId}/{LobbyId}\n", Brushes.Blue),
                    new Addition.Log(Properties.Resources.Opened, StatusColor),
                    new Addition.Log($"{MapName} | {Mode} | {Region} | ", Brushes.White),
                    new Addition.Log(UpTime.ToString("[HH:mm:ss] "), Brushes.Yellow)
                }
            );

            // Создание потока для постоянного обновления информации о лобби и конец функции
            UpdateLobbyNotification();

            // ========== Скачивание иконки игры лобби ==========
            void DowloadLogoImage()
            {
                // Флаг проверки необходимости скачивания лого лобби
                bool needDownloadImage = false;

                // Проверка
                try
                {
                    // Проверка корректности скаченного ранее изображения
                    System.Drawing.Image.FromFile(LogoImage);
                }
                // Если произошла ошибка значит необходимо скачивать
                catch
                {
                    needDownloadImage = true;
                }

                // Скачивание лого игры
                while (needDownloadImage)
                {
                    try
                    {
                        // Скачивание в файл LogoImage
                        new WebClient().DownloadFile(new Uri(ImageUrl), LogoImage);

                        // Проверка корректности скаченного изображения
                        System.Drawing.Image.FromFile(LogoImage);

                        // Проверка пройдена и досрочное завершение функции
                        return;
                    }
                    catch { }
                }
            }

            // В случае нажатия по уведомлению оно удаляется и открывается вся доступная информация на сайте
            void Activated(ToastNotification sender, object e) => Addition.OpenLink($"https://sc2arcade.com/lobby/{RegionId}/{BnetBucketId}/{LobbyId}");
        }



        //==================================================================== ПОСТОЯННОЕ ОБНОВЛЕНИЕ ИНФОРМАЦИИ О ЛОББИ В ОБЛАСТИ УВЕДОМЛЕНИЙ ====================================================================

        async void UpdateLobbyNotification()
        {
            // Время между обновлениями данных о лобби в миллисекундах (5 сек)
            const int LobbyUpdateTime = 5000;

            // Пока лобби открыто, оно будет обновляться
            while (Status == "open")
            {
                // Пока данные об лобби не обновятся
                while (!UpdateLobbyData())
                {
                    // Будет происходить задержка между обновлениями в 0.1 сек
                    await Task.Delay(100);
                }

                // Ликвидирование ошибки недоступности платформы уведомлений
                try
                {
                    // Если обновление данных получилось, то обновление информации в области уведомлений
                    ToastNotificationManagerCompat.CreateToastNotifier().Update(
                        new NotificationData(new Dictionary<string, string>()
                        {
                            {"title" , LobbyTitle},
                            {"host" , HostName},
                            {"progress", ProgressBarValue},
                            {"slots", SlotsHumansTaken + "/" + SlotsHumansTotal}
                        }), LobbyId
                    );
                }
                catch { }

                // Задержка перед новым обновлением информации в уведомлении
                await Task.Delay(LobbyUpdateTime);
            }

            // Так как был совершен выход из цикла значит лобби закрылось и надо отправить одно большое сообщение об этом
            Addition.LogMessages.AddRange(
                new List<Addition.Log>()
                {
                    new Addition.Log($" https://sc2arcade.com/lobby/{RegionId}/{BnetBucketId}/{LobbyId}\n", Brushes.Blue),
                    new Addition.Log(Status, StatusColor),
                    new Addition.Log($"{MapName} | {Mode} | {Region} | ", Brushes.White),
                    new Addition.Log(ClosedTime.ToString("[HH:mm:ss] "), Brushes.Yellow)
                }
            );

            // Время задержки перед удалением уведомления лобби (45 сек)
            const int LobbyRemoveTime = 45000;

            // Задержка перед удалением уведомления
            await Task.Delay(LobbyRemoveTime);

            // Удаление лобби из области уведомлений
            ToastNotificationManagerCompat.History.Remove(LobbyId);

            // Удаление из списка обработанных лобби
            MainWindow.AnalyzedLobbies.Remove(this);
        }



        //==================================================================== ОБНОВЛЕНИЕ ИНФОРМАЦИИ О ЛОББИ ====================================================================

        bool UpdateLobbyData()
        {
            // Получение новой информации из запроса данных по данному лобби
            string json = Addition.GetResponseAsJson(LobbyResponse);

            // Если ответ не получен, то обновление не получилось
            if (json == "") return false;

            // Парсинг ответа
            dynamic details = JObject.Parse(json);

            // Получение статуса лобби для проверки открыто ли оно еще
            Status = details.status;

            // Соотношение кол-ва игроков в лобби
            SlotsHumansTaken = details.slotsHumansTaken;
            SlotsHumansTotal = details.slotsHumansTotal;

            // Вычисление соотношения в процентах от 0 до 1
            ProgressBarValue = Convert.ToDouble(double.Parse(SlotsHumansTaken) / double.Parse(SlotsHumansTotal)).ToString().Replace(',', '.');

            // Получение заголовка лобби, указанного хостом
            LobbyTitle = details.lobbyTitle;
            LobbyTitle = Properties.Resources.Title + LobbyTitle;

            // Получение никнейма хоста
            HostName = Properties.Resources.Host + details.hostName;

            // Если у лобби нет заголовка, то вместо него пишется имя хоста
            if (LobbyTitle == Properties.Resources.Title)
            {
                LobbyTitle = HostName;
                HostName = "";
            }

            // Если лобби больше не открыто, то сохраняется время его закрытия
            if (Status != "open")
            {
                ClosedTime = DateTime.Parse(details.closedAt.ToString()).ToLocalTime();
                LobbyTitle = GetLobbyFinalSatus(Status, ClosedTime.ToString("HH:mm:ss"));
                HostName = Properties.Resources.Host + details.hostName;
            }

            // Обновление прошло успешно
            return true;

            // ========== Получение фразы в зависимости от того, как закрылось лобби ==========
            string GetLobbyFinalSatus(string checkStatus, string closedAt)
            {
                // Если лобби было начато
                if (checkStatus == "started")
                {
                    // Изменение статуса лобби на "Начато"
                    Status = Properties.Resources.Started;

                    // Установка зеленого цвета для статуса
                    StatusColor = Brushes.Green;

                    // Сообщение о старте лобби
                    return Properties.Resources.LobbyStartedMessage + closedAt;
                }

                // Если лобби было закрыто
                else if (checkStatus == "abandoned")
                {
                    // Изменение статуса лобби на "Отменено"
                    Status = Properties.Resources.Abandoned;

                    // Установка красного цвета для статуса
                    StatusColor = Brushes.Red;

                    // Сообщение об отмене лобби
                    return Properties.Resources.LobbyAbandonedMessage + closedAt;
                }

                // Если неизвестно что произошло с лобби
                else
                {
                    // Массив из особых фраз
                    string[] unknownEnds =
                    {
                        Properties.Resources.UnknownEnd1,
                        Properties.Resources.UnknownEnd2,
                        Properties.Resources.UnknownEnd3
                    };

                    // Изменение статуса лобби на "Неизвестно"
                    Status = Properties.Resources.Unknown;

                    // Установка серого цвета для статуса
                    StatusColor = Brushes.Gray;

                    // Случайное сообщение об конце существования лобби
                    return unknownEnds[new Random().Next(0, 3)] + closedAt;
                }
            }
        }
    }
}