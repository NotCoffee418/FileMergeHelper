public class FileData
{
    /// <summary>
    /// Order in which the file is read. Higher number will take priority when filenames are duplicate.
    /// </summary>
    public int SourceDirPriority { get; set; }

    /// <summary>
    /// MD5 hash
    /// </summary>
    public string Hash { get; set; }

    /// <summary>
    /// Source directory
    /// </summary>
    public string SourceDir { get; set; }

    /// <summary>
    /// File path excluding source dir
    /// </summary>
    public string FilePath { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }
}