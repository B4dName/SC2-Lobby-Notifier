using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Windows.Media;

namespace SC2_Lobby_Notifier
{

    //============================================================================= КЛАСС ДЛЯ СОДЕРЖАНИЕ ОБЩИХ ЭЛЕМЕНТОВ МЕЖДУ КЛАССАМИ =============================================================================

    class Addition
    {
        //============================================================================= ЦВЕТНЫЕ СООБЩЕНИЯ ДЛЯ ВЫВОДА В ПАНЕЛЬ ЛОГОВ =============================================================================

        // Тип данных хранящий цветной текст
        public struct Log
        {
            public string Text;
            public SolidColorBrush Color;

            public Log(string text, SolidColorBrush color)
            {
                this.Text = text;
                this.Color = color;
            }
        }

        // Список всех сообщений для вывода в панель логов
        public static List<Log> LogMessages = new List<Log>();



        //============================================================================= ФУНКЦИИ =============================================================================

        // Небольшой класс для добавления ограничений по времени для получения ответов на запросы
        private class TimeoutWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri uri)
            {
                // Создание запроса
                WebRequest request = base.GetWebRequest(uri);

                // Установка ограничения по времени в миллисекундах (5 секунд)
                request.Timeout = 5000;

                // Возврат запроса
                return request;
            }
        }

        // Получение ответа по запросу
        public static string GetResponseAsJson(string response)
        {
            // Получаемый в итоге JSON файл
            string json;

            // Максимальное кол-во итерации при попытке получить ответ на запрос (2 итерации)
            const int MaxResponseIterationCount = 2;

            // Ответ на запрос получается до тех пор, пока не будет получено хоть что-то до MaxResponseIterationCount итераций
            for (int i = 0; i < MaxResponseIterationCount; i++)
            {
                // Предотвращение ошибок при отсутствии подключения по сети
                try
                {
                    // Создание клиента c указанием кодировки UTF8 для обработки символов любого языка и получение ответа на запрос
                    json = new TimeoutWebClient() { Encoding = Encoding.UTF8 }.DownloadString(new Uri(response));

                    // Возврат ответа на запрос
                    return json;
                }
                catch { }
            }

            // Возврат пустой строки как ошибки
            return "";
        }

        // Открытие веб-страницы
        public static void OpenLink(string url)
        {
            try
            {
                // Открытие веб-страницы в браузере по умолчанию
                System.Diagnostics.Process.Start(url);
            }
            catch
            {
                // Непроверенная возможность отсутствия браузера по умолчанию в системе поэтому все и выполняется в try catch
            }
        }
    }
}
