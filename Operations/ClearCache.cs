using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileMergeHelper.Operations
{
    public class ClearCache : IOperation
    {
        public string Description => "Clear Cache";

        public void Run()
        {
            List<string> cacheFiles = new()
            {
                "cache_hashes.bson",
                "cache_exportdata.bson",
                "cache_finalmovequeue.bson"
            };

            foreach (var cacheFile in cacheFiles)
                if (File.Exists(cacheFile))
                    File.Delete(cacheFile);

            Console.WriteLine("Cache is cleared.");
        }
    }
}
