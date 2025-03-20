using System;
using System.Windows;
using System.Windows.Threading;

namespace SDBEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set up global exception handling
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Exception ex = args.ExceptionObject as Exception;
                MessageBox.Show($"An unhandled exception occurred: {ex?.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            // Set up dispatcher unhandled exception handling
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"An unhandled UI exception occurred: {args.Exception.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}