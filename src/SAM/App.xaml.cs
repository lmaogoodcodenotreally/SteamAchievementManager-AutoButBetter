using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Console;
using CommandLine;
using CommandLine.Text;
using log4net;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using SAM.API;
using SAM.Core;
using SAM.Core.Logging;
using SAM.ViewModels;
using SAM.SplashScreen;
using SteamGameViewModel = SAM.ViewModels.SteamGameViewModel;
using SAM.Managers;

namespace SAM;

public partial class App
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
    protected readonly ILog log = LogManager.GetLogger(nameof(App));

    protected override void OnStartup(StartupEventArgs args)
    {
        base.OnStartup(args);

        try
        {
            GlobalContext.Properties[AssemblyVersionHelper.KEY] = new AssemblyVersionHelper();
            GlobalContext.Properties[EntryAssemblyHelper.KEY] = new EntryAssemblyHelper();
            GlobalContext.Properties[SteamAppContextHelper.KEY] = new SteamAppContextHelper();

            // Wait for Steam process instead of crashing
            if (!SAMHelper.IsSteamRunning())
            {
                SplashScreenHelper.Show("Waiting for Steam...");

                while (!SAMHelper.IsSteamRunning())
                {
                    System.Threading.Thread.Sleep(2000);
                }

                // Give Steam a moment to fully initialize
                System.Threading.Thread.Sleep(3000);
                SplashScreenHelper.Close();

                // Re-initialize splash infrastructure so it can be used again later
                // (Close shuts down the splash dispatcher, so we need a fresh start)
            }

            // handle any WPF dispatcher exceptions
            Current.DispatcherUnhandledException += OnDispatcherException;

            // handle any AppDomain exceptions
            var current = AppDomain.CurrentDomain;
            current.UnhandledException += OnAppDomainException;

            // handle any TaskScheduler exceptions
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            var helpWriter = new StringWriter();
            var parser = new Parser(with =>
            {
                //ignore case for enum values
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = helpWriter;
            });

            var options = parser.ParseArguments<SelectOptions, ManageOptions>(args.Args);

            // MessageBox.Show(JsonConvert.SerializeObject(options, Formatting.Indented));

            var result = options.MapResult(
                    (SelectOptions o) => HandleSelect(o),
                    (ManageOptions o) => HandleManage(o),
                    errors =>
                    {
                        // assume that this was launched from command line with invalid arguments
                        // and attach to the parent process' console since WPF apps don't have a
                        // console otherwise
                        var attached = PInvoke.AttachConsole(PInvoke.ATTACH_PARENT_PROCESS);
                        
                        var helpText = HelpText.AutoBuild(options);
                        var message = helpText.ToString();
                        
                        var err = errors.ToList();
                        var showHelp = err.IsHelp() || err.IsVersion();

                        if (attached)
                        {
                            var handle = showHelp
                                ? PInvoke.GetStdHandle_SafeHandle(STD_HANDLE.STD_OUTPUT_HANDLE)
                                : PInvoke.GetStdHandle_SafeHandle(STD_HANDLE.STD_ERROR_HANDLE);

                            unsafe
                            {
                                // TODO: this output formatting needs to be fixed
                                var ptr = (uint*) 0;
                                PInvoke.WriteConsole(handle, "\n", 1, ptr);
                                PInvoke.WriteConsole(handle, message, (uint) message.Length, ptr);
                                PInvoke.WriteConsole(handle, "\n", 1, ptr);
                            }
                        }
                        else
                        {
                            var icon = showHelp ? MessageBoxImage.Information : MessageBoxImage.Error;
                            MessageBox.Show(message, "Usage", MessageBoxButton.OK, icon);
                        }

                        return SAMExitCode.InvalidArguments;
                    }
                );

            log.Debug($"Argument {nameof(parser)} returned {result}");

            if (result != 0)
            {
                Environment.Exit(result);
            }
        }
        catch (Exception e)
        {
            var message = $"An error occurred on application startup. {e.Message}";

            log.Error(message, e);

            MessageBox.Show(message, @"SAM Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);

            Environment.Exit(SAMExitCode.UnhandledException);
        }
    }
    
    // ReSharper disable once InconsistentNaming
    private void DisplayHelp(IEnumerable<Error> err)
    {
        var errors = err.ToList();

        if (errors.IsVersion() || errors.IsHelp())
        {
            Console.WriteLine();
        }
    }

    private int HandleSelect(SelectOptions options)
    {
        log.Info($"Starting processing {nameof(SelectOptions)}...");

        SplashScreenHelper.Show("Starting up...");

        // create the default Client instance
        SteamClientManager.Init(0);

        SteamLibraryManager.Default.Init();

        var settings = HomeSettings.Load();

        if (options.TileView)
        {
            log.Info($"{nameof(options.TileView)} argument detected. Setting {nameof(HomeSettings)} {nameof(HomeSettings.View)} to {LibraryView.Tile:G}");

            settings.View = LibraryView.Tile;
        }
        else if (options.GridView)
        {
            log.Info($"{nameof(options.GridView)} argument detected. Setting {nameof(HomeSettings)} {nameof(HomeSettings.View)} to {LibraryView.DataGrid:G}");

            settings.View = LibraryView.DataGrid;
        }

        MainWindow = new MainWindow
        {
            DataContext = new MainWindowViewModel(settings)
        };

        MainWindow.Show();

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        return 0;
    }

    private int HandleManage(ManageOptions options)
    {
        log.Info($"Starting processing {nameof(ManageOptions)}...");

        var appId = options.AppId;

        SteamClientManager.Init(appId);

        if (!SteamClientManager.Default.OwnsGame(appId))
        {
            throw new SAMInitializationException($"The current Steam account does not have a license for app '{appId}'.");
        }

        var appInfo = new SteamApp(appId);
        var gameVm = new SteamGameViewModel(appInfo);
        gameVm.RefreshStats();

        if (options.UnlockAll)
        {
            gameVm.UnlockAllAchievements();

            gameVm.Save(false);

            return 0;
        }

        SplashScreenHelper.Show(appInfo.Name);

        var mainWindowVm = new MainWindowViewModel(gameVm)
        {
            SubTitle = appInfo.Name
        };

        MainWindow = new MainWindow
        {
            DataContext = mainWindowVm
        };
        
        MainWindow.Show();

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        return 0;
    }

    protected override void OnExit(ExitEventArgs args)
    {
        base.OnExit(args);

        // TODO: with there being only one executable need a new way to differentiate the process types
        //try
        //{
        //    log.Info(@"Application exiting. Ending any running manager processes...");

        //    SAMHelper.CloseAllManagers();
        //}
        //catch (Exception e)
        //{
        //    log.Fatal($"An error occurred attempting to exit the SAM Managers. {e.Message}", e);
        //}
    }

    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
    {
        try
        {
            var exception = args.Exception;
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            var message = $"An unobserved task exception occurred. {exception.Message}";

            log.Error(message, args.Exception);

            WriteCrashLog(args.Exception, "UnobservedTaskException");

            MessageBox.Show(message, $"Unhandled ${exception.GetType().Name}", MessageBoxButton.OK, MessageBoxImage.Error);

            args.SetObserved();
        }
        catch (Exception e)
        {
            log.Fatal($"An error occurred in {nameof(OnUnobservedTaskException)}. {e.Message}", e);

            Environment.Exit(SAMExitCode.TaskException);
        }
    }

    private void OnAppDomainException(object sender, UnhandledExceptionEventArgs args)
    {
        try
        {
            var exception = (Exception) args.ExceptionObject;
            var message = $"Dispatcher unhandled exception occurred. {exception.Message}";

            log.Fatal(message, exception);

            WriteCrashLog(exception, "AppDomainUnhandledException");

            MessageBox.Show(message, $"Unhandled ${exception.GetType().Name}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception e)
        {
            log.Fatal($"An error occurred in {nameof(OnAppDomainException)}. {e.Message}", e);
        }
        finally
        {
            Environment.Exit(SAMExitCode.AppDomainException);
        }
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        try
        {
            var message = $"Dispatcher unhandled exception occurred. {args.Exception.Message}";

            log.Fatal(message, args.Exception);

            WriteCrashLog(args.Exception, "DispatcherUnhandledException");

            Environment.Exit(SAMExitCode.DispatcherException);
        }
        catch (Exception e)
        {
            var message = $"An error occurred in {nameof(OnDispatcherException)}. {e.Message}";

            Environment.FailFast(message);
        }
    }

    private void WriteCrashLog(Exception ex, string source)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            var uploadDir = Path.Combine(baseDir, "upload", "logs");
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            var crashFile = Path.Combine(uploadDir, "crash.log");
            using var fs = new FileStream(crashFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var sw = new StreamWriter(fs);
            sw.WriteLine("----");
            sw.WriteLine($"Timestamp: {DateTime.UtcNow:O}");
            sw.WriteLine($"Source: {source}");
            sw.WriteLine($"Process: {Environment.ProcessPath} (PID: {Environment.ProcessId})");
            sw.WriteLine($"User: {Environment.UserName}");
            sw.WriteLine($"Message: {ex.Message}");
            sw.WriteLine($"Type: {ex.GetType().FullName}");
            sw.WriteLine($"StackTrace:\n{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                sw.WriteLine("InnerException:");
                sw.WriteLine($"Type: {ex.InnerException.GetType().FullName}");
                sw.WriteLine($"Message: {ex.InnerException.Message}");
                sw.WriteLine($"StackTrace:\n{ex.InnerException.StackTrace}");
            }
            sw.WriteLine();
            sw.Flush();
            fs.Flush(true);
        }
        catch
        {
            // best-effort only; don't throw from crash logging
        }
    }
}
