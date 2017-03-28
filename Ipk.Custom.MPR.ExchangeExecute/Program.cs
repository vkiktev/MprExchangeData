//-----------------------------------------------------------------------
// <copyright file="Program.cs" author="Slava Kiktev">
//
// Copyright © 2016 Slava Kiktev.  All rights reserved.
//
// </copyright>
//-----------------------------------------------------------------------

using System;
using Ipk.Custom.MPR.Exchange;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Ipk.Custom.MPR.ExchangeExecute
{
    /// <summary>
    /// Console-application which run exchange method for sync two databases
    /// </summary>
    public class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Program));

        static string _argoConnectionString;
        static string _mprConnectionString;

        static void Main(string[] args)
        {
            PrintInfo("Start exchange");
            if (args == null || args.Length != 4)
                PrintHelp();
            else
            {
                if (args[0] == @"\ArgoConnectionString")
                    _argoConnectionString = args[1];
                else if (args[0] == @"\MprConnectionString")
                    _mprConnectionString = args[1];

                if (args[2] == @"\ArgoConnectionString")
                    _argoConnectionString = args[3];
                else if (args[2] == @"\MprConnectionString")
                    _mprConnectionString = args[3];

                if (string.IsNullOrWhiteSpace(_mprConnectionString) || string.IsNullOrWhiteSpace(_argoConnectionString))
                    PrintHelp();
                else
                {
                    PrintInfo(string.Format("Parameters: ArgoConnectionString: {0}, MprConnectionString: {1}", _argoConnectionString, _mprConnectionString));
                    Exchange();
                }
            }
            PrintInfo("Finish exchange");
        }

        /// <summary>
        /// Method for running sync process
        /// </summary>
        private static void Exchange()
        {
            SyncInstance syncInstance = new SyncInstance(_argoConnectionString, _mprConnectionString);
            syncInstance.ExchnageEventCaused += SyncInstance_ExchnageEventCaused;
            syncInstance.StartExchange();
        }

        static void SyncInstance_ExchnageEventCaused(object sender, ExchangeEventArgs e)
        {
            PrintInfo(e.ToString());
        }

        private static void PrintError(string message, Exception ex)
        {
            log.Error(message, ex);
        }

        private static void PrintInfo(string message)
        {
            log.Info(message);
        }

        private static void PrintHelp()
        {
            string help = @"Использование: ExchangeExecute.exe \ArgoConnectionString строка \MprConnectionString строка
Параметры:
    \ArgoConnectionString      Строка подключения к базе данных арго
    \MprConnectionString       Строка подключения к базе данных МПР  

Пример: ExchangeExecute.exe \ArgoConnectionString ""server=ARGOSERVER;database=ArgoDB;Integrated Security=True;"" \MprConnectionString ""server=(local);database=MPR;Integrated Security=True;""";
            Console.WriteLine(help);
        }
    }
}
