using System;
using System.IO;

namespace Minecraft_server_backupper
{
    class Program
    {
        //args: [startinghour] [startingminute] [hourFrequency] [minuteFrequency] [rcon password] [rcon port]
        static void Main(string[] args)
        {
            if (args[0].Equals("help"))
            {
                Console.WriteLine("Usage: \"minecraft server backuper.exe\" [startinghour] [startingminute] [hourFrequency] [minuteFrequency] [rcon password] [rcon port]");
                return;
            }
            var backuper = new Backuper();
            backuper.Start(args);
        }
    }
}
