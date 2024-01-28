using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace Minecraft_server_backupper
{
    internal class Backuper
    {
        public bool playerActivity = false;
        public string backupFolder;
        public MinecraftClient.MinecraftClient client;
        public string RconPassword;
        public string RconPort;
        public string WorldName;
        public void Start(string[] args)
        {
            WorldName = GetWorldName();
            RconPassword = args[4];
            RconPort = args[5];
            backupFolder = ".\\backups\\" + WorldName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + "\\";
            using var watcher = new FileSystemWatcher(@".\"+WorldName +@"\playerdata");

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
        private string GetWorldName()
        {
            // Read in lines from file.
            foreach (string line in File.ReadLines(@".\server.properties"))
            {
                if (line.Split("=")[0]=="level-name")
                {
                    string _levelName = line.Split("=",2)[1].Replace("\\", "");
                    return _levelName;
                }
            }
            return null;
        }
        // Process all files in the directory passed in, recurse on any directories
        // that are found, and process the files they contain.
        public void Backup(string targetDirectory)
        {
            client = new MinecraftClient.MinecraftClient("localhost", Convert.ToInt32(RconPort));
            MinecraftClient.Message resp;
            backupFolder = ".\\backups\\" + WorldName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + "\\";
            if (!client.Authenticate(RconPassword))
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
                return;
            }
            if (client.SendCommand("save-off", out resp))
            {
                Console.WriteLine(resp.Body);
            }
            else
            {
                Console.WriteLine("Error turning saving off.");
                return;
            }


            Console.WriteLine("backing up");
            Console.WriteLine(".\\backups\\" + WorldName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));


            DirectoryCopy(WorldName, backupFolder, true);


            if (client.SendCommand("save-on", out resp))
            {
                Console.WriteLine(resp.Body);
            }
            else
            {
                Console.WriteLine("Error turning saving on.");
                return;
            }
            playerActivity = false;
            client.Close();

            Compress(backupFolder);

        }

        // Insert logic for processing found files here.
        public void ProcessFile(string path)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    var hash = md5.ComputeHash(stream);
                    var checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    Console.WriteLine("file '{0}' checksum {1}", path, checksum);

                    try
                    {
                        // Append the line to the text file
                        File.AppendAllText(backupFolder + "\\filelist.txt", checksum+":"+path + Environment.NewLine);
                        Console.WriteLine("Line added successfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An error occurred: {ex.Message}");
                    }
                    if(!File.Exists($"all_files_{WorldName}\\"+checksum))
                    {
                        File.Copy(path, $"all_files_{WorldName}\\" + checksum);
                    }
                    
                    stream.Close();
                    if (!path.Contains("filelist.txt"))
                        File.Delete(path);

                }

            }
        }

        public void Compress(string targetDirectory)
        {
            if (!Directory.Exists($"all_files_{WorldName}"))
            {
                Directory.CreateDirectory($"all_files_{WorldName}");
            }
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory);
            foreach (string fileName in fileEntries)
            {
                ProcessFile(fileName);
            }

            // Recurse into subdirectories of this directory.
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
                Compress(subdirectory);
        }
    }
}
