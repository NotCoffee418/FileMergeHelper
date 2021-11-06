// Prepare config file
if (!File.Exists("config.json"))
{
    string json = JsonConvert.SerializeObject(new Config {
        OutputDir = "/output/directory/here",
        SourceDirs = new() {
            "/source/dir/one",
            "/source/dir/two"
        }
    }, Formatting.Indented);
    File.WriteAllText("config.json", json);
}

// Register operations
OperationManager.RegisterOperationsBulk(
    new List<IOperation>() {
        new ScheduleMerge(),
        new ClearCache()
    }
);
OperationManager.StartListening();
