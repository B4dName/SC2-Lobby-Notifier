using System.Threading;
using System.Windows;

namespace SC2_Lobby_Notifier
{
    public partial class App : Application
    {
        // Предотвращение запуска более 1 приложения
        Mutex myMutex;
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            bool isNewInstance = false;
            myMutex = new Mutex(true, "SC2_Lobby_Notifier", out isNewInstance);
            if (!isNewInstance)
            {
                Current.Shutdown();
            }
        }
    }
}