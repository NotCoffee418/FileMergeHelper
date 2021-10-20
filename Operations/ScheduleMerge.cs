namespace FileMergeHelper.Operations
{
    class ScheduleMerge : IOperation
    {
        public string Description => 
            "Copy files without conflict and prepare conflict file";

        public void Run()
        {
            // Load config
            Config? conf = JsonConvert.DeserializeObject<Config>("config.json");
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

            // index all files by filename
            // md5, <paths>
            Dictionary<string, List<string>> sortedPaths = new();
            foreach (var dirConts in sourceFiles.Values)
                foreach (string path in dirConts)
                {
                    string md5 = Helper.CalculateMD5(path);

                    // Create entry if missing
                    if (!sortedPaths.ContainsKey(md5))
                        sortedPaths.Add(md5, new());

                    // Add it
                    sortedPaths[md5].Add(path);
                }

            // Add a copy of each md5 to move queue
            List<string> filesToMove = sortedPaths
                .Select(x => x.Value[0])
                .ToList();

            // Handle duplicate filenames (manual review)
            var duplicateFilePathGroups = filesToMove
                .GroupBy(x => conf.SourceDirs
                        .Select(y => x.ToLower().StartsWith(y.ToLower()))
                        .First());

            //


        }
    }
}