namespace SC2_Lobby_Notifier
{

    //==================================================================== КЛАСС СТРОКИ КАРТЫ ТАБЛИЦЫ ====================================================================

    public class Map
    {
        // Имя карты
        public string Name { get; set; }

        // Доступность карты на европейском сервере
        public bool AvaiableOnEU { get; set; }

        // Доступность карты на американском сервере
        public bool AvaiableOnUS { get; set; }

        // Ссылка на карту на европейском сервере
        public string MapLinkEU { get; set; }

        // Ссылка на карту на американском сервере
        public string MapLinkUS { get; set; }

        // Активирован ли поиск на европейском сервере
        public bool IsCellEnabledEU { get; set; }

        // Активирован ли поиск на американском сервере
        public bool IsCellEnabledUS { get; set; }

        // Видимость чекбокса по европейскому серверу
        public System.Windows.Visibility CellVisibleEU { get; set; }

        // Видимость чекбокса по американском серверу
        public System.Windows.Visibility CellVisibleUS { get; set; }
    }
}