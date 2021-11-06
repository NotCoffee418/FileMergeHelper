using System.Collections.Concurrent;
using CoffeeToolkit.Progress;
using FileMergeHelper.Helpers;
using Humanizer;

namespace FileMergeHelper.Operations
{
    class ScheduleMerge : IOperation
    {
        public string Description => 
            "Copy files without conflict and prepare conflict file";

        public void Run()
        {
            // Load config
            string json = File.ReadAllText("config.json");
            Config? conf = JsonConvert.DeserializeObject<Config>(json);
            if (conf?.OutputDir == "/output/directory/here")
            {
                Console.WriteLine("Set up config.json before running this.");
                return;
            }

            // Get all files to be merged
            Dictionary<string, List<string>> sourceFiles = new();
            foreach (string sourceDir in conf.SourceDirs)
            {
                var files = Directory.EnumerateFiles(
                    sourceDir, "*", SearchOption.AllDirectories);
                sourceFiles.Add(sourceDir, files.ToList());
            }

            // Calculate all MD5s
            Console.WriteLine("Calculating hashes. This will take a few minutes...");
            List<Task> calcMd5Tasks = new List<Task>();
            ConcurrentDictionary<string, string> hashes = new(); // path, md5

            // Attempt to load from cache
            if (File.Exists("cache_hashes.bson"))
            {
                byte[] cachedHashData = File.ReadAllBytes("cache_hashes.bson");
                hashes = BsonHelper.DeserializeObject<ConcurrentDictionary<string, string>>(
                    cachedHashData) ?? new();
            }

            // Load everything else
            foreach (var dirConts in sourceFiles.Values)
                foreach (string path in dirConts)
                {
                    calcMd5Tasks.Add(Task.Factory.StartNew(() =>
                    {
                        if (!hashes.ContainsKey(path))
                        {
                            string md5 = Helper.CalculateMD5(path);
                            hashes.TryAdd(path, md5);
                        }
                    }));
                }


            // Await completion and cache
            Task.WaitAll(calcMd5Tasks.ToArray());
            byte[] finalHashesBson = BsonHelper.SerializeObject(hashes);
            File.WriteAllBytes("cache_hashes.bson", finalHashesBson);


            // index all files by filename
            // md5, <paths>
            int currentPriority = 0;
            ConcurrentBag<FileData> exportData = new();
            foreach (var sourceDirData in sourceFiles)
            {
                // Clarify data
                string sourceDirPath = sourceDirData.Key;
                List<string> dirContents = sourceDirData.Value;

                // One input directory at a time to handle merge conflicts correctly
                List<Task> directoryAssessnmentTasks = new();
                foreach (string fullSourcePath in dirContents)
                    directoryAssessnmentTasks.Add(Task.Factory.StartNew(() =>
                    {
                        string md5 = hashes[fullSourcePath];

                        // Filter out duplicates
                        bool isDuplicateHash = exportData
                            .Where(x => x.Hash == md5)
                            .Count() > 0;

                        Console.WriteLine(md5 + ": " + (isDuplicateHash ? "duplicate" : "new"));
                        if (isDuplicateHash)
                            return;

                        long fileSize = new FileInfo(fullSourcePath).Length;

                        // Still here, create entry
                        exportData.Add(new()
                        {
                            SourceDirPriority = currentPriority,
                            Hash = md5,
                            SourceDir = sourceDirPath,
                            FilePath = fullSourcePath.Substring(sourceDirPath.Length),
                            FileSize = fileSize
                        });
                    }));

                // run tasks
                Task.WaitAll(directoryAssessnmentTasks.ToArray());
                currentPriority++;
            }

            // Select the latest version of duplicate filepaths
            // Key: FilePath - Values: FileDatas
            var filePathGroups = exportData
                .OrderBy(x => x.SourceDirPriority)
                .GroupBy(x => x.FilePath)
                .ToList();

            // report on duplicate count
            Console.WriteLine($"These are duplicate filenames with different contents:");
            foreach (var filePathGroup in filePathGroups.Where(x => x.Count() > 1))
            {
                filePathGroup.ToList().ForEach(x =>
                    Console.WriteLine(Path.Join(x.SourceDir, x.FilePath))
                );
                Console.WriteLine();
            }

            // request override permission
            while (!UserInput.PoseBoolQuestion(
                "Is it okay to override these with the latest version as defined by the input order in the config file?", defaultAnswer: false))
                Console.WriteLine("Can't continue until override permission is granted.");

            // Schedule all files which should moved
            Console.WriteLine("Preparing move queue...");
            List<FileData> moveQueue = new List<FileData>();

            // Run move queue
            if (File.Exists("cache_finalmovequeue.bson"))
            {
                moveQueue = BsonHelper.DeserializeObject<List<FileData>>(
                    File.ReadAllBytes("cache_finalmovequeue.bson"));
            }
            // No cache, run the queue
            else
            {
                foreach (var filePathGroup in filePathGroups)
                    moveQueue.Add(filePathGroup.Last());
                // Cache it
                byte[] moveQueueBson = BsonHelper.SerializeObject(moveQueue);
                File.WriteAllBytes("cache_finalmovequeue.bson", finalHashesBson);
            }


            // Report on final filesize
            long totalFileSize = moveQueue
                .Select(x => x.FileSize)
                .Sum();
            Console.WriteLine("Required filesize on output directory: " + totalFileSize.Bytes());

            // Copy?
            bool copyInstead = UserInput.PoseBoolQuestion(
                "Would you like to copy files instead of move?",
                defaultAnswer: true);

            // Readonly?
            bool shouldMakeReadOnly = UserInput.PoseBoolQuestion(
                "Would you like to make these files readonly after moving?",
                defaultAnswer: true);
            Console.WriteLine("Moving files to output directory...");

            // Prepare progress tracker
            ProgressTracker pt = new ProgressTracker(moveQueue.Count);
            int lastPercent = 0;
            pt.ProgressChanged += (sender, eventArgs) =>
            {
                int currentPercent = (int)Math.Floor(eventArgs.ProgressPercentage);
                if (currentPercent > lastPercent)
                {
                    lastPercent = currentPercent;
                    Console.WriteLine($"[{DateTime.Now}] {currentPercent}%");
                }
            };
            

            // Prepare folder structure
            foreach (FileData file in moveQueue)
            {
                // Create directory
                string targetDir = Path.GetDirectoryName(
                    Path.Join(conf.OutputDir, file.FilePath));
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);                
            }

            // Move/copy
            moveQueue
                .AsParallel()
                .ForAll(file =>
                {
                    // Prepare paths
                    string sourceFilePath = Path.Join(file.SourceDir, file.FilePath);
                    string destinationFilePath = Path.Join(conf.OutputDir, file.FilePath);

                    // Do move or copy
                    if (copyInstead)
                        File.Copy(sourceFilePath, destinationFilePath);
                    else
                        File.Move(sourceFilePath, destinationFilePath);

                    // Make readonly
                    if (shouldMakeReadOnly)
                        File.SetAttributes(destinationFilePath, FileAttributes.ReadOnly);

                    // Report progress
                    pt.IncrementProgress();
                });

            Console.WriteLine("Files merged successfully!");
        }
    }
}