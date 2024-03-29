﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Minecraft_server_backupper
{
    internal class Backuper
    {
        public bool playerActivity = false;
        public void Start(string[] args)
        {
            string worldname = WorldName();
            using var watcher = new FileSystemWatcher(@".\"+worldname+@"\playerdata");

            watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;

            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            DateTime nextBackup = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), 0);
            int hourFrequency = Convert.ToInt32(args[2]);
            int minuteFrequency = Convert.ToInt32(args[3]);
            while (nextBackup.CompareTo(DateTime.Now) < 0)
            {
                Console.WriteLine("Starting time " + nextBackup.ToString() + " already passed, advancing by wanted increment hours " + hourFrequency + " and minutes " + minuteFrequency);
                nextBackup = nextBackup.AddHours(hourFrequency);
                nextBackup = nextBackup.AddMinutes(minuteFrequency);
            }
            while (nextBackup.AddHours(hourFrequency * -1).AddMinutes(minuteFrequency * -1).CompareTo(DateTime.Now) > 0)
            {
                Console.WriteLine("Starting time " + nextBackup.ToString() + " more than the increment away, making start time earlier by the increment " + hourFrequency + " and minutes " + minuteFrequency);
                nextBackup = nextBackup.AddHours(hourFrequency * -1);
                nextBackup = nextBackup.AddMinutes(minuteFrequency * -1);
            }
            Console.WriteLine("Starting backupper, next backup is done:" + nextBackup.ToString());
            while (true)
            {
                System.Threading.Thread.Sleep(700);
                MinecraftClient.MinecraftClient client;
                MinecraftClient.Message resp;

                if (nextBackup.CompareTo(DateTime.Now) < 0)
                {
                    nextBackup = nextBackup.AddHours(hourFrequency);
                    nextBackup = nextBackup.AddMinutes(minuteFrequency);
                    if (!playerActivity)
                    {
                        Console.WriteLine("No player has been on the server since last backup, skipping backup to save hard drive space");
                    }
                    else
                    {
                        client = new MinecraftClient.MinecraftClient("localhost", Convert.ToInt32(args[5]));
                        if (!client.Authenticate(args[4]))
                        {
                            Console.WriteLine("authentication failure");
                            client.Close();
                            return;
                        }
                        if (client.SendCommand("save-all", out resp))
                        {
                            Console.WriteLine(resp.Body);
                        }
                        else
                        {
                            Console.WriteLine("Error saving.");
                            break;
                        }
                        if (client.SendCommand("save-off", out resp))
                        {
                            Console.WriteLine(resp.Body);
                        }
                        else
                        {
                            Console.WriteLine("Error turning saving off.");
                            break;
                        }
                        Console.WriteLine("backing up");
                        System.Threading.Thread.Sleep(5000);
                        Console.WriteLine(".\\backups\\" + worldname + "_" + DateTime.Now.ToString("yyyyMMdd_hhmm"));
                        System.Threading.Thread.Sleep(2000);
                        try
                        {
                            DirectoryCopy(worldname, ".\\backups\\"+worldname+"_"+ DateTime.Now.ToString("yyyyMMdd_hhmm") + "\\", true);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                        if (client.SendCommand("save-on", out resp))
                        {
                            Console.WriteLine(resp.Body);
                        }
                        else
                        {
                            Console.WriteLine("Error turning saving on.");
                            break;
                        }
                        playerActivity = false;
                        client.Close();
                    }
                }
            }
        }
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                if (!file.Name.Contains("session.lock"))
                {
                    string tempPath = Path.Combine(destDirName, file.Name);
                    file.CopyTo(tempPath, false);
                }
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            Console.WriteLine($"Changed: {e.FullPath}");
            playerActivity = true;
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            string value = $"Created: {e.FullPath}";
            Console.WriteLine(value);
            playerActivity = true;
        }
        private string WorldName()
        {
            // Read in lines from file.
            foreach (string line in File.ReadLines(@".\server.properties"))
            {
                Console.WriteLine($"{line}");
                if (line.Split("=")[0]=="level-name")
                {
                    string _levelName = line.Split("=",2)[1].Replace("\\", "");
                    Console.WriteLine(_levelName);
                    return _levelName;
                }
            }
            return null;
        }
    }
}
