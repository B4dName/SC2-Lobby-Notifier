using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.Notifications;

namespace SC2_Lobby_Notifier
{

    //============================================================================= КЛАСС ОКНА ПРОГРАММЫ =============================================================================

    public partial class MainWindow : Window
    {

        //==================================================================== ПЕРЕМЕННЫЕ ====================================================================

        // Папка с данными программы в "Документы\SC2 Lobby Notification"
        public static string DataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SC2 Lobby Notification\\";

        // Список всех проанализированных лобби
        public static List<Lobby> AnalyzedLobbies = new List<Lobby>();

        // Файл конфигурации
        IniFile IniFile = new IniFile(DataDirectory + "config.ini");

        // Определение того происходит ли поиск лобби в данный момент или нет
        bool IsScaning = false;

        // Включение/выключение поиска новых лобби
        bool SearchEnabled = true;

        // Иконка программы в трее
        NotifyIcon TrayIcon = new NotifyIcon()
        {
            // Настройка видимости иконки (всегда показывать)
            Visible = true,

            // Надпись при наведении курсора на иконку в трее
            Text = "SC2 Lobby Notifier",

            // Установка изображения иконки в трее
            Icon = new System.Drawing.Icon(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Prog.ico")).Stream)
        };

        // Типы соединения в сайтом
        enum ConnectionType
        {
            // Хорошее соединение
            GoodConnection,

            // Сайт плохо работает
            SiteDoesntWork,

            // Нет соединения
            NoConnection
        }

        // Переменная для отслеживания текущего состояния подключения к сайту
        ConnectionType CurrentConnection = ConnectionType.GoodConnection;


        //==================================================================== ИНИЦИИЛИЗАЦИЯ ПРОГРАММЫ ====================================================================

        public MainWindow()
        {
            // Стандартная процедура инициализации компонентов приложения WPF
            InitializeComponent();

            // Создание папки программы (если она уже существует, то проблем не будет)
            Directory.CreateDirectory(DataDirectory);

            // Создание иконки программы в трее
            CreateTrayIcon();

            // Привязывание горячих клавиш к некоторым функциям
            AddHotkeys();

            // Интервал тиков таймера поиска нового лобби в секундах (5 сек)
            const int TimerSecondsInterval = 5;

            // Таймер для периодического сканирования открытых лобби с установкой интервала
            DispatcherTimer LobbiesScaner = new DispatcherTimer { Interval = new TimeSpan(0, 0, TimerSecondsInterval) };

            // Привязывание функции поиска новых лобби к таймеру срабатывающего между его интервалами
            LobbiesScaner.Tick += ScanNewLobbies;

            // Запуск таймера
            LobbiesScaner.Start();

            // Проверка подключения к сайту
            ScanNewLobbies(null, null);

            // Проверка существования файла конфигурации и его создание в случае отсутствия
            if (!File.Exists(IniFile.Path))
            {
                // Создание файла конфигурации для постоянного хранения данных в папке программы
                File.WriteAllText(IniFile.Path, "");

                // Добавление карт игротеки в качестве примера
                AddMapToIniFile("Aiur Chef");
                AddMapToIniFile("Direct Strike");
                AddMapToIniFile("Left 2 Die");
            }

            // Считывание данных из файла конфигурации в таблицу программы
            foreach (string map in IniFile.GetAllSectionsNames())
            {
                // Добавить выбранную карту в таблицу
                AddMapToTable(map);
            }

            // Полноценное сканирование на наличие открытых лобби из таблицы в конце инициализации программы
            ScanNewLobbies(null, null);


            //========== Добавление горячих клавиш к программе ==========
            void AddHotkeys()
            {
                // Удаление выделенных карт из списка по нажатию Ctrl+D
                RoutedCommand deleteMapsHotkey = new RoutedCommand();
                deleteMapsHotkey.InputGestures.Add(new KeyGesture(Key.D, ModifierKeys.Control));
                CommandBindings.Add(new CommandBinding(deleteMapsHotkey, DeleteMaps_Click));

                // Отключение/включение поиска открытых лобби по нажатию Ctrl+S
                RoutedCommand disableSearchHotkey = new RoutedCommand();
                disableSearchHotkey.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
                CommandBindings.Add(new CommandBinding(disableSearchHotkey, ChangeSearchStatus));
            }

            //========== Создание и настройка иконки программы в трее ==========
            void CreateTrayIcon()
            {
                // Событие при двойном щелчке по иконке в трее
                TrayIcon.DoubleClick += new EventHandler(TrayIcon_DoubleClick);

                // Добавление контекстного меню к иконке в трее
                TrayIcon.ContextMenuStrip = new ContextMenuStrip();

                // Пункт меню "показать главное окно" (по умолчанию скрыто)
                TrayIcon.ContextMenuStrip.Items.Add(Properties.Resources.ShowMainWindow, null, TrayIcon_DoubleClick).Visible = false;

                // Пункт меню "отключить/включить поиск"
                var menuItem = new ToolStripMenuItem(Properties.Resources.DisableSearch, null, ChangeSearchStatus);
                menuItem.ShortcutKeys = Keys.Control | Keys.S;
                TrayIcon.ContextMenuStrip.Items.Add(menuItem);

                // Пункт меню "выйти из приложения"
                TrayIcon.ContextMenuStrip.Items.Add(Properties.Resources.Exit, null, CloseApplication);

                // Показ окна программы из трея при двойном нажатии ЛКМ по иконке в трее
                void TrayIcon_DoubleClick(object sender, EventArgs e)
                {
                    // Показать окно
                    this.Show();

                    // Установка нормального развернутого состояния окна
                    this.WindowState = WindowState.Normal;

                    // Скрытие 1го пункта меню иконки в трее
                    TrayIcon.ContextMenuStrip.Items[0].Visible = false;
                }

                // Закрытие программы из меню иконки в трее
                void CloseApplication(object sender, EventArgs e) => this.Close();
            }
        }



        //==================================================================== ФУНКЦИИ ====================================================================

        // Добавление карты в таблицу окна программы путем чтения информации из файла конфигурации
        void AddMapToTable(string mapName)
        {
            // Получение ссылок на карту из файла конфигурации
            string mapLinkEU = IniFile.ReadFromINI(mapName, "LinkEU");
            string mapLinkUS = IniFile.ReadFromINI(mapName, "LinkUS");

            // Получение настроек поиска указанных пользователем
            bool searchOnEU = IniFile.ReadFromINI(mapName, "SearchEU") == "True";
            bool searchOnUS = IniFile.ReadFromINI(mapName, "SearchUS") == "True";

            // Флаги возможности изменять значение ячейки по серверу
            bool isCellEnabledEU = true;
            bool isCellEnabledUS = true;

            // Если карта не существует на европейском сервере, то флаг соответствующей ячейки менять нельзя
            if (mapLinkEU == "NONE")
            {
                mapLinkEU = "";
                isCellEnabledEU = false;
            }

            // Если карта не существует на американском сервере, то флаг соответствующей ячейки менять нельзя
            if (mapLinkUS == "NONE")
            {
                mapLinkUS = "";
                isCellEnabledUS = false;
            }

            // Добавление новой строки в таблицу
            MapsList.Items.Add(new Map()
            {
                Name = mapName,
                AvaiableOnEU = searchOnEU,
                AvaiableOnUS = searchOnUS,
                MapLinkEU = mapLinkEU,
                MapLinkUS = mapLinkUS,
                IsCellEnabledEU = isCellEnabledEU,
                IsCellEnabledUS = isCellEnabledUS,
                CellVisibleEU = isCellEnabledEU ? Visibility.Visible : Visibility.Hidden,
                CellVisibleUS = isCellEnabledUS ? Visibility.Visible : Visibility.Hidden
            });
        }

        // Получение всей необходимой информации об игре с сервера и запись ее в файл конфигурации, если она существует
        bool AddMapToIniFile(string newMap)
        {
            string nameOfMapForResponses = Uri.EscapeDataString(newMap);

            // Подготовка запросов по серверам для поиска по названию карты
            string[] responses =  {

                // Аркадные карты на европейском сервере
                $"https://api.sc2arcade.com/maps?regionId=2&type=arcade_map&name={nameOfMapForResponses}&limit=5000",

                // Карты схватки на европейском сервере
                $"https://api.sc2arcade.com/maps?regionId=2&type=melee_map&name={nameOfMapForResponses}&limit=5000",

                // Аркадные карты на американском сервере
                $"https://api.sc2arcade.com/maps?regionId=1&type=arcade_map&name={nameOfMapForResponses}&limit=5000",

                // Карты схватки на американском сервере
                $"https://api.sc2arcade.com/maps?regionId=1&type=melee_map&name={nameOfMapForResponses}&limit=5000"
            };

            // Ссылки на карту
            string mapLinkEU = "";
            string mapLinkUS = "";

            // Обработка каждого запроса
            foreach (string response in responses)
            {
                // Запрос на получение результат поиска карты
                string json = Addition.GetResponseAsJson(response);

                // Если ответ на запрос не получен, то досрочный конец в следствии ошибки (ошибка почти невозможна)
                if (json == "") return false;

                // Если карта не найдена, то конец обработки текущего запроса
                if (json == "{\"page\":{\"prev\":null,\"next\":null},\"results\":[]}" || json.IndexOf(newMap) == -1) continue;

                // Удаление лишних символов из ответа
                json = json.Remove(json.Length - 3).Substring(json.IndexOf("results") + 11);

                // Создание массива из полученных карт
                string[] jsonMaps = json.Split(new string[] { "},{" }, StringSplitOptions.None);

                // Поиск карты из списка полученных карт по названию
                foreach (string map in jsonMaps)
                {
                    // Парсинг JSON данных карты
                    dynamic details = JObject.Parse("{" + map + "}");

                    // Если названия карт совпадают значит найден оригинал из списка
                    if (details.name == newMap)
                    {
                        // Сохранение ссылки на карту по европейскому серверу
                        if (details.regionId == "2")
                        {
                            mapLinkEU = $"https://sc2arcade.com/map/{details.regionId}/{details.bnetId}";
                        }
                        // Сохранение ссылки на карту по американскому серверу
                        else
                        {
                            mapLinkUS = $"https://sc2arcade.com/map/{details.regionId}/{details.bnetId}";
                        }

                        // Так как карта найдена, то не имеет смысла искать ее дальше из списка
                        break;
                    }
                }
            }

            // Проверка ссылок, если их нет, значит не удалось найти карту и как следствие она не существует и/или неправильно введена
            if (mapLinkEU == "" && mapLinkUS == "") return false;

            // Добавление информации о карте по европейскому серверу в файл конфигурации
            if (mapLinkEU != "")
            {
                IniFile.WriteToINI(newMap, "SearchEU", "True");
                IniFile.WriteToINI(newMap, "LinkEU", mapLinkEU);
            }
            else
            {
                IniFile.WriteToINI(newMap, "SearchEU", "False");
                IniFile.WriteToINI(newMap, "LinkEU", "NONE");
            }

            // Добавление информации о карте по американскому серверу в файл конфигурации
            if (mapLinkUS != "")
            {
                IniFile.WriteToINI(newMap, "SearchUS", "True");
                IniFile.WriteToINI(newMap, "LinkUS", mapLinkUS);
            }
            else
            {
                IniFile.WriteToINI(newMap, "SearchUS", "False");
                IniFile.WriteToINI(newMap, "LinkUS", "NONE");
            }

            // Карта была найдена
            return true;
        }

        // Изменение статуса поиска открытых лобби
        void ChangeSearchStatus(object sender, EventArgs e)
        {
            // Переключение флага поиска
            SearchEnabled = !SearchEnabled;

            // Если поиск теперь включен
            if (SearchEnabled)
            {
                // То вывод сообщения о включении поиска открытых лобби
                SendMessage(new Addition.Log(Properties.Resources.SearchEnabledMessage, Brushes.Green));

                // Изменение текста во втором пункте меню в трее на "Выключить поиск"
                TrayIcon.ContextMenuStrip.Items[1].Text = Properties.Resources.DisableSearch;
            }
            else
            {
                // Иначе вывод сообщения о выключении поиска открытых лобби
                SendMessage(new Addition.Log(Properties.Resources.SearchDisabledMessage, Brushes.Red));

                // Изменение текста во втором пункте меню в трее на "Включить поиск"
                TrayIcon.ContextMenuStrip.Items[1].Text = Properties.Resources.EnableSearch;
            }
        }

        // Вывод сообщения в логи по умолчанию без времени
        void SendMessage(Addition.Log message, bool addTime = false)
        {
            if (message.Text == null) return;

            // Добавление текста в начало
            TextRange textrange = new TextRange(LogTextBox.Document.ContentStart, LogTextBox.Document.ContentStart) { Text = message.Text };

            // Придание тексту определенного цвета
            textrange.ApplyPropertyValue(TextElement.ForegroundProperty, message.Color);

            // Проверка необходимости добавить время к сообщению
            if (addTime)
            {
                // Этой же функцией отправляется время сообщения в логи
                SendMessage(new Addition.Log(DateTime.Now.ToString("[HH:mm:ss] "), Brushes.Yellow));
            }
        }



        //==================================================================== СОБЫТИЯ ====================================================================

        // Периодическое событие поиска открытых лобби
        async void ScanNewLobbies(object sender, EventArgs e)
        {
            // Ссылки на запросы из которых принимается вся информация об открытых лобби по серверам
            string[] responses = {

                // Европейский сервер
                "https://api.sc2arcade.com/lobbies/active?regionId=2&includeMapInfo=true&includeSlots=false&includeSlotsProfile=false&includeSlotsJoinInfo=false&includeJoinHistory=false&recentlyClosedThreshold=0",

                // Американский сервер
                "https://api.sc2arcade.com/lobbies/active?regionId=1&includeMapInfo=true&includeSlots=false&includeSlotsProfile=false&includeSlotsJoinInfo=false&includeJoinHistory=false&recentlyClosedThreshold=0"
            };

            // Получение ответов на запросы в асинхронном потоке для ликвидированний подтормаживаний
            await Task.Factory.StartNew(() =>
            {
                // Информация с европейского сервера
                responses[0] = Addition.GetResponseAsJson(responses[0]);

                // Информация с американского сервера
                responses[1] = Addition.GetResponseAsJson(responses[1]);
            });

            // Проверка подключения к сайту и небольшая обработка ответов
            responses = CheckConnection(responses[0], responses[1]);


            // Если предыдущее сканирование закончилось, поиск включен и есть подключение к сайту, то значит происходит новый поиск
            if (!IsScaning && SearchEnabled && CurrentConnection != ConnectionType.NoConnection)
            {
                // Поиск начинается (это не позволит начинаться новым сканированиям пока это не закончится)
                IsScaning = true;
                // Поиск проводится асинхронно, так как могут возникнуть подтормаживания главного окна программы
                await Task.Factory.StartNew(() =>
                {
                    // Обработка каждого ответа
                    foreach (string response in responses)
                    {
                        // Копирование выбранного ответа
                        string json = response;

                        // Удаление лишних скобок
                        json = json.Remove(json.Length - 2).Remove(0, 2);

                        // Создание массива из каждого лобби
                        string[] jsonLobbies = json.Split(new string[] { "},{" }, StringSplitOptions.None);

                        // Анализ каждого лобби
                        foreach (string jsonLobby in jsonLobbies)
                        {
                            // Парсинг JSON файла
                            dynamic details = JObject.Parse("{" + jsonLobby + "}");

                            // Если информация о лобби отсутствует, то оно пропускается (ошибка со стороны сайта)
                            if (details.map == null) continue;

                            // Выборка некоторой информации о лобби для сравнения
                            // Уникальный ID лобби
                            string bnetRecordId = details.bnetRecordId;
                            // Имя карты
                            string mapName = details.map.name;
                            // ID региона
                            string regionId = details.regionId;

                            // Проверка необходимости обработать лобби
                            if (LobbyCanBeProcessed(regionId == "2", bnetRecordId, mapName))
                            {
                                // Создание и инициализация уведомления лобби
                                Lobby lobby = new Lobby(details);

                                // Добавление лобби в список для обновления в будущем
                                AnalyzedLobbies.Add(lobby);

                                // Вывод лобби в область уведомлений с последующей самоподдержкой
                                lobby.ShowLobbyAsNotification();
                            }
                        }
                    }
                });

                // Максимальное кол-во текста в текстовой панели
                const int maxLenghtOfTextInPanel = 125000;

                // Позиция с которой начнется удаление лишнего текста в панели вывода
                const int startPositionForTextDeleting = 115000;

                // Очистка поля вывода логов для предотвращения лагов программы
                if (new TextRange(LogTextBox.Document.ContentStart, LogTextBox.Document.ContentEnd).Text.Length > maxLenghtOfTextInPanel)
                {
                    new TextRange(LogTextBox.Document.ContentStart.GetPositionAtOffset(startPositionForTextDeleting), LogTextBox.Document.ContentEnd) { Text = "" };
                }
            }

            // После обработки списка открытых лобби отправляются сохраненные логи программы в текстовое поле вывода (это делается для обработанных и обрабатываемых лобби в других потоках)
            if (Addition.LogMessages.Count != 0)
            {
                // Копирование содержимого списка всех сообщений для вывода
                List<Addition.Log> LogMessagesClone = Addition.LogMessages.ToList();

                // Очистка списка с сообщениями
                Addition.LogMessages.Clear();

                // Обработка каждого сообщения в копированном списке
                foreach (var message in LogMessagesClone)
                {
                    // Отправка сообщения
                    SendMessage(message);
                }
            }

            // Поиск завершен
            IsScaning = false;


            //========== Проверка условий может ли лобби быть обработано по разным критериям ==========
            bool LobbyCanBeProcessed(bool isCreatedOnEU, string bnetRecordId, string mapName)
            {
                // Индекс строки карты в таблице
                int mapRow = -1;

                // Получение индекса строки карты в таблице путем поиска совпадений по имени
                for (int i = 0; i < MapsList.Items.Count; i++)
                {
                    // Проверка совпадений имен
                    if (((dynamic)MapsList.Items[i]).Name == mapName)
                    {
                        // Если имена совпадают значит карта найдена, то сохранение индекса карты в таблице
                        mapRow = i;

                        // И завершение цикла
                        break;
                    }
                }

                // Если карта не найдена в таблице, то ее обрабатывать не нужно
                if (mapRow == -1) return false;

                // Получение статуса поиска лобби карты по серверам
                bool searchOnEU = ((dynamic)MapsList.Items[mapRow]).AvaiableOnEU;
                bool searchOnUS = ((dynamic)MapsList.Items[mapRow]).AvaiableOnUS;

                // Если поиск активирован хотя бы на одном сервере и лобби создано на соответствующем сервере, то продолжение проверки
                if (searchOnEU && isCreatedOnEU || searchOnUS && !isCreatedOnEU)
                {
                    // Создание копии уже существующего списка обрабатываемых лобби
                    List<Lobby> AnalyzedLobbiesCopy = AnalyzedLobbies.ToList();

                    // Проверка того выведено ли уже лобби в область уведомлений путем его поиска по ID в списке обработанных лобби 
                    foreach (Lobby lobby in AnalyzedLobbiesCopy)
                    {
                        // Если каким-то образом было получено пустое лобби, то оно пропускается
                        if (lobby == null) continue;

                        // Сравнение по ID
                        if (lobby.LobbyId == bnetRecordId)
                        {
                            // Так как совпадение найдено, то лобби уже было обработано и как итог ничего делать не надо
                            return false;
                        }
                    }

                    // Если лобби не было найдено, то оно еще не обрабатывалось
                    return true;
                }

                // Иначе поиск заблокирован пользователем и как итог обрабатывать далее лобби не нужно
                return false;
            }


            //========== Проверка соединения с сервером и возврат немного обработанных ответов с сервера ==========
            string[] CheckConnection(string dataFromEU, string dataFromUS)
            {
                // Если не удалось получить абсолютно какой-либо информации, то нет подключения к сайту
                if (dataFromEU == "" && dataFromUS == "")
                {
                    // Проверка того произошла, ли проблема впервые
                    if (CurrentConnection != ConnectionType.NoConnection)
                    {
                        // Вывод сообщения о потери соединения с сайтом
                        SendMessage(new Addition.Log(Properties.Resources.ConnectionIsLostMessage, Brushes.Red), true);

                        // Сохранения статуса подключения
                        CurrentConnection = ConnectionType.NoConnection;

                        // Отключение кнопки добавления карты в список
                        AddMap.IsEnabled = false;
                        AddMap.Foreground = Brushes.Gray;
                    }

                    // Ничего не получено поэтому возвращать нечего
                    return null;
                }
                // Иначе подключение с сайтом есть
                else
                {
                    // Если до этого момента соединения с сайтом не было
                    if (CurrentConnection == ConnectionType.NoConnection)
                    {
                        // То включение кнопки добавления карты в список
                        AddMap.IsEnabled = true;
                        AddMap.Foreground = Brushes.White;
                    }

                    // Если получены пустые списки, то сайт не работает или в SC2 на данный момент не играют
                    if (dataFromEU == "[]" && dataFromUS == "[]")
                    {
                        // Проверка того произошла ли проблема впервые
                        if (CurrentConnection != ConnectionType.SiteDoesntWork)
                        {
                            // Вывод сообщения о плохой работе сайта с серверами
                            SendMessage(new Addition.Log(Properties.Resources.SiteDoesntWorkMessage, Brushes.Red), true);

                            // Изменение статуса подключения на нерабочий сайт
                            CurrentConnection = ConnectionType.SiteDoesntWork;
                        }

                        // Ничего не получено поэтому возвращать нечего
                        return null;
                    }
                    // Если получен пустой список по европейскому серверу, то сайт не работает или в SC2 в Европе на данный момент не играют
                    else if (dataFromEU == "[]" || dataFromEU == "")
                    {
                        // Проверка того произошла ли проблема впервые
                        if (CurrentConnection != ConnectionType.SiteDoesntWork)
                        {
                            // Вывод сообщения о плохой работе сайта с европейским сервером
                            SendMessage(new Addition.Log(Properties.Resources.SiteDoesntWorkOnEUMessage, Brushes.OrangeRed), true);

                            // Изменение статуса подключения на нерабочий сайт
                            CurrentConnection = ConnectionType.SiteDoesntWork;
                        }

                        // Из ответов будет обрабатываться только список открытых лобби на американском сервере
                        return new string[] { dataFromUS };
                    }
                    // Если получен пустой список по американскому серверу, то сайт не работает или в SC2 в Америке на данный момент не играют
                    else if (dataFromUS == "[]" || dataFromUS == "")
                    {
                        // Проверка того произошла ли проблема впервые
                        if (CurrentConnection != ConnectionType.SiteDoesntWork)
                        {
                            // Вывод сообщения о плохой работе сайта с американским сервером
                            SendMessage(new Addition.Log(Properties.Resources.SiteDoesntWorkOnUSMessage, Brushes.OrangeRed), true);

                            // Изменение статуса подключения на нерабочий сайт
                            CurrentConnection = ConnectionType.SiteDoesntWork;
                        }

                        // Из ответов будет обрабатываться только список открытых лобби на европейском сервере
                        return new string[] { dataFromEU };
                    }
                    // Иначе все работает нормально
                    else
                    {
                        // Проверка того было ли соединение ранее нормальным
                        if (CurrentConnection != ConnectionType.GoodConnection)
                        {
                            // Вывод сообщения о восстановлении соединения
                            SendMessage(new Addition.Log(Properties.Resources.ConnectionRestoredMessage, Brushes.Green), true);

                            // Изменение статуса подключения на хорошее
                            CurrentConnection = ConnectionType.GoodConnection;
                        }

                        // Сохранение всех ответов со серверов
                        return new string[] { dataFromEU, dataFromUS };
                    }
                }
            }
        }

        // Добавление новой карты в список по нажатию кнопки "Добавить карту"
        void AddMapClick(object sender, EventArgs e)
        {
            // Получение значения поля ввода строки
            string newMap = NewMapName.Text;

            // Если входная строка пустая
            if (newMap == "")
            {
                // То вывод соответствующего сообщения в логи
                SendMessage(new Addition.Log(Properties.Resources.UserNotEnteredMapNameMessage, Brushes.Red), true);

                // И досрочный конец
                return;
            }

            // Поиск карты среди других в списке
            for (int i = 0; i < MapsList.Items.Count; i++)
            {
                // Поиск по совпадению имен
                if (((dynamic)MapsList.Items[i]).Name == newMap)
                {
                    // Карта найдена значит добавлять ее не надо
                    SendMessage(new Addition.Log(Properties.Resources.MapAlreadyExistMessage.Replace("newMap", newMap), Brushes.Coral), true);

                    // Досрочный конец
                    return;
                }
            }

            // Скачивание информации о карте и проверка результата
            if (!AddMapToIniFile(newMap))
            {
                // Вывод сообщения об отсутствии введенной карты в StarCraft 2 (может также произойти если пользователь отключит интернет и попытается добавить карту)
                SendMessage(new Addition.Log(Properties.Resources.MapNotExistMessage.Replace("newMap", newMap), Brushes.Orange), true);
            }
            else
            {
                // Иначе все прошло удачно и поэтому можно добавить карту в таблицу путем считывания информации о ней из файла конфигурации
                AddMapToTable(newMap);

                // Очистка поля ввода
                NewMapName.Text = "";

                // Вывод сообщения об удачном добавлении карты
                SendMessage(new Addition.Log(Properties.Resources.MapAddedMessage.Replace("newMap", newMap), Brushes.Green), true);
            }
        }

        // Сворачивание программы в трей при нажатии кнопки минимизации на тулбаре
        void MinimizeButton_Pressed(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
            TrayIcon.ContextMenuStrip.Items[0].Visible = true;
            this.Hide();
        }

        // Завершение работы программы через кнопку на тулбаре
        void CloseButtonPressed(object sender, MouseButtonEventArgs e) => this.Close();

        // Обработка события закрытия программы
        void ApplicationCloseEvent(object sender, EventArgs e)
        {
            // Удаление всех отправленных уведомлений
            ToastNotificationManagerCompat.History.Clear();

            // Удаление иконки в трее
            TrayIcon.Dispose();
        }

        // Перетаскивание окна программы ЛКМ
        void DragWindow(object sender, MouseButtonEventArgs e)
        {
            // Проверка нажатия левой кнопки мыши
            if (e.ChangedButton == MouseButton.Left)
            {
                // Перемещение окна
                this.DragMove();
            }
        }

        // Событие изменения значения флажка статуса обработки открытых лобби карты по европейскому серверу
        void CheckBoxCheckedEU(object sender, EventArgs args) => ChangeSearchOnEUStatus(null, null);

        // Событие изменения значения флажка статуса обработки открытых лобби карты по американскому серверу
        void CheckBoxCheckedUS(object sender, EventArgs args) => ChangeSearchOnUSStatus(null, null);



        //==================================================================== МЕНЮ КАРТЫ В СПИСКЕ ====================================================================

        // Открытие страницы в браузере карты на европейском сервере
        void OpenMapInfoEU(object sender, RoutedEventArgs e)
        {
            // Если ячейка выбрана
            if (MapsList.SelectedItem != null)
            {
                // То открыть ссылку на европейскую карту внедренную в ячейку в браузере по умолчанию
                Addition.OpenLink(((dynamic)MapsList.SelectedItem).MapLinkEU);
            }
        }

        // Открытие страницы в браузере карты на американском сервере
        void OpenMapInfoUS(object sender, RoutedEventArgs e)
        {
            // Если ячейка выбрана
            if (MapsList.SelectedItem != null)
            {
                // То открыть ссылку на американскую карту внедренную в ячейку в браузере по умолчанию
                Addition.OpenLink(((dynamic)MapsList.SelectedItem).MapLinkUS);
            }
        }

        // Изменение статуса поиска карты по европейскому серверу
        void ChangeSearchOnEUStatus(object sender, RoutedEventArgs e)
        {
            // Сохранение информации о выбранной карте
            Map MapData = (dynamic)MapsList.SelectedItem;

            // Если карта не была выделена, то конец (это бывает после новой попытки взаимодействия с контекстным меню)
            if (MapData == null) return;

            // Перебор всех карт в таблице
            for (int i = 0; i < MapsList.Items.Count; i++)
            {
                // Поиск карты по совпадению имени
                if (((dynamic)MapsList.Items[i]).Name == MapData.Name)
                {
                    // Если карта найдена, то замена ее на такую-же, но с противоположным состоянием поиска по европейскому серверу
                    MapsList.Items[i] = new Map()
                    {
                        Name = MapData.Name,
                        AvaiableOnEU = !MapData.AvaiableOnEU,
                        AvaiableOnUS = MapData.AvaiableOnUS,
                        MapLinkEU = MapData.MapLinkEU,
                        MapLinkUS = MapData.MapLinkUS,
                        IsCellEnabledEU = true,
                        IsCellEnabledUS = MapData.IsCellEnabledUS,
                        CellVisibleEU = MapData.CellVisibleEU,
                        CellVisibleUS = MapData.CellVisibleUS
                    };
                }
            }

            // Запись статуса поиска по Европе в файл конфигурации
            IniFile.WriteToINI(MapData.Name, "SearchEU", !MapData.AvaiableOnEU ? "True" : "False");
        }

        // Изменение статуса поиска карты по американскому серверу
        void ChangeSearchOnUSStatus(object sender, RoutedEventArgs e)
        {
            // Сохранение информации о выбранной карте
            Map MapData = (dynamic)MapsList.SelectedItem;

            // Если карта не была выделена, то конец (это бывает после новой попытки взаимодействия с контекстным меню)
            if (MapData == null) return;

            // Перебор всех карт в таблице
            for (int i = 0; i < MapsList.Items.Count; i++)
            {
                // Поиск карты по совпадению имени
                if (((dynamic)MapsList.Items[i]).Name == MapData.Name)
                {
                    // Если карта найдена, то замена ее на такую-же, но с противоположным статусом поиска по американскому серверу
                    MapsList.Items[i] = new Map()
                    {
                        Name = MapData.Name,
                        AvaiableOnEU = MapData.AvaiableOnEU,
                        AvaiableOnUS = !MapData.AvaiableOnUS,
                        MapLinkEU = MapData.MapLinkEU,
                        MapLinkUS = MapData.MapLinkUS,
                        IsCellEnabledEU = MapData.IsCellEnabledEU,
                        IsCellEnabledUS = true,
                        CellVisibleEU = MapData.CellVisibleEU,
                        CellVisibleUS = MapData.CellVisibleUS
                    };
                }
            }

            // Запись статуса поиска по Америке в файл конфигурации
            IniFile.WriteToINI(MapData.Name, "SearchUS", !MapData.AvaiableOnUS ? "True" : "False");
        }

        // Удаление выделенных карт из списка
        void DeleteMaps_Click(object sender, EventArgs e)
        {
            // Обработка каждой выделенной карты
            while (MapsList.SelectedItems.Count > 0)
            {
                // Сохранение названия обрабатываемой карты
                string mapName = ((dynamic)MapsList.SelectedItem).Name;

                // Перебор всех карт в таблице и поиск карты по совпадению имени
                for (int i = 0; i < MapsList.Items.Count; i++)
                {
                    // Если имена совпадают
                    if (((dynamic)MapsList.Items[i]).Name == mapName)
                    {
                        // То удаление карты из файла конфигурации
                        IniFile.DeleteSection(mapName);

                        // И удаление карты из таблицы по индексу
                        MapsList.Items.RemoveAt(i);
                    }
                }
            }
        }
    }
}