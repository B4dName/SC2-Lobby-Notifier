// Замена квадратных скобок в CorruptString и RecoverString маловероятно, но может подвергаться ошибкам

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SC2_Lobby_Notifier
{

    //==================================================================== КЛАСС ФАЙЛА КОНФИГУРАЦИИ ====================================================================

    /// <summary>
    /// Позволяет работать с файлами конфигурации
    /// </summary>
    class IniFile
    {

        // ========== Импортирование функций из ядра Windows ==========

        // Запись данных в файл конфигурации по секции и ключу
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(byte[] section, string key, string val, string filePath);

        // Чтение данных из файла конфигурации по секции и ключу
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(byte[] section, string key, byte[] def, byte[] retVal, int size, string filePath);


        /// <summary>
        /// Путь к файлу конфигурации
        /// </summary>
        public string Path;


        /// <summary>
        /// Указание пути к файлу 
        /// </summary>
        public IniFile(string path)
        {
            this.Path = path;
        }



        // ========== Дополнительные функции для работы с квадратными скобками в названиях секций ==========

        // Изменение квадратных скобок строки для записи в файл (делается во избежание ошибок чтения)
        private byte[] CorruptString(string input)
        {
            // Замена квадратных скобок на фигурные скобки в кавычках
            input = input.Replace("[", "\"{\"").Replace("]", "\"}\"");

            // Указание кодировки UTF-8 для обработки строк, содержащих символы не из латиницы
            return Encoding.GetEncoding("utf-8").GetBytes(input);
        }

        // Восстановление квадратных скобок в строке из файла при чтении
        private string RecoverString(string input)
        {
            // Замена фигурных скобок в кавычках на квадратные скобки 
            return input.Replace("\"{\"", "[").Replace("\"}\"", "]");
        }


        // Функции для работы с файлом конфигурации

        /// <summary>
        /// Запись данных в ключ секции файла конфигурации
        /// </summary>
        public void WriteToINI(string section, string key, string value)
        {
            WritePrivateProfileString(CorruptString(section), key, value, Path);
        }

        /// <summary>
        /// Удаление секции в файле конфигурации
        /// </summary>
        public void DeleteSection(string section)
        {
            WritePrivateProfileString(CorruptString(section), null, null, Path);
        }

        /// <summary>
        /// Чтение значения ключа секции из файла конфигурации
        /// </summary>
        public string ReadFromINI(string section, string key)
        {
            // Считываемое значение ключа
            byte[] buffer = new byte[1024];

            // Получение кол-ва символов в ключе и копирование его значения в виде байтов в buffer
            int count = GetPrivateProfileString(CorruptString(section), key, null, buffer, 255, Path);

            // Возврат значения ключа
            return Encoding.GetEncoding("utf-8").GetString(buffer, 0, count);
        }

        /// <summary>
        /// Получение названий всех секций из файла конфигурации
        /// </summary>
        public List<string> GetAllSectionsNames()
        {
            // Получение всех строчек из файла в виде массива
            string[] fileStrings = File.ReadAllText(Path).Split(new string[] { "\r\n" }, StringSplitOptions.None);

            // Будущий список всех названий секций
            List<string> sections = new List<string>();

            // Добавление каждой секции в список
            for (int i = 0; i < fileStrings.Length - 1; i += 5)
            {
                // Удаление квадратных скобок по краям секции
                sections.Add(RecoverString(fileStrings[i].Substring(1, fileStrings[i].Length - 2)));
            }

            // Возврат всех названий секций
            return sections;
        }
    }
}